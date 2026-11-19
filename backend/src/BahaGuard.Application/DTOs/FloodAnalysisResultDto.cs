namespace BahaGuard.Application.DTOs;

public record PassabilityDto(
    bool Motorcycle,
    bool Sedan,
    bool SuvOrJeepney
);

public record FloodAnalysisResultDto(
    bool IsValidFloodReport,
    string? RejectionReason,
    string NormalizedLocation,
    int EstimatedDepthInches,
    string DepthCategory,
    PassabilityDto Passability,
    decimal ConfidenceScore,
    string CommuterAdvisory
);
