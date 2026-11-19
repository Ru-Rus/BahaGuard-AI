namespace BahaGuard.Application.Interfaces;

public interface IFloodAlertNotifier
{
    Task BroadcastReportStatusUpdateAsync(Guid reportId, string status, CancellationToken cancellationToken = default);
}
