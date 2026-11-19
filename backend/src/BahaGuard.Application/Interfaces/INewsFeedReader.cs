using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface INewsFeedReader
{
    Task<IReadOnlyList<NewsAlertItem>> FetchLatestAlertsAsync(CancellationToken cancellationToken = default);
}
