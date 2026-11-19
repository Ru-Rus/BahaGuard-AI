using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface IFloodReportService
{
    Task<FloodReportResponseDto> CreateReportAsync(
        CreateFloodReportDto dto,
        Stream? imageStream = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FloodReportResponseDto>> GetNearbyReportsAsync(
        NearbyReportsQuery query,
        CancellationToken cancellationToken = default);

    Task<FloodReportResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
