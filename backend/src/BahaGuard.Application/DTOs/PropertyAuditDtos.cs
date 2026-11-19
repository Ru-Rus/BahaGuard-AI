namespace BahaGuard.Application.DTOs;

public record PropertyAuditQuery(
    string? Address,
    double? Latitude,
    double? Longitude,
    double RadiusMeters = 500
);

public record PropertyAuditResponseDto(
    string Address,
    string ResolvedLocationName,
    double Latitude,
    double Longitude,
    int RiskScore,               // 1 to 10
    string RiskLevel,              // "Low", "Moderate", "High", "Extreme"
    int HistoricalIncidentsCount,
    double AverageDepthInches,
    int MaxDepthInches,
    string PredominantDepthCategory,
    double EstimatedRecedingTimeHours,
    VehicleVulnerabilityDto VehicleVulnerability,
    TenantDueDiligenceDto DueDiligenceReport,
    DateTimeOffset AuditedAt
);

public record VehicleVulnerabilityDto(
    bool MotorcycleHighRisk,
    bool SedanHighRisk,
    bool SuvHighRisk,
    string ParkingAdvisory
);

public record TenantDueDiligenceDto(
    string ExecutiveSummary,
    string GroundFloorRiskAdvice,
    string ElectricalAndUtilityRisk,
    string EvacuationAndAccessAdvisory
);

public record ConfirmReportRequestDto(
    bool StillFlooded,
    string? ReporterAlias = null
);

public record ConfirmReportResponseDto(
    Guid ReportId,
    string Status,
    DateTimeOffset ExpiresAt,
    string Message
);
