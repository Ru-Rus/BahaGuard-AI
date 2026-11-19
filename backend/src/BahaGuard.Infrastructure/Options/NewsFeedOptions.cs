namespace BahaGuard.Infrastructure.Options;

public class NewsFeedOptions
{
    public const string SectionName = "NewsFeeds";

    public int PollIntervalMinutes { get; set; } = 10;
    public bool AutoIngestionEnabled { get; set; } = true;
    public double DeduplicationRadiusMeters { get; set; } = 250.0;
    public bool IncludeMockFeedsInDevelopment { get; set; } = true;

    public List<string> FeedUrls { get; set; } =
    [
        "https://news.google.com/rss/search?q=flood+OR+baha+Metro+Manila&hl=en-PH&gl=PH&ceid=PH:en"
    ];
}
