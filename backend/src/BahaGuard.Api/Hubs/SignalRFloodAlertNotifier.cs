using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BahaGuard.Api.Hubs;

public class SignalRFloodAlertNotifier(IHubContext<FloodAlertHub, IFloodAlertClient> hubContext) : IFloodAlertNotifier
{
    public Task BroadcastReportStatusUpdateAsync(Guid reportId, string status, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.All.ReceiveReportStatusUpdate(reportId, status);
    }
}
