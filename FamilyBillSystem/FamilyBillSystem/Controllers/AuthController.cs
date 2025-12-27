using FamilyBillSystem.DTOs;
using FamilyBillSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;

namespace FamilyBillSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly EmailService _emailService;
        private readonly VerificationCodeService _verificationCodeService;
        private readonly ILogger<AuthController> _logger;
        private readonly IWebHostEnvironment _env;

        public AuthController(AuthService authService, EmailService emailService,
            VerificationCodeService verificationCodeService, ILogger<AuthController> logger, IWebHostEnvironment env)
        {
            _authService = authService;
            _emailService = emailService;
            _verificationCodeService = verificationCodeService;
            _logger = logger;
            _env = env;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.Login(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮箱登录失败");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("wechat-login")]
        public async Task<IActionResult> WeChatLogin([FromBody] WeChatLoginRequest request)
        {
            try
            {
                var response = await _authService.WeChatLogin(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信登录失败");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _authService.Register(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendCodeRequest request)
        {
            try
            {
                var existingUser = await _authService.CheckEmailExists(request.Email);
                if (existingUser)
                    return BadRequest(new { message = "该邮箱已被注册" });

                // 生成验证码
                var code = _verificationCodeService.GenerateCode();

                // 存储验证码到缓存
                _verificationCodeService.StoreCode(request.Email, code);

                // 发送邮件
                await _emailService.SendVerificationCodeAsync(request.Email, code);

                return Ok(new { message = "验证码已发送，请查收邮件" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"发送验证码失败: {request.Email}");
                return BadRequest(new { message = "发送验证码失败：" + ex.Message });
            }
        }

        [HttpPost("upload-avatar")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, code = 400, message = "未选择文件" });
            }

            try
            {
                // 获取当前用户ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, code = 401, message = "用户未认证" });
                }



                // 调用服务层方法保存头像到数据库
                var result = await _authService.UploadAvatarAsync(userId, file);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, code = 400, message = result.Message });
                }

                return Ok(new
                {
                    success = true,
                    code = 200,
                    message = "头像上传成功",
                    data = result.Data // 返回头像URL
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传头像失败");
                return StatusCode(500, new { success = false, code = 500, message = "上传失败，请重试" });
            }
        }


        [HttpGet("avatar-proxy")]
        public async Task<IActionResult> ProxyAvatar([FromQuery] string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return BadRequest("url不能为空");
                }

                var (data, contentType) = await _authService.ProxyAvatarAsync(url);
                if (data == null || data.Length == 0)
                {
                    return NotFound();
                }

                return File(data, string.IsNullOrEmpty(contentType) ? "image/jpeg" : contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理头像失败: {Url}", url);
                return StatusCode(500, new { message = "获取头像失败" });
            }
        }


        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "用户未认证" });
                }

                var userId = int.Parse(userIdClaim);
                var result = await _authService.UpdateUserProfileAsync(userId, request);

                if (result == null || !result.Success)
                {
                    return BadRequest(new { message = result?.Message ?? "更新个人资料时发生错误" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [Authorize]
        [HttpPost("validate-token")]
        public IActionResult ValidateToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                return Ok(new
                {
                    isValid = true,
                    userId,
                    username
                    // 可以添加更多用户信息
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证 Token 时发生错误");
                return Unauthorized(new { message = "Token 验证失败" });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var response = await _authService.RefreshToken(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 时发生错误");
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { success = true, message = "退出成功" });
        }
    }
}
