using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ERP.PL.Services
{
    /// <summary>
    /// Warm-up for critical shared caches to reduce first-request latency after cold start.
    /// </summary>
    public sealed class CacheWarmupHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CacheWarmupHostedService> _logger;

        public CacheWarmupHostedService(IServiceScopeFactory scopeFactory, ILogger<CacheWarmupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            try
            {
                var departments = await dbContext.Departments
                    .AsNoTracking()
                    .Where(d => !d.IsDeleted)
                    .OrderBy(d => d.DepartmentCode)
                    .ToListAsync(cancellationToken);

                await cacheService.SetAsync(CacheKeys.DepartmentsAll, departments, TimeSpan.FromMinutes(10));

                var lockedAccountsQuery = userManager.Users
                                                     .Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now);

                var lockedAccounts = lockedAccountsQuery.Provider is IAsyncQueryProvider
                    ? await lockedAccountsQuery.CountAsync(cancellationToken)
                    : lockedAccountsQuery.Count();

                var dashboardStats = new ITAdminDashboardStats
                {
                    PendingResets = await dbContext.PasswordResetRequests
                        .CountAsync(r => r.Status == ResetStatus.Pending, cancellationToken),
                    ExpiredResets = await dbContext.PasswordResetRequests
                        .CountAsync(r => r.Status == ResetStatus.Pending && r.ExpiresAt < DateTime.UtcNow, cancellationToken),
                    LockedAccounts = lockedAccounts
                };

                await cacheService.SetAsync(CacheKeys.ItAdminDashboard, dashboardStats, TimeSpan.FromMinutes(2));
                _logger.LogInformation("Cache warm-up completed for departments list and IT admin dashboard stats.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache warm-up failed; application will continue with lazy cache population.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
