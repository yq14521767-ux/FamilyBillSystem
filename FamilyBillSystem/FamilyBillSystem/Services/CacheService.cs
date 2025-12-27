using Microsoft.Extensions.Caching.Memory;
using System.Collections;

namespace FamilyBillSystem.Services
{
    public class CacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public T? Get<T>(string key)
        {
            try
            {
                return _cache.Get<T>(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache key: {Key}", key);
                return default;
            }
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var options = new MemoryCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration;
                }
                else
                {
                    // 默认缓存5分钟
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                }

                // 设置缓存优先级
                options.Priority = CacheItemPriority.Normal;

                _cache.Set(key, value, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key: {Key}", key);
            }
        }

        public void Remove(string key)
        {
            try
            {
                _cache.Remove(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key: {Key}", key);
            }
        }

        public void RemoveByPattern(string pattern)
        {
            try
            {
                // 注意：IMemoryCache 不直接支持按模式删除
                // 这里提供一个基础实现，实际项目中可能需要使用 Redis 等支持模式匹配的缓存
                if (_cache is MemoryCache memoryCache)
                {
                    var field = typeof(MemoryCache).GetField("_coherentState", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (field?.GetValue(memoryCache) is object coherentState)
                    {
                        var entriesCollection = coherentState.GetType()
                            .GetProperty("EntriesCollection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (entriesCollection?.GetValue(coherentState) is IDictionary entries)
                        {
                            var keysToRemove = new List<object>();
                            foreach (DictionaryEntry entry in entries)
                            {
                                if (entry.Key.ToString()?.Contains(pattern) == true)
                                {
                                    keysToRemove.Add(entry.Key);
                                }
                            }

                            foreach (var key in keysToRemove)
                            {
                                _cache.Remove(key);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
            }
        }

        public string GenerateKey(params object[] keyParts)
        {
            return string.Join(":", keyParts.Select(p => p?.ToString() ?? "null"));
        }

        public string GenerateUserKey(int userId, params object[] keyParts)
        {
            var allParts = new object[] { "user", userId }.Concat(keyParts);
            return GenerateKey(allParts.ToArray());
        }

        public string GenerateFamilyKey(int familyId, params object[] keyParts)
        {
            var allParts = new object[] { "family", familyId }.Concat(keyParts);
            return GenerateKey(allParts.ToArray());
        }
    }
}