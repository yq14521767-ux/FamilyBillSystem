using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using FamilyBillSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

            if (user == null || !PasswordHelper.VerifyPW(request.Password, user.PasswordHash, user.PasswordSalt))
                throw new Exception("邮箱或密码错误");

            if (user.Status == "frozen")
                throw new Exception("账户已被冻结");

            await UpdateLoginInfo(user);

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // 关键：保存到数据库
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpireAt = DateTime.UtcNow.AddDays(
                Convert.ToDouble(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7"));
            await _context.SaveChangesAsync();

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
                    Phone = "",
                    LastLoginAt = DateTime.Now,
                    LoginCount = 1
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                await UpdateLoginInfo(user);
            }

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // 保存 RefreshToken
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpireAt = DateTime.UtcNow.AddDays(
                Convert.ToDouble(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7"));
            await _context.SaveChangesAsync();

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
                Phone = "", // 添加空手机号
                OpenId = null, // 添加空OpenId
                Gender = null, // 添加空性别
                LastLoginAt = DateTime.Now, // 设置最后登录时间为当前时间
                LoginCount = 0 // 初始化登录次数为0
            };

            _context.Users.Add(user);
            await UpdateLoginInfo(user);

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpireAt = DateTime.UtcNow.AddDays(
                Convert.ToDouble(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7"));
            await _context.SaveChangesAsync();

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

        // 生成随机 Refresh Token
        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes)
                .TrimEnd('=')           // 去掉 = 填充
                .Replace('+', '-')      // URL 安全
                .Replace('/', '_');
        }

        private string GenerateJwtToken(User user)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key未配置"));

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("type", "access"),  // 固定为 access
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.Nickname ?? ""),
            new Claim("uid", user.Id.ToString())
        };

                var expiryMinutes = Convert.ToDouble(_configuration["Jwt:AccessTokenExpireMinutes"] ?? "120");

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

                // 保留格式验证（可选，但建议保留）
                var parts = tokenString.Split('.');
                if (parts.Length != 3)
                {
                    _logger.LogError($"JWT Token格式错误: 部分数={parts.Length}");
                    throw new InvalidOperationException("JWT Token格式错误");
                }

                _logger.LogInformation($"生成AccessToken: 用户ID={user.Id}, 长度={tokenString.Length}");

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成AccessToken失败");
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
            if (string.IsNullOrEmpty(request.RefreshToken))
                throw new Exception("RefreshToken不能为空");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken &&
                                         u.RefreshTokenExpireAt > DateTime.UtcNow &&
                                         u.DeletedAt == null);

            if (user == null)
                throw new Exception("Refresh Token 无效或已过期，请重新登录");

            if (user.Status == "frozen")
                throw new Exception("账户已被冻结");

            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpireAt = DateTime.UtcNow.AddDays(
                Convert.ToDouble(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7"));

            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                User = MapToUserDto(user)
            };
        }

    }
}
