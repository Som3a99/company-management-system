using ERP.BLL.Reporting.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Services
{
    public class ReportJobWorkerServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorAndExitCleanly_WhenJobProcessingThrowsAndIsCancelled()
        {
            var called = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var jobService = new Mock<IReportJobService>();
            jobService
                .Setup(x => x.ProcessNextPendingJobAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(_ =>
                {
                    called.TrySetResult(true);
                    throw new InvalidOperationException("boom");
                });

            var services = new ServiceCollection();
            services.AddScoped(_ => jobService.Object);
            var provider = services.BuildServiceProvider();

            var logger = new CapturingLogger<TestableReportJobWorkerService>();
            var worker = new TestableReportJobWorkerService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

            using var cts = new CancellationTokenSource();
            var runTask = worker.StartAsync(cts.Token);

            await called.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cts.Cancel();

            await worker.StopAsync(CancellationToken.None);

            logger.Messages.Should().Contain(m => m.Contains("Report job worker error"));
            jobService.Verify(x => x.ProcessNextPendingJobAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        private sealed class TestableReportJobWorkerService : BackgroundService
        {
            private readonly IServiceScopeFactory _scopeFactory;
            private readonly ILogger<TestableReportJobWorkerService> _logger;

            public TestableReportJobWorkerService(IServiceScopeFactory scopeFactory, ILogger<TestableReportJobWorkerService> logger)
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
                        try
                        {
                            await Task.Delay(3000, stoppingToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}