using BahaGuard.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BahaGuard.UnitTests;

public class GeminiFloodAnalyzerTests
{
    private readonly GeminiFloodAnalyzer _analyzer = new(NullLogger<GeminiFloodAnalyzer>.Instance);

    [Theory]
    [InlineData("gutter deep lang dito sa may españa", 4, "Gutter", true, true, true)]
    [InlineData("hanggang tuhod na ang baha sa taft pedro gil", 15, "Knee", false, false, true)]
    [InlineData("lubog na sa may españa lacson lagpas bewang", 28, "Waist", false, false, false)]
    [InlineData("half tire baha sa quiapo", 10, "HalfTire", false, true, true)]
    public async Task AnalyzeIncidentAsync_ShouldClassifyDepthAndPassabilityCorrectly(
        string rawDescription,
        int expectedDepth,
        string expectedCategory,
        bool expectedMoto,
        bool expectedSedan,
        bool expectedSuv)
    {
        var result = await _analyzer.AnalyzeIncidentAsync(rawDescription);

        Assert.True(result.IsValidFloodReport);
        Assert.Equal(expectedDepth, result.EstimatedDepthInches);
        Assert.Equal(expectedCategory, result.DepthCategory);
        Assert.Equal(expectedMoto, result.Passability.Motorcycle);
        Assert.Equal(expectedSedan, result.Passability.Sedan);
        Assert.Equal(expectedSuv, result.Passability.SuvOrJeepney);
        Assert.NotEmpty(result.CommuterAdvisory);
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_ShouldRejectSpamReports()
    {
        var result = await _analyzer.AnalyzeIncidentAsync("This is a spam report test fake message");

        Assert.False(result.IsValidFloodReport);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_ShouldExtractEspañaLacsonLandmark()
    {
        var result = await _analyzer.AnalyzeIncidentAsync("Baha sa españa cor lacson ave");

        Assert.Contains("España Blvd cor. Lacson Ave", result.NormalizedLocation);
    }
}
