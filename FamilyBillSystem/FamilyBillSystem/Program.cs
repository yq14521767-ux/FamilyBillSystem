using FamilyBillSystem.Data;
using FamilyBillSystem.Services;
using FamilyBillSystem.Utils;
using FamilyBillSystem.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Microsoft.OpenApi.Models;

namespace FamilyBillSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ---------------- 日志配置 ----------------
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            // ---------------- Mysql 配置  ----------------
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            );

            // 使用自定义JWT中间件
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            // ---------------- 邮件服务 ----------------
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
            builder.Services.AddSingleton<EmailService>();
            
            // ---------------- 验证码服务 ----------------
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<VerificationCodeService>();

            // ---------------- 七牛云服务 ----------------
            builder.Services.Configure<QiniuSettings>(builder.Configuration.GetSection("Qiniu"));
            builder.Services.AddScoped<QiniuService>();

            // ---------------- AuthService ----------------
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddHttpClient<AuthService>();
            
            // ---------------- BudgetService ----------------
            builder.Services.AddScoped<IBudgetService, BudgetService>();

            // ---------------- CORS ----------------
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // ---------------- Controllers & Swagger ----------------
            builder.Services.AddControllers();
            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.MapType<IFormFile>(() => new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                });
            });


            var app = builder.Build();

            // ---------------- HTTP 管道 ----------------
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            
            // Token验证中间件
            app.Use(async (context, next) =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var tokenPart = authHeader.Substring(7);
                    var parts = tokenPart.Split('.');
                    if (parts.Length != 3)
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("{\"message\":\"Invalid token format\"}");
                        return;
                    }
                }
                await next();
            });
            
            // 使用自定义JWT中间件
            app.UseMiddleware<CustomJwtMiddleware>();
            
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // 初始化种子数据
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SeedData.Initialize(context);
            }

            app.Run();
        }
    }
}
