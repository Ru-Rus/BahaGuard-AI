using BahaGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BahaGuard.Infrastructure.Workers;

/// <summary>
/// Background worker that periodically checks for expired flood reports
/// and marks them as 'Receding' or 'Resolved'.
/// </summary>
public class ReportExpirationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReportExpirationWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);

    public ReportExpirationWorker(
        IServiceProvider serviceProvider,
        ILogger<ReportExpirationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportExpirationWorker is running.");

        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
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
                    _logger.LogInformation("Auto-resolved {Count} expired flood reports.", expiredReports.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing ReportExpirationWorker.");
            }
        }

        _logger.LogInformation("ReportExpirationWorker is stopping.");
    }
}
