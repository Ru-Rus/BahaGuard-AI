using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BahaGuard.Infrastructure.Workers;

/// <summary>
/// Background worker that periodically polls public news feeds and official weather/traffic advisories,
/// extracts flood incidents via Gemini 2.5 Flash, merges duplicates spatially, and broadcasts live pins.
/// </summary>
public class NewsAlertIngestionWorker(
    IServiceProvider serviceProvider,
    IOptions<NewsFeedOptions> options,
    ILogger<NewsAlertIngestionWorker> logger) : BackgroundService
{
    private readonly NewsFeedOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoIngestionEnabled)
        {
            logger.LogInformation("NewsAlertIngestionWorker is disabled via configuration.");
            return;
        }

        var interval = TimeSpan.FromMinutes(_options.PollIntervalMinutes > 0 ? _options.PollIntervalMinutes : 10);
        logger.LogInformation("NewsAlertIngestionWorker started. Polling interval: {Interval}", interval);

        // Warm-up initial delay (10 seconds) to allow database connections & SignalR to initialize
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Execute first run immediately after startup
        await RunIngestionCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunIngestionCycleAsync(stoppingToken);
        }
    }

    private async Task RunIngestionCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<INewsAlertIngestionService>();

            var result = await ingestionService.IngestLatestFeedsAsync(stoppingToken);
            logger.LogInformation("Scheduled news alert ingestion cycle finished: {Created} new reports created, {Merged} duplicates corroborated.",
                result.NewReportsCreated, result.DuplicatesMerged);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred during news alert ingestion cycle.");
        }
    }
}
