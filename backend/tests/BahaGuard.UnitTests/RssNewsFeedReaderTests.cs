using BahaGuard.Infrastructure.Services;
using Xunit;

namespace BahaGuard.UnitTests;

public class RssNewsFeedReaderTests
{
    [Fact]
    public void ParseRssXml_WithValidRss2Feed_ShouldExtractNewsItemsCorrectly()
    {
        var rssXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>Inquirer Metro Alerts</title>
                <link>https://newsinfo.inquirer.net</link>
                <item>
                  <title>Half-tire flood reported along Rizal Ave Blumentritt</title>
                  <description><![CDATA[<p>Water has accumulated up to half-tire along Blumentritt corner Rizal Avenue.</p>]]></description>
                  <link>https://newsinfo.inquirer.net/blumentritt-flood</link>
                  <pubDate>Thu, 19 Nov 2026 14:00:00 +0800</pubDate>
                </item>
                <item>
                  <title>Gutter-deep flood on Taft Ave Pedro Gil</title>
                  <description>Gutter deep waters reported along Taft Avenue.</description>
                  <link>https://newsinfo.inquirer.net/taft-flood</link>
                  <pubDate>Thu, 19 Nov 2026 14:15:00 +0800</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var items = RssNewsFeedReader.ParseRssXml(rssXml, "Default Source");

        Assert.Equal(2, items.Count);
        Assert.Equal("Half-tire flood reported along Rizal Ave Blumentritt", items[0].Title);
        Assert.Contains("half-tire along Blumentritt", items[0].Description);
        Assert.Equal("Inquirer Metro Alerts", items[0].Source);
        Assert.NotNull(items[0].Link);

        Assert.Equal("Gutter-deep flood on Taft Ave Pedro Gil", items[1].Title);
    }

    [Fact]
    public void ParseRssXml_WithAtomFeed_ShouldExtractEntryItems()
    {
        var atomXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>Google News - Flood</title>
              <entry>
                <title>Knee-deep flood shuts down España Boulevard near UST</title>
                <summary>Heavy rain causes knee-deep flood along España Blvd.</summary>
                <link href="https://news.google.com/articles/123" />
                <updated>2026-11-19T06:00:00Z</updated>
              </entry>
            </feed>
            """;

        var items = RssNewsFeedReader.ParseRssXml(atomXml, "Google News");

        Assert.Single(items);
        Assert.Equal("Knee-deep flood shuts down España Boulevard near UST", items[0].Title);
        Assert.Contains("knee-deep flood along España", items[0].Description);
        Assert.Equal("Google News", items[0].Source);
    }

    [Fact]
    public void ParseRssXml_WithInvalidXml_ShouldReturnEmptyListWithoutThrowing()
    {
        var invalidXml = "not valid xml at all <<>>>";

        var items = RssNewsFeedReader.ParseRssXml(invalidXml, "Fallback");

        Assert.Empty(items);
    }

    [Fact]
    public void GetSimulatedOfficialBulletins_ShouldReturnCuratedDisasterAlerts()
    {
        var bulletins = RssNewsFeedReader.GetSimulatedOfficialBulletins();

        Assert.NotEmpty(bulletins);
        Assert.Contains(bulletins, b => b.Title.Contains("Taft Ave"));
        Assert.Contains(bulletins, b => b.Title.Contains("España"));
        Assert.Contains(bulletins, b => b.Title.Contains("Araneta"));
    }
}
