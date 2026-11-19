using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface IFloodAlertNotifier
{
    Task BroadcastNewReportAsync(FloodReportResponseDto report, CancellationToken cancellationToken = default);
    Task BroadcastReportStatusUpdateAsync(Guid reportId, string status, CancellationToken cancellationToken = default);
}

