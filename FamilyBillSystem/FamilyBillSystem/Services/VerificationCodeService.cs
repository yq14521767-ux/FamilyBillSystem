using Microsoft.Extensions.Caching.Memory;

namespace FamilyBillSystem.Services
{
    public class VerificationCodeService
    {
        private readonly IMemoryCache _cache;

        public VerificationCodeService(IMemoryCache cache)
        {
            _cache = cache;
        }

        // 生成 6 位数字验证码
        public string GenerateCode()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        // 存储验证码（键：邮箱，值：验证码，有效期 5 分钟）
        public void StoreCode(string email, string code)
        {
            _cache.Set(
                key: $"VerifyCode_{email}",
                value: code,
                absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(5)
            );
        }

        // 验证验证码（返回是否正确）
        public bool VerifyCode(string email, string inputCode)
        {
            // 从缓存获取验证码
            if (_cache.TryGetValue($"VerifyCode_{email}", out string storedCode))
            {
                // 验证通过后删除缓存（防止重复使用）
                if (storedCode == inputCode)
                {
                    _cache.Remove($"VerifyCode_{email}");
                    return true;
                }
            }
            return false;
        }
    }
}
