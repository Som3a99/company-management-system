using ERP.BLL.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Tests.Infrastructure;

namespace Tests.Caching
{
    public class InMemoryCacheServiceTests
    {
        [Fact]
        public async Task GetOrCreateSafeAsync_ShouldExecuteFactoryOnce_ForConcurrentRequests()
        {
            var cache = CreateService();
            var runCount = 0;

            Task<string> Factory()
            {
                Interlocked.Increment(ref runCount);
                return Task.FromResult("value");
            }

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => cache.GetOrCreateSafeAsync("k1", Factory, TimeSpan.FromMinutes(1)))
                .ToArray();

            var values = await Task.WhenAll(tasks);

            values.Should().OnlyContain(v => v == "value");
            runCount.Should().Be(1);
        }

        [Fact]
        public async Task RemoveByPrefixAsync_ShouldRemoveRelatedKeysOnly()
        {
            var cache = CreateService();
            await cache.SetAsync("erp:report:tasks:1", "a", TimeSpan.FromMinutes(1));
            await cache.SetAsync("erp:report:tasks:2", "b", TimeSpan.FromMinutes(1));
            await cache.SetAsync("erp:report:projects:1", "c", TimeSpan.FromMinutes(1));

            await cache.RemoveByPrefixAsync("erp:report:tasks:");

            (await cache.GetAsync<string>("erp:report:tasks:1")).Should().BeNull();
            (await cache.GetAsync<string>("erp:report:tasks:2")).Should().BeNull();
            (await cache.GetAsync<string>("erp:report:projects:1")).Should().Be("c");
        }

        private static InMemoryCacheService CreateService()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
            return new InMemoryCacheService(
                memoryCache,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryCacheService>.Instance,
                TestHttpContextFactory.CreateAccessor());
        }
    }
}
