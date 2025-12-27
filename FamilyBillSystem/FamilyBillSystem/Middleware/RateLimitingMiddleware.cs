using System.Collections.Concurrent;
using System.Net;

namespace FamilyBillSystem.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private static readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
        private static readonly object _cleanupLock = new();
        private static DateTime _lastCleanup = DateTime.UtcNow;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.Request.Path.Value?.ToLower();
            
            // 只对敏感端点进行速率限制
            if (IsSensitiveEndpoint(endpoint))
            {
                var clientId = GetClientIdentifier(context);
                
                if (!IsRequestAllowed(clientId, endpoint))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    await context.Response.WriteAsync("{\"message\":\"请求过于频繁，请稍后再试\"}");
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsSensitiveEndpoint(string? endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return false;

            var sensitiveEndpoints = new[]
            {
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/send-verification-code",
                "/api/auth/reset-password"
            };

            return sensitiveEndpoints.Any(se => endpoint.StartsWith(se));
        }

        private static string GetClientIdentifier(HttpContext context)
        {
            // 优先使用用户ID，其次使用IP地址
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip:{ip}";
        }

        private static bool IsRequestAllowed(string clientId, string endpoint)
        {
            var now = DateTime.UtcNow;
            var windowMinutes = GetRateLimitWindow(endpoint);
            var maxRequests = GetMaxRequests(endpoint);

            // 清理过期记录
            CleanupExpiredRequests();

            // 获取或创建客户端请求记录
            var requests = _requests.GetOrAdd(clientId, _ => new List<DateTime>());

            lock (requests)
            {
                // 移除窗口期外的请求
                requests.RemoveAll(r => r < now.AddMinutes(-windowMinutes));

                // 检查是否超过限制
                if (requests.Count >= maxRequests)
                {
                    return false;
                }

                // 记录当前请求
                requests.Add(now);
                return true;
            }
        }

        private static int GetRateLimitWindow(string endpoint)
        {
            // 根据端点返回不同的时间窗口（分钟）
            return endpoint switch
            {
                var e when e.Contains("login") => 15,
                var e when e.Contains("register") => 60,
                var e when e.Contains("send-verification-code") => 5,
                var e when e.Contains("reset-password") => 30,
                _ => 10
            };
        }

        private static int GetMaxRequests(string endpoint)
        {
            // 根据端点返回不同的最大请求数
            return endpoint switch
            {
                var e when e.Contains("login") => 5,
                var e when e.Contains("register") => 3,
                var e when e.Contains("send-verification-code") => 3,
                var e when e.Contains("reset-password") => 2,
                _ => 10
            };
        }

        private static void CleanupExpiredRequests()
        {
            lock (_cleanupLock)
            {
                // 每小时清理一次
                if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromHours(1))
                    return;

                var cutoff = DateTime.UtcNow.AddHours(-2);
                var keysToRemove = new List<string>();

                foreach (var kvp in _requests)
                {
                    lock (kvp.Value)
                    {
                        kvp.Value.RemoveAll(r => r < cutoff);
                        if (kvp.Value.Count == 0)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _requests.TryRemove(key, out _);
                }

                _lastCleanup = DateTime.UtcNow;
            }
        }
    }
}