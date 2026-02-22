using ERP.BLL.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ERP.BLL.Services
{
    public sealed class InMemoryCacheService : ICacheService
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new(StringComparer.Ordinal);

        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
        private readonly ILogger<InMemoryCacheService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public InMemoryCacheService(
            IMemoryCache cache,
            ILogger<InMemoryCacheService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            var found = _cache.TryGetValue(key, out T? value);
            SetCacheStatusHeader(found ? "HIT" : "MISS", key);
            _logger.LogDebug("Cache {Status}: {Key}", found ? "HIT" : "MISS", key);
            return Task.FromResult(found ? value : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
        {
            ArgumentNullException.ThrowIfNull(value);

            _keys[key] = 1;
            _cache.Set(key, value, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1,
                Priority = CacheItemPriority.High,
                PostEvictionCallbacks =
                {
                    new PostEvictionCallbackRegistration
                    {
                        EvictionCallback = static (evictedKey, _, reason, state) =>
                        {
                            if (state is ConcurrentDictionary<string, byte> registry && evictedKey is string k)
                            {
                                registry.TryRemove(k, out byte _);
                            }
                        },
                        State = _keys
                    }
                }
            });

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix)
        {
            var matchingKeys = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();

            foreach (var key in matchingKeys)
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }

            _logger.LogDebug("Cache prefix invalidation completed for {Prefix}. Removed {Count} entries.", prefix, matchingKeys.Count);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateSafeAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached is not null)
            {
                return cached;
            }

            var keyLock = KeyLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync();
            try
            {
                cached = await GetAsync<T>(key);
                if (cached is not null)
                {
                    return cached;
                }

                var value = await factory();
                await SetAsync(key, value, ttl);
                SetCacheStatusHeader("MISS", key);
                _logger.LogInformation("Cache MISS (created): {Key}", key);
                return value;
            }
            finally
            {
                keyLock.Release();
            }
        }

        private void SetCacheStatusHeader(string status, string key)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return;
            }

            context.Response.Headers["X-Cache-Status"] = status;
            context.Response.Headers["X-Cache-Key"] = key;
        }

        public async Task<T?> GetOrCreateNullableAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached is not null)
            {
                return cached;
            }

            var keyLock = KeyLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync();
            try
            {
                cached = await GetAsync<T>(key);
                if (cached is not null)
                {
                    return cached;
                }

                var value = await factory();
                if (value is not null)
                {
                    await SetAsync(key, value, ttl);
                }
                else
                {
                    _logger.LogWarning("Factory for key {Key} returned null. Not caching.", key);
                }
                SetCacheStatusHeader("MISS", key);
                _logger.LogInformation("Cache MISS (created): {Key}", key);
                return value;
            }
            finally
            {
                keyLock.Release();
            }
        }
    }
}
