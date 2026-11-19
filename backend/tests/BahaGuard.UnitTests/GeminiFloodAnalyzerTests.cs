using System.Net;
using System.Text;
using BahaGuard.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BahaGuard.UnitTests;

public class GeminiFloodAnalyzerTests
{
    private static GeminiFloodAnalyzer CreateAnalyzerWithMockHttp(
        string? apiKey = null,
        HttpStatusCode responseCode = HttpStatusCode.OK,
        string? responseBody = null)
    {
        var handler = new MockHttpMessageHandler(responseCode, responseBody ?? string.Empty);
        var httpClient = new HttpClient(handler);

        var options = Options.Create(new GeminiOptions
        {
            ApiKey = apiKey ?? string.Empty,
            Model = "gemini-2.5-flash",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        });

        var configuration = new ConfigurationBuilder().Build();

        return new GeminiFloodAnalyzer(
            httpClient,
            options,
            configuration,
            NullLogger<GeminiFloodAnalyzer>.Instance);
    }

    [Fact]
    public void TaglishNormalizer_ShouldExtractUSTDapitanAndKneeDeep()
    {
        // Roadmap requirement: Test inputs like "hanggang binti na sa may UST Dapitan"
        var result = TaglishNormalizer.Normalize("hanggang binti na sa may UST Dapitan");

        Assert.True(result.IsValidFloodReport);
        Assert.Contains("Dapitan St cor. Lacson Ave (UST area)", result.NormalizedLocation);
        Assert.Equal("Knee", result.DepthCategory);
        Assert.Equal(15, result.EstimatedDepthInches);
        Assert.False(result.Passability.Motorcycle);
        Assert.False(result.Passability.Sedan);
        Assert.True(result.Passability.SuvOrJeepney);
    }

    [Theory]
    [InlineData("gutter deep lang dito sa may españa", 4, "Gutter", true, true, true)]
    [InlineData("half tire baha sa quiapo", 10, "HalfTire", false, true, true)]
    [InlineData("hanggang tuhod na ang baha sa taft pedro gil", 15, "Knee", false, false, true)]
    [InlineData("lubog na sa may españa lacson lagpas bewang", 28, "Waist", false, false, false)]
    [InlineData("chest deep lampas tao sa underpass", 48, "ChestPlus", false, false, false)]
    public void TaglishNormalizer_ShouldClassifyDepthAndPassabilityCorrectly(
        string rawDescription,
        int expectedDepth,
        string expectedCategory,
        bool expectedMoto,
        bool expectedSedan,
        bool expectedSuv)
    {
        var result = TaglishNormalizer.Normalize(rawDescription);

        Assert.True(result.IsValidFloodReport);
        Assert.Equal(expectedDepth, result.EstimatedDepthInches);
        Assert.Equal(expectedCategory, result.DepthCategory);
        Assert.Equal(expectedMoto, result.Passability.Motorcycle);
        Assert.Equal(expectedSedan, result.Passability.Sedan);
        Assert.Equal(expectedSuv, result.Passability.SuvOrJeepney);
        Assert.NotEmpty(result.CommuterAdvisory);
    }

    [Fact]
    public void TaglishNormalizer_ShouldRejectSpamReports()
    {
        var result = TaglishNormalizer.Normalize("This is a spam report test fake message not flood");

        Assert.False(result.IsValidFloodReport);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WithoutApiKey_ShouldGracefullyFallbackToTaglishNormalizer()
    {
        var analyzer = CreateAnalyzerWithMockHttp(apiKey: null);

        var result = await analyzer.AnalyzeIncidentAsync("hanggang tuhod sa españa lacson");

        Assert.True(result.IsValidFloodReport);
        Assert.Equal("Knee", result.DepthCategory);
        Assert.Contains("España Blvd cor. Lacson Ave", result.NormalizedLocation);
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WithApiKey_ShouldCallGeminiAndParseStructuredOutput()
    {
        const string geminiStructuredJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"isValidFloodReport\": true,\n  \"rejectionReason\": null,\n  \"normalizedLocation\": \"España Blvd cor. Lacson Ave, Sampaloc, Manila\",\n  \"estimatedDepthInches\": 12,\n  \"depthCategory\": \"Knee\",\n  \"passability\": {\n    \"motorcycle\": false,\n    \"sedan\": false,\n    \"suvOrJeepney\": true\n  },\n  \"confidenceScore\": 0.98,\n  \"commuterAdvisory\": \"Knee-deep flood on España. Avoid sedans.\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var analyzer = CreateAnalyzerWithMockHttp(apiKey: "AIzaSyTestKey123", responseCode: HttpStatusCode.OK, responseBody: geminiStructuredJson);

        using var imageStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-jpeg-image-bytes"));

        var result = await analyzer.AnalyzeIncidentAsync(
            "baha sa españa",
            imageStream,
            "image/jpeg");

        Assert.True(result.IsValidFloodReport);
        Assert.Equal("España Blvd cor. Lacson Ave, Sampaloc, Manila", result.NormalizedLocation);
        Assert.Equal(12, result.EstimatedDepthInches);
        Assert.Equal("Knee", result.DepthCategory);
        Assert.False(result.Passability.Motorcycle);
        Assert.False(result.Passability.Sedan);
        Assert.True(result.Passability.SuvOrJeepney);
        Assert.Equal(0.98m, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WhenGeminiFails_ShouldFallbackGracefully()
    {
        var analyzer = CreateAnalyzerWithMockHttp(
            apiKey: "AIzaSyTestKey123",
            responseCode: HttpStatusCode.InternalServerError,
            responseBody: "Gemini server error");

        var result = await analyzer.AnalyzeIncidentAsync("gutter deep tubig sa quiapo church");

        Assert.True(result.IsValidFloodReport);
        Assert.Equal("Gutter", result.DepthCategory);
        Assert.Contains("Quiapo Church", result.NormalizedLocation);
    }

    private class MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
