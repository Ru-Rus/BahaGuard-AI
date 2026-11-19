using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BahaGuard.Infrastructure.Workers;

/// <summary>
/// Background worker that periodically checks for expired flood reports,
/// marks them as 'Resolved', and broadcasts live status changes via SignalR.
/// </summary>
public class ReportExpirationWorker(
    IServiceProvider serviceProvider,
    ILogger<ReportExpirationWorker> logger) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReportExpirationWorker is running (check interval: {Interval}).", _checkInterval);

        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BahaGuardDbContext>();

                var now = DateTimeOffset.UtcNow;
                var expiredReports = await dbContext.FloodReports
                    .Where(r => r.Status == "Active" && r.ExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredReports.Count > 0)
                {
                    foreach (var report in expiredReports)
                    {
                        report.Status = "Resolved";
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Auto-resolved {Count} expired flood reports.", expiredReports.Count);

                    var notifier = scope.ServiceProvider.GetService<IFloodAlertNotifier>();
                    if (notifier != null)
                    {
                        foreach (var report in expiredReports)
                        {
                            await notifier.BroadcastReportStatusUpdateAsync(report.Id, report.Status, stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while executing ReportExpirationWorker.");
            }
        }
    }
}
