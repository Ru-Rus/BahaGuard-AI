using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface INewsFloodExtractor
{
    Task<ExtractedNewsFloodDto?> ExtractFloodIncidentAsync(NewsAlertItem item, CancellationToken cancellationToken = default);
}
