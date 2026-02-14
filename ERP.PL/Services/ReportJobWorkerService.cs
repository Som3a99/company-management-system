using ERP.BLL.Reporting.Interfaces;

namespace ERP.PL.Services
{
    public sealed class ReportJobWorkerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReportJobWorkerService> _logger;

        public ReportJobWorkerService(IServiceScopeFactory scopeFactory, ILogger<ReportJobWorkerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IReportJobService>();
                    var processed = await service.ProcessNextPendingJobAsync(stoppingToken);
                    await Task.Delay(processed ? 500 : 2000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Report job worker error");
                    await Task.Delay(3000, stoppingToken);
                }
            }
        }
    }
}
