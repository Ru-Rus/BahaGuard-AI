using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BahaGuard.Api.Hubs;

public class SignalRFloodAlertNotifier(IHubContext<FloodAlertHub, IFloodAlertClient> hubContext) : IFloodAlertNotifier
{
    public async Task BroadcastNewReportAsync(FloodReportResponseDto report, CancellationToken cancellationToken = default)
    {
        var cityGroup = string.IsNullOrWhiteSpace(report.City) ? "manila" : report.City.Trim().ToLowerInvariant();
        await hubContext.Clients.Group(cityGroup).ReceiveNewFloodReport(report);
        await hubContext.Clients.All.ReceiveNewFloodReport(report);
    }

    public Task BroadcastReportStatusUpdateAsync(Guid reportId, string status, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients.All.ReceiveReportStatusUpdate(reportId, status);
    }
}

