using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FamilyBillSystem.Middleware
{
    public class CustomJwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public CustomJwtMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var token = ExtractTokenFromHeader(context);
            
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var principal = ValidateToken(token);
                    if (principal != null)
                    {
                        context.User = principal;
                        Console.WriteLine($"[自定义JWT] 认证成功: 用户ID={principal.FindFirst(ClaimTypes.NameIdentifier)?.Value}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[自定义JWT] 认证失败: {ex.Message}");
                }
            }

            await _next(context);
        }

        private string? ExtractTokenFromHeader(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring(7);
            }
            return null;
        }

        private ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key未配置");
                var key = Encoding.UTF8.GetBytes(jwtKey);

                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[自定义JWT] Token验证异常: {ex.Message}");
                return null;
            }
        }
    }
}