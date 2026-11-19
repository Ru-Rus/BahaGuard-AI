using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using BahaGuard.Domain.Entities;
using BahaGuard.Infrastructure.Data;
using BahaGuard.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace BahaGuard.Infrastructure.Services;

/// <summary>
/// Core Ingestion & Deduplication Service for Automated News & Official Weather/Traffic Feeds.
/// Solves crowdsourcing cold-start by converting public news and bulletins into verified map incidents.
/// Merges duplicate alerts within 250m to reinforce confidence scores instead of cluttering the live map.
/// </summary>
public class NewsAlertIngestionService : INewsAlertIngestionService
{
    private static readonly GeometryFactory Srid4326Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly BahaGuardDbContext _dbContext;
    private readonly INewsFeedReader _feedReader;
    private readonly INewsFloodExtractor _floodExtractor;
    private readonly IFloodAlertNotifier? _alertNotifier;
    private readonly NewsFeedOptions _options;
    private readonly ILogger<NewsAlertIngestionService> _logger;

    public NewsAlertIngestionService(
        BahaGuardDbContext dbContext,
        INewsFeedReader feedReader,
        INewsFloodExtractor floodExtractor,
        IOptions<NewsFeedOptions> options,
        ILogger<NewsAlertIngestionService> logger,
        IFloodAlertNotifier? alertNotifier = null)
    {
        _dbContext = dbContext;
        _feedReader = feedReader;
        _floodExtractor = floodExtractor;
        _alertNotifier = alertNotifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NewsIngestionResultDto> IngestLatestFeedsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting automated news & official alert ingestion cycle...");

        var alertItems = await _feedReader.FetchLatestAlertsAsync(cancellationToken);
        int totalPolled = alertItems.Count;
        int detected = 0;
        int createdCount = 0;
        int mergedCount = 0;
        int skippedCount = 0;

        var createdList = new List<FloodReportResponseDto>();
        var mergedList = new List<FloodReportResponseDto>();

        foreach (var item in alertItems)
        {
            var extracted = await _floodExtractor.ExtractFloodIncidentAsync(item, cancellationToken);
            if (extracted == null || !extracted.IsFloodIncident)
            {
                skippedCount++;
                continue;
            }

            detected++;

            var (reportDto, isMerged) = await SaveOrMergeIncidentAsync(extracted, cancellationToken);

            if (isMerged)
            {
                mergedCount++;
                mergedList.Add(reportDto);
            }
            else
            {
                createdCount++;
                createdList.Add(reportDto);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("News Ingestion Complete: Polled={Total}, Detected={Detected}, Created={Created}, Merged={Merged}, Skipped={Skipped}",
            totalPolled, detected, createdCount, mergedCount, skippedCount);

        return new NewsIngestionResultDto(
            TotalItemsPolled: totalPolled,
            FloodIncidentsDetected: detected,
            NewReportsCreated: createdCount,
            DuplicatesMerged: mergedCount,
            ItemsSkippedNonFlood: skippedCount,
            CreatedReports: createdList,
            MergedReports: mergedList,
            ProcessedAt: DateTimeOffset.UtcNow
        );
    }

    public async Task<FloodReportResponseDto?> IngestDirectAlertAsync(
        IngestAlertRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var item = new NewsAlertItem(
            Title: request.RawText,
            Description: request.RawText,
            Source: string.IsNullOrWhiteSpace(request.Source) ? "Official Emergency Advisory" : request.Source.Trim(),
            PublishedAt: DateTimeOffset.UtcNow,
            Link: request.SourceUrl
        );

        var extracted = await _floodExtractor.ExtractFloodIncidentAsync(item, cancellationToken);
        if (extracted == null || !extracted.IsFloodIncident)
        {
            // If fallback coordinates provided, create incident even if NLP didn't find specific landmark
            if (request.FallbackLatitude.HasValue && request.FallbackLongitude.HasValue)
            {
                extracted = new ExtractedNewsFloodDto(
                    IsFloodIncident: true,
                    LocationName: "Reported Flood Zone",
                    City: "Manila",
                    Barangay: null,
                    Latitude: request.FallbackLatitude.Value,
                    Longitude: request.FallbackLongitude.Value,
                    EstimatedDepthInches: 10,
                    DepthCategory: "HalfTire",
                    PassableMotorcycle: false,
                    PassableSedan: true,
                    PassableSuvJeepney: true,
                    ConfidenceScore: 0.90m,
                    Source: item.Source,
                    Summary: request.RawText
                );
            }
            else
            {
                return null;
            }
        }

        var (reportDto, _) = await SaveOrMergeIncidentAsync(extracted, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return reportDto;
    }

    public async Task<IReadOnlyList<FloodReportResponseDto>> GetRecentOfficialAlertsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var reports = await _dbContext.FloodReports
            .AsNoTracking()
            .Where(r => r.Status == "Active" && r.ExpiresAt > now)
            .Where(r => r.ReporterAlias != null && (
                r.ReporterAlias.Contains("MMDA") ||
                r.ReporterAlias.Contains("Official") ||
                r.ReporterAlias.Contains("PAGASA") ||
                r.ReporterAlias.Contains("News") ||
                r.ReporterAlias.Contains("Advisory") ||
                r.ReporterAlias.Contains("Bulletin") ||
                r.ReporterAlias.Contains("Disaster")))
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return reports.Select(r => MapToDto(r, null)).ToList();
    }

    private async Task<(FloodReportResponseDto Report, bool IsMerged)> SaveOrMergeIncidentAsync(
        ExtractedNewsFloodDto incident,
        CancellationToken cancellationToken)
    {
        var deduplicationRadius = _options.DeduplicationRadiusMeters > 0 ? _options.DeduplicationRadiusMeters : 250.0;
        var now = DateTimeOffset.UtcNow;

        // Bounding box pre-filter (~0.0035 degrees is ~380m around Manila)
        var deltaDeg = (deduplicationRadius / 111000.0) * 1.5;
        var minLat = incident.Latitude - deltaDeg;
        var maxLat = incident.Latitude + deltaDeg;
        var minLng = incident.Longitude - deltaDeg;
        var maxLng = incident.Longitude + deltaDeg;

        // Query active candidate reports within bounding box
        var candidates = await _dbContext.FloodReports
            .Where(r => r.Status == "Active" && r.ExpiresAt > now)
            .Where(r => r.Coordinates.Y >= minLat && r.Coordinates.Y <= maxLat &&
                        r.Coordinates.X >= minLng && r.Coordinates.X <= maxLng)
            .ToListAsync(cancellationToken);

        // Precise Haversine distance in meters (works identically on SQL Server & In-Memory unit tests)
        var matched = candidates
            .Select(r => new
            {
                Report = r,
                DistanceMeters = CalculateHaversineDistanceMeters(incident.Latitude, incident.Longitude, r.Coordinates.Y, r.Coordinates.X)
            })
            .Where(x => x.DistanceMeters <= deduplicationRadius)
            .OrderBy(x => x.DistanceMeters)
            .FirstOrDefault();

        if (matched != null)
        {
            var existingReport = matched.Report;
            // --- DUPLICATE MERGING & CORROBORATION ---
            _logger.LogInformation("Merging news alert '{Source}' into existing active report {ReportId} (Distance: {Distance:F1}m)",
                incident.Source, existingReport.Id, matched.DistanceMeters);

            // Extend expiration to full 3 hours from now
            existingReport.ExpiresAt = now.AddHours(3);

            // Reinforce AI confidence score up to 0.98
            existingReport.AiConfidenceScore = Math.Min(0.98m, existingReport.AiConfidenceScore + 0.15m);

            // If official news reports deeper water, upgrade severity and passability
            if (incident.EstimatedDepthInches > existingReport.EstimatedDepthInches)
            {
                existingReport.EstimatedDepthInches = incident.EstimatedDepthInches;
                existingReport.DepthCategory = incident.DepthCategory;
                existingReport.PassableMotorcycle = incident.PassableMotorcycle;
                existingReport.PassableSedan = incident.PassableSedan;
                existingReport.PassableSuvJeepney = incident.PassableSuvJeepney;
            }

            // Append corroboration note
            if (!existingReport.RawDescription.Contains(incident.Source))
            {
                existingReport.RawDescription = $"{existingReport.RawDescription}\n[Corroborated by {incident.Source}: {incident.Summary}]";
                if (existingReport.RawDescription.Length > 500)
                {
                    existingReport.RawDescription = existingReport.RawDescription[..500];
                }
            }

            var mergedDto = MapToDto(existingReport, matched.DistanceMeters);

            if (_alertNotifier != null)
            {
                await _alertNotifier.BroadcastNewReportAsync(mergedDto, cancellationToken);
            }

            return (mergedDto, true);
        }

        // --- NEW INCIDENT CREATION ---
        var newReport = FloodReport.Create(
            rawDescription: incident.Summary.Length > 500 ? incident.Summary[..500] : incident.Summary,
            extractedLocationName: incident.LocationName,
            latitude: incident.Latitude,
            longitude: incident.Longitude,
            estimatedDepthInches: incident.EstimatedDepthInches,
            depthCategory: incident.DepthCategory,
            passableMotorcycle: incident.PassableMotorcycle,
            passableSedan: incident.PassableSedan,
            passableSuvJeepney: incident.PassableSuvJeepney,
            aiConfidenceScore: incident.ConfidenceScore,
            reporterAlias: incident.Source,
            barangay: incident.Barangay,
            city: incident.City,
            imageUrl: null,
            lifetime: TimeSpan.FromHours(3)
        );

        _dbContext.FloodReports.Add(newReport);
        var createdDto = MapToDto(newReport, 0);

        if (_alertNotifier != null)
        {
            await _alertNotifier.BroadcastNewReportAsync(createdDto, cancellationToken);
        }

        return (createdDto, false);
    }

    private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // Earth radius in meters
        var dLat = (lat2 - lat1) * (Math.PI / 180.0);
        var dLon = (lon2 - lon1) * (Math.PI / 180.0);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * (Math.PI / 180.0)) * Math.Cos(lat2 * (Math.PI / 180.0)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static FloodReportResponseDto MapToDto(FloodReport report, double? distanceMeters)
    {
        return new FloodReportResponseDto(
            Id: report.Id,
            ReporterAlias: report.ReporterAlias,
            RawDescription: report.RawDescription,
            ExtractedLocationName: report.ExtractedLocationName,
            Barangay: report.Barangay,
            City: report.City,
            Latitude: report.Coordinates.Y,
            Longitude: report.Coordinates.X,
            EstimatedDepthInches: report.EstimatedDepthInches,
            DepthCategory: report.DepthCategory,
            PassableMotorcycle: report.PassableMotorcycle,
            PassableSedan: report.PassableSedan,
            PassableSuvJeepney: report.PassableSuvJeepney,
            AiConfidenceScore: report.AiConfidenceScore,
            ImageUrl: report.ImageUrl,
            Status: report.Status,
            CreatedAt: report.CreatedAt,
            ExpiresAt: report.ExpiresAt,
            DistanceMeters: distanceMeters
        );
    }
}
