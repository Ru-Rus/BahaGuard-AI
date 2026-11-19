namespace BahaGuard.Application.DTOs;

public record FloodReportResponseDto(
    Guid Id,
    string? ReporterAlias,
    string RawDescription,
    string ExtractedLocationName,
    string? Barangay,
    string City,
    double Latitude,
    double Longitude,
    int EstimatedDepthInches,
    string DepthCategory,
    bool PassableMotorcycle,
    bool PassableSedan,
    bool PassableSuvJeepney,
    decimal AiConfidenceScore,
    string? ImageUrl,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    double? DistanceMeters = null
);
