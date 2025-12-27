using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using FamilyBillSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FamilyBillSystem.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly VerificationCodeService _verificationCodeService;
        private readonly QiniuService _qiniuService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext context, IConfiguration configuration,
                         HttpClient httpClient, VerificationCodeService verificationCodeService,
                         QiniuService qiniuService, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _verificationCodeService = verificationCodeService;
            _qiniuService = qiniuService;
            _logger = logger;
        }

        public async Task<AuthResponse> Login(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null);

            if (user == null)
                throw new Exception("邮箱或密码错误");

            if (!PasswordHelper.VerifyPW(request.Password, user.PasswordHash, user.PasswordSalt))
                throw new Exception("邮箱或密码错误");

            if (user.Status == "frozen")
                throw new Exception("账户已被冻结");

            await UpdateLoginInfo(user);

            var accessToken = GenerateJwtToken(user, false);
            var refreshToken = GenerateJwtToken(user, true);

            return new AuthResponse
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                User = MapToUserDto(user)
            };
        }

        public async Task<AuthResponse> WeChatLogin(WeChatLoginRequest request)
        {
            var openId = await GetWeChatOpenId(request.Code);

            if (string.IsNullOrEmpty(openId))
                throw new Exception("获取微信用户信息失败");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.OpenId == openId && u.DeletedAt == null);

            if (user == null)
            {
                user = new User
                {
                    OpenId = openId,
                    Nickname = $"微信用户{DateTime.Now:yyyyMMddHHmmss}",
                    AvatarUrl = "/images/user.png",
                    Status = "active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Email = $"wechat_{DateTime.Now:yyyyMMddHHmmss}@wechat.com",
                    Settings = "{}",
                    Phone = "",
                    LastLoginAt = DateTime.Now,
                    LoginCount = 1
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync(); // 立即保存新用户
            }
            else
            {
                // 对于已存在的用户，不自动覆盖其自定义的昵称和头像
                // 只更新登录信息
                await UpdateLoginInfo(user);
            }

            var accessToken = GenerateJwtToken(user, false);
            var refreshToken = GenerateJwtToken(user, true);

            return new AuthResponse
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                User = MapToUserDto(user)
            };
        }

        public async Task<AuthResponse> Register(RegisterRequest request)
        {
            if (!_verificationCodeService.VerifyCode(request.Email, request.VerificationCode))
                throw new Exception("验证码错误或已过期");

            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.DeletedAt == null))
            {
                throw new Exception("该邮箱已被注册");
            }

            PasswordHelper.CreatePWHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Email = request.Email,
                Nickname = request.NickName ?? request.Email.Split('@')[0],
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Status = "active",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                AvatarUrl = "/images/user.png", // 使用前端路径的默认头像
                Settings = "{}", // 添加默认设置
                Phone = "", // 添加空手机号
                OpenId = null, // 添加空OpenId
                Gender = null, // 添加空性别
                LastLoginAt = DateTime.Now, // 设置最后登录时间为当前时间
                LoginCount = 0 // 初始化登录次数为0
            };

            _context.Users.Add(user);
            await UpdateLoginInfo(user);

            var accessToken = GenerateJwtToken(user, false);
            var refreshToken = GenerateJwtToken(user, true);

            return new AuthResponse
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                User = MapToUserDto(user)
            };
        }

        public async Task<bool> CheckEmailExists(string email)
        {
            return await _context.Users
                .AnyAsync(u => u.Email == email && u.DeletedAt == null);
        }

        private async Task<string?> GetWeChatOpenId(string code)
        {
            var appId = _configuration["WeChat:AppId"];
            var appSecret = _configuration["WeChat:AppSecret"];
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={appSecret}&js_code={code}&grant_type=authorization_code";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<WeChatResponse>(url);
                return response?.OpenId;
            }
            catch
            {
                return null;
            }
        }

        private string GenerateJwtToken(User user, bool isRefreshToken = false)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key未配置"));

                // 使用标准Claims，确保与JWT中间件配置一致
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("type", isRefreshToken ? "refresh" : "access"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                // 只在AccessToken中添加用户信息
                if (!isRefreshToken)
                {
                    claims.Add(new Claim(ClaimTypes.Email, user.Email ?? ""));
                    claims.Add(new Claim(ClaimTypes.Name, user.Nickname ?? ""));
                    claims.Add(new Claim("uid", user.Id.ToString())); // 保留自定义claim以兼容现有代码
                }

                // Access Token: 2小时，Refresh Token: 7天
                var expiryMinutes = isRefreshToken ? 10080 : 120; // 延长AccessToken到2小时

                var now = DateTime.UtcNow;
                var expires = now.AddMinutes(expiryMinutes);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expires,
                    IssuedAt = now,
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var securityToken = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(securityToken);

                // 验证Token格式和完整性
                var parts = tokenString.Split('.');
                if (parts.Length != 3)
                {
                    _logger.LogError($"JWT Token格式错误: 部分数={parts.Length}, Token={tokenString}");
                    throw new InvalidOperationException("JWT Token格式错误");
                }

                _logger.LogInformation($"生成{(isRefreshToken ? "Refresh" : "Access")}Token: 用户ID={user.Id}, 长度={tokenString.Length}, 部分={parts[0].Length}.{parts[1].Length}.{parts[2].Length}");

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"生成JWT Token失败: 用户ID={user.Id}, isRefresh={isRefreshToken}");
                throw;
            }
        }

        private async Task UpdateLoginInfo(User user)
        {
            user.LastLoginAt = DateTime.Now;
            user.LoginCount++;
            await _context.SaveChangesAsync();
        }

        public async Task<ServiceResponse<UserDto>> UpdateUserProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return ServiceResponse<UserDto>.Error("用户不存在");
            }

            // 更新昵称
            if (!string.IsNullOrEmpty(request.Nickname))
            {
                user.Nickname = request.Nickname.Trim();
            }

            // 更新性别
            if (request.Gender.HasValue)
            {
                user.Gender = request.Gender.Value;
            }

            // 更新手机号
            if (!string.IsNullOrEmpty(request.Phone))
            {
                // 检查手机号是否已被其他用户使用
                var phoneExists = await _context.Users.AnyAsync(u => u.Id != userId && u.Phone == request.Phone);
                if (phoneExists)
                {
                    return ServiceResponse<UserDto>.Error("该手机号已被其他账号使用");
                }
                user.Phone = request.Phone.Trim();
            }

            user.UpdatedAt = DateTime.Now;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var userDto = MapToUserDto(user);
            return ServiceResponse<UserDto>.CreateSuccess(userDto);
        }

        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Nickname = user.Nickname ?? user.Email?.Split('@')[0] ?? "",
                AvatarUrl = user.AvatarUrl,
                Email = user.Email,
                Phone = user.Phone,
                Gender = user.Gender,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                LoginCount = user.LoginCount,
                OpenId = user.OpenId
            };
        }

        public async Task<ServiceResponse<string>> UploadAvatarAsync(int userId, IFormFile file)
        {
            try
            {

                // 验证文件大小（限制为2MB）
                if (file.Length > 2 * 1024 * 1024)
                {
                    return ServiceResponse<string>.Error("头像文件大小不能超过2MB");
                }

                // 验证文件类型
                var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                var contentType = file.ContentType.ToLower();
                if (!allowedContentTypes.Contains(contentType))
                {
                    return ServiceResponse<string>.Error("只支持上传JPEG、PNG或GIF格式的图片");
                }

                // 查找用户
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return ServiceResponse<string>.Error("用户不存在");
                }

                // 删除旧头像（如果存在且是七牛云链接）
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    try
                    {
                        var oldFileName = _qiniuService.ExtractFileNameFromUrl(user.AvatarUrl);
                        if (!string.IsNullOrEmpty(oldFileName))
                        {
                            var deleted = await _qiniuService.DeleteFileAsync(oldFileName);
                            if (deleted)
                            {
                                _logger.LogInformation($"已删除旧头像: {oldFileName}");
                            }
                            else
                            {
                                _logger.LogWarning($"尝试删除旧头像失败（文件可能不存在）: {oldFileName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"删除旧头像失败，但不影响新头像上传: {user.AvatarUrl}");
                        // 不抛出异常，继续上传新头像
                    }
                }

                // 生成文件名：avatars/user_{userId}_{timestamp}.{ext}
                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"avatars/user_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{fileExtension}";

                // 上传到七牛云
                using var stream = file.OpenReadStream();
                var avatarUrl = await _qiniuService.UploadFileAsync(stream, fileName);

                // 更新用户头像URL
                user.AvatarUrl = avatarUrl;
                user.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return ServiceResponse<string>.CreateSuccess(avatarUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理头像上传时发生异常");
                return ServiceResponse<string>.Error("上传头像失败，请重试");
            }
        }


        public async Task<(byte[]? Data, string ContentType)> ProxyAvatarAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (null, string.Empty);
            }

            try
            {
                var qiniuDomain = _configuration["Qiniu:Domain"]?.TrimEnd('/');
                if (!string.IsNullOrEmpty(qiniuDomain))
                {
                    if (!url.StartsWith(qiniuDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("尝试代理非七牛云域名的头像: {Url}", url);
                        return (null, string.Empty);
                    }
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("代理头像请求失败: 状态码={StatusCode}, Url={Url}", response.StatusCode, url);
                    return (null, string.Empty);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var data = await response.Content.ReadAsByteArrayAsync();
                return (data, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理头像请求异常: {Url}", url);
                return (null, string.Empty);
            }
        }


        private class WeChatResponse
        {
            public string? OpenId { get; set; }
            public string? SessionKey { get; set; }
            public int? ErrCode { get; set; }
            public string? ErrMsg { get; set; }
        }


        public async Task<AuthResponse> RefreshToken(RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    _logger.LogWarning("RefreshToken为空");
                    throw new Exception("RefreshToken不能为空");
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key未配置"));

                // 检查Token格式
                var parts = request.RefreshToken.Split('.');
                if (parts.Length != 3)
                {
                    _logger.LogWarning($"RefreshToken格式错误: 部分数={parts.Length}");
                    throw new Exception("RefreshToken格式无效");
                }

                _logger.LogInformation($"验证RefreshToken: 长度={request.RefreshToken.Length}, 部分={parts[0].Length}.{parts[1].Length}.{parts[2].Length}");

                // 验证 Refresh Token
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                SecurityToken validatedToken;
                ClaimsPrincipal principal;

                try
                {
                    principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out validatedToken);
                }
                catch (SecurityTokenExpiredException ex)
                {
                    _logger.LogWarning(ex, "RefreshToken已过期");
                    throw new Exception("Refresh Token 已过期，请重新登录");
                }
                catch (SecurityTokenException ex)
                {
                    _logger.LogError(ex, "RefreshToken验证失败: {Message}", ex.Message);
                    throw new Exception("RefreshToken无效，请重新登录");
                }

                // 验证是否为 Refresh Token
                var tokenTypeClaim = principal.FindFirst("type")?.Value;
                if (tokenTypeClaim != "refresh")
                {
                    _logger.LogWarning($"Token类型错误: {tokenTypeClaim}");
                    throw new Exception("无效的 Refresh Token");
                }

                // 获取用户ID - 优先使用标准claim，回退到自定义claim
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("uid")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning($"用户ID无效: {userIdClaim}");
                    throw new Exception("无效的用户信息");
                }

                // 查找用户
                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.DeletedAt != null)
                {
                    _logger.LogWarning($"用户不存在: {userId}");
                    throw new Exception("用户不存在");
                }

                if (user.Status == "frozen")
                {
                    _logger.LogWarning($"用户账户被冻结: {userId}");
                    throw new Exception("账户已被冻结");
                }

                // 生成新的 Access Token 和 Refresh Token
                var newAccessToken = GenerateJwtToken(user, false);
                var newRefreshToken = GenerateJwtToken(user, true);

                _logger.LogInformation($"用户 {user.Id} Token刷新成功");

                return new AuthResponse
                {
                    Token = newAccessToken,
                    RefreshToken = newRefreshToken,
                    User = MapToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新Token失败: {Message}", ex.Message);
                throw new Exception("刷新Token失败，请重新登录");
            }
        }
    }
}
