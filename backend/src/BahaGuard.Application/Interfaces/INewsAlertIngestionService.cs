using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface INewsAlertIngestionService
{
    Task<NewsIngestionResultDto> IngestLatestFeedsAsync(CancellationToken cancellationToken = default);
    Task<FloodReportResponseDto?> IngestDirectAlertAsync(IngestAlertRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FloodReportResponseDto>> GetRecentOfficialAlertsAsync(int limit = 20, CancellationToken cancellationToken = default);
}
