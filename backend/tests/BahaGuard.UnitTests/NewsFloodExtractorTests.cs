using BahaGuard.Application.DTOs;
using BahaGuard.Infrastructure.AI;
using Xunit;

namespace BahaGuard.UnitTests;

public class NewsFloodExtractorTests
{
    [Fact]
    public void ExtractUsingLocalRules_WithTaftAveGutterFlood_ShouldExtractCorrectLocationAndDepth()
    {
        var item = new NewsAlertItem(
            Title: "MMDA: Gutter-deep flood along Taft Ave. near PGH as of 2:30 PM",
            Description: "FLOOD ALERT: Gutter-deep flood along Taft Avenue corner Pedro Gil. Passable to all vehicles.",
            Source: "Official MMDA Advisory",
            PublishedAt: DateTimeOffset.UtcNow
        );

        var result = NewsFloodExtractor.ExtractUsingLocalRules(item, $"{item.Title} {item.Description}");

        Assert.NotNull(result);
        Assert.True(result.IsFloodIncident);
        Assert.Contains("Taft Ave", result.LocationName);
        Assert.Equal("Manila", result.City);
        Assert.Equal("Gutter", result.DepthCategory);
        Assert.Equal(4, result.EstimatedDepthInches);
        Assert.True(result.PassableMotorcycle);
        Assert.True(result.PassableSedan);
        Assert.True(result.PassableSuvJeepney);
        Assert.True(result.ConfidenceScore >= 0.90m);
    }

    [Fact]
    public void ExtractUsingLocalRules_WithEspanaKneeDeepFlood_ShouldFlagImpassableForSedans()
    {
        var item = new NewsAlertItem(
            Title: "PAGASA Warning: Knee-deep flood along España Boulevard UST Area",
            Description: "Heavy rains caused knee-deep baha along España Blvd in front of UST. Not passable to light vehicles.",
            Source: "DOST-PAGASA Bulletin",
            PublishedAt: DateTimeOffset.UtcNow
        );

        var result = NewsFloodExtractor.ExtractUsingLocalRules(item, $"{item.Title} {item.Description}");

        Assert.NotNull(result);
        Assert.True(result.IsFloodIncident);
        Assert.Contains("España", result.LocationName);
        Assert.Equal("Knee", result.DepthCategory);
        Assert.Equal(15, result.EstimatedDepthInches);
        Assert.False(result.PassableMotorcycle);
        Assert.False(result.PassableSedan);
        Assert.True(result.PassableSuvJeepney);
    }

    [Fact]
    public void ExtractUsingLocalRules_WithAranetaWaistDeepFlood_ShouldFlagImpassableToAllVehicles()
    {
        var item = new NewsAlertItem(
            Title: "GMA Alert: Waist-deep flooding along Araneta Avenue Talayan Creek",
            Description: "Waist-deep flood makes Araneta Avenue completely impassable to all vehicles including SUVs.",
            Source: "GMA Disaster Desk",
            PublishedAt: DateTimeOffset.UtcNow
        );

        var result = NewsFloodExtractor.ExtractUsingLocalRules(item, $"{item.Title} {item.Description}");

        Assert.NotNull(result);
        Assert.True(result.IsFloodIncident);
        Assert.Contains("Araneta Ave", result.LocationName);
        Assert.Equal("Quezon City", result.City);
        Assert.Equal("Waist", result.DepthCategory);
        Assert.Equal(28, result.EstimatedDepthInches);
        Assert.False(result.PassableMotorcycle);
        Assert.False(result.PassableSedan);
        Assert.False(result.PassableSuvJeepney);
    }

    [Fact]
    public void ExtractUsingLocalRules_WithNonFloodClassSuspensionNotice_ShouldReturnNull()
    {
        var item = new NewsAlertItem(
            Title: "Palace declares suspension of classes and work in Metro Manila",
            Description: "Executive Secretary Lucas Bersamin announced class suspensions due to weather conditions.",
            Source: "Official Gazette",
            PublishedAt: DateTimeOffset.UtcNow
        );

        var result = NewsFloodExtractor.ExtractUsingLocalRules(item, $"{item.Title} {item.Description}");

        Assert.Null(result);
    }
}
