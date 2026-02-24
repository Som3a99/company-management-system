using ERP.BLL.Interfaces;

namespace Tests.Infrastructure
{
    internal sealed class PassthroughCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            return Task.FromResult(_store.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix)
        {
            var keys = _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var key in keys)
            {
                _store.Remove(key);
            }

            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateSafeAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl) where T : class
        {
            var existing = await GetAsync<T>(key);
            if (existing is not null)
            {
                return existing;
            }

            var value = await factory();
            await SetAsync(key, value, ttl);
            return value;
        }

        public async Task<T?> GetOrCreateNullableAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl) where T : class
        {
            var existing = await GetAsync<T>(key);
            if (existing is not null)
            {
                return existing;
            }

            var value = await factory();
            if (value is not null)
            {
                await SetAsync(key, value, ttl);
            }

            return value;
        }
    }
}
