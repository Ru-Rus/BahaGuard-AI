using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface IGeminiVisionService
{
    Task<FloodAnalysisResultDto> AnalyzeIncidentAsync(
        string rawDescription,
        Stream? imageStream = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default);
}
