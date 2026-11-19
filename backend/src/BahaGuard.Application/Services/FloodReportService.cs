using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using BahaGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace BahaGuard.Application.Services;

public class FloodReportService(
    IBahaGuardDbContext dbContext,
    IGeminiVisionService geminiVisionService,
    ILogger<FloodReportService> logger) : IFloodReportService
{
    private static readonly GeometryFactory Srid4326GeometryFactory = 
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<FloodReportResponseDto> CreateReportAsync(
        CreateFloodReportDto dto,
        Stream? imageStream = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing incident report submission at ({Lat}, {Lng})", dto.Latitude, dto.Longitude);

        // 1. Analyze incident with Gemini (Mock in Phase 1, real multimodal in Phase 2)
        var analysis = await geminiVisionService.AnalyzeIncidentAsync(
            dto.RawDescription,
            imageStream,
            mimeType,
            cancellationToken);

        if (!analysis.IsValidFloodReport)
        {
            throw new ArgumentException(
                analysis.RejectionReason ?? "Incident was rejected as non-flood or spam content.");
        }

        // 2. Create Domain Entity
        var report = FloodReport.Create(
            rawDescription: dto.RawDescription,
            extractedLocationName: analysis.NormalizedLocation,
            latitude: dto.Latitude,
            longitude: dto.Longitude,
            estimatedDepthInches: analysis.EstimatedDepthInches,
            depthCategory: analysis.DepthCategory,
            passableMotorcycle: analysis.Passability.Motorcycle,
            passableSedan: analysis.Passability.Sedan,
            passableSuvJeepney: analysis.Passability.SuvOrJeepney,
            aiConfidenceScore: analysis.ConfidenceScore,
            reporterAlias: dto.ReporterAlias,
            barangay: dto.Barangay,
            city: dto.City,
            imageUrl: dto.ImageUrl,
            lifetime: TimeSpan.FromHours(3)
        );

        dbContext.FloodReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Flood report {ReportId} successfully created for location '{Location}'",
            report.Id, report.ExtractedLocationName);

        return MapToDto(report, distanceMeters: 0);
    }

    public async Task<IReadOnlyList<FloodReportResponseDto>> GetNearbyReportsAsync(
        NearbyReportsQuery query,
        CancellationToken cancellationToken = default)
    {
        // NetTopologySuite: X is Longitude, Y is Latitude
        var commuterPoint = Srid4326GeometryFactory.CreatePoint(new Coordinate(query.Longitude, query.Latitude));
        var now = DateTimeOffset.UtcNow;

        var baseQuery = dbContext.FloodReports
            .AsNoTracking()
            .Where(r => r.Status == "Active" && r.ExpiresAt > now)
            .Where(r => r.Coordinates.Distance(commuterPoint) <= query.RadiusMeters);

        // Optional vehicle filter
        if (!string.IsNullOrWhiteSpace(query.VehicleType))
        {
            var normalizedVehicle = query.VehicleType.Trim().ToLowerInvariant();
            baseQuery = normalizedVehicle switch
            {
                "moto" or "motorcycle" => baseQuery.Where(r => r.PassableMotorcycle),
                "sedan" => baseQuery.Where(r => r.PassableSedan),
                "suv" or "jeepney" => baseQuery.Where(r => r.PassableSuvJeepney),
                _ => baseQuery
            };
        }

        // Query spatial distance using STDistance and order by distance
        var rawResults = await baseQuery
            .OrderBy(r => r.Coordinates.Distance(commuterPoint))
            .Select(r => new
            {
                Report = r,
                DistanceMeters = r.Coordinates.Distance(commuterPoint)
            })
            .ToListAsync(cancellationToken);

        return rawResults
            .Select(x => MapToDto(x.Report, Math.Round(x.DistanceMeters, 1)))
            .ToList();
    }

    public async Task<FloodReportResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.FloodReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report == null ? null : MapToDto(report, distanceMeters: null);
    }

    public async Task<ConfirmReportResponseDto> ConfirmReportAsync(
        Guid id,
        ConfirmReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.FloodReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (report == null)
        {
            throw new KeyNotFoundException($"Flood report with ID '{id}' was not found.");
        }

        string message;
        var now = DateTimeOffset.UtcNow;

        if (request.StillFlooded)
        {
            // Extend validity by 1 hour (capped at max 6 hours from original creation)
            var maxExpiry = report.CreatedAt.AddHours(6);
            var newExpiry = report.ExpiresAt.AddHours(1);

            report.ExpiresAt = newExpiry > maxExpiry ? maxExpiry : newExpiry;
            if (report.Status != "Active")
            {
                report.Status = "Active";
            }
            message = $"Flood still active. Validity extended until {report.ExpiresAt:HH:mm} UTC.";
        }
        else
        {
            // Mark as receding and shorten lifespan
            report.Status = "Receding";
            report.ExpiresAt = now.AddMinutes(20);
            message = "Flood subsidence confirmed. Status updated to Receding.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Report {ReportId} confirmed (StillFlooded={StillFlooded}). New status={Status}, expires={ExpiresAt}",
            id, request.StillFlooded, report.Status, report.ExpiresAt);

        return new ConfirmReportResponseDto(
            ReportId: report.Id,
            Status: report.Status,
            ExpiresAt: report.ExpiresAt,
            Message: message
        );
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
