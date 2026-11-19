namespace BahaGuard.Application.DTOs;

/// <summary>
/// Represents a raw article, bulletin, or advisory item retrieved from a public RSS/Atom news feed or alert webhook.
/// </summary>
public record NewsAlertItem(
    string Title,
    string Description,
    string Source,
    DateTimeOffset PublishedAt,
    string? Link = null
);

/// <summary>
/// Direct ingestion request for manual or webhook-driven disaster alerts.
/// </summary>
public record IngestAlertRequestDto(
    string RawText,
    string? Source = "Official Emergency Advisory",
    string? SourceUrl = null,
    double? FallbackLatitude = null,
    double? FallbackLongitude = null
);

/// <summary>
/// Incident data extracted from a news article or official bulletin by Gemini 2.5 Flash.
/// </summary>
public record ExtractedNewsFloodDto(
    bool IsFloodIncident,
    string LocationName,
    string City,
    string? Barangay,
    double Latitude,
    double Longitude,
    int EstimatedDepthInches,
    string DepthCategory,
    bool PassableMotorcycle,
    bool PassableSedan,
    bool PassableSuvJeepney,
    decimal ConfidenceScore,
    string Source,
    string Summary
);

/// <summary>
/// Summary response for an automated or manual news feed ingestion execution.
/// </summary>
public record NewsIngestionResultDto(
    int TotalItemsPolled,
    int FloodIncidentsDetected,
    int NewReportsCreated,
    int DuplicatesMerged,
    int ItemsSkippedNonFlood,
    IReadOnlyList<FloodReportResponseDto> CreatedReports,
    IReadOnlyList<FloodReportResponseDto> MergedReports,
    DateTimeOffset ProcessedAt
);
