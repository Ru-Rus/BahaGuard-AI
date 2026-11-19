using BahaGuard.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BahaGuard.Api.Hubs;

public interface IFloodAlertClient
{
    Task ReceiveNewFloodReport(FloodReportResponseDto report);
    Task ReceiveReportStatusUpdate(Guid reportId, string status);
}

public class FloodAlertHub : Hub<IFloodAlertClient>
{
    public async Task SubscribeToCityAlerts(string city)
    {
        var groupName = string.IsNullOrWhiteSpace(city) ? "Manila" : city.Trim().ToLowerInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task UnsubscribeFromCityAlerts(string city)
    {
        var groupName = string.IsNullOrWhiteSpace(city) ? "Manila" : city.Trim().ToLowerInvariant();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
