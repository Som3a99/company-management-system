using Microsoft.Extensions.Caching.Memory;

namespace ERP.PL.Services
{
    /// <summary>
    /// Periodically emits cache statistics for centralized telemetry pipelines
    /// (Application Insights / OpenTelemetry / ELK/Grafana ingestion).
    /// </summary>
    public sealed class CacheTelemetryHostedService : BackgroundService
    {
        private readonly ILogger<CacheTelemetryHostedService> _logger;
        private readonly IMemoryCache _memoryCache;

        public CacheTelemetryHostedService(
            ILogger<CacheTelemetryHostedService> logger,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                var stats = (_memoryCache as MemoryCache)?.GetCurrentStatistics();
                if (stats == null)
                {
                    continue;
                }

                var totalLookups = stats.TotalHits + stats.TotalMisses;
                var hitRatio = totalLookups == 0 ? 0 : Math.Round(stats.TotalHits * 100.0 / totalLookups, 2);

                _logger.LogInformation(
                    "CacheTelemetry Hits={Hits} Misses={Misses} HitRatio={HitRatio}% Entries={Entries} EstimatedSize={EstimatedSize}",
                    stats.TotalHits,
                    stats.TotalMisses,
                    hitRatio,
                    stats.CurrentEntryCount,
                    stats.CurrentEstimatedSize);

                if (stats.CurrentEntryCount > 900)
                {
                    _logger.LogWarning(
                        "CacheTelemetryPressure EntryCount={EntryCount} exceeds warning threshold 900/1024.",
                        stats.CurrentEntryCount);
                }
            }
        }
    }
}
