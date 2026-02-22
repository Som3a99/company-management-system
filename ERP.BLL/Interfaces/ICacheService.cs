namespace ERP.BLL.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPrefixAsync(string prefix);
        Task<T> GetOrCreateSafeAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl) where T : class;
        Task<T?> GetOrCreateNullableAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl) where T : class;

    }
}
