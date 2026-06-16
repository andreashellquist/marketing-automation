using MarketingAutomation.Modules.Journeys.Application;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketingAutomation.Modules.Journeys.Infrastructure;

/// <summary>
/// Wakes due time-waits. Because runs are tenant-scoped (global query filter), the loop
/// discovers which tenants have due runs by bypassing the filter, then advances each tenant
/// in its own scope with the tenant set. Persisted wake-ups mean a restart simply re-discovers
/// the same due runs — nothing is lost.
/// </summary>
public sealed class JourneyScheduler(IServiceScopeFactory scopeFactory, ILogger<JourneyScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Journey scheduler tick failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;

        List<Guid> tenantIds;
        using (var discoveryScope = scopeFactory.CreateScope())
        {
            var db = discoveryScope.ServiceProvider.GetRequiredService<JourneysDbContext>();
            tenantIds = await db.JourneyRuns
                .IgnoreQueryFilters()
                .Where(r => r.Status == Domain.JourneyRunStatus.Waiting
                            && r.WakeUpAtTicks != null && r.WakeUpAtTicks <= nowTicks
                            && !r.IsDeleted)
                .Select(r => r.TenantId)
                .Distinct()
                .ToListAsync(ct);
        }

        foreach (var tenantId in tenantIds)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AdvanceDueRunsCommand(), ct);
        }
    }
}
