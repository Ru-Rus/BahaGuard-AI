using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BahaGuard.Application.DTOs;
using BahaGuard.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BahaGuard.Infrastructure.AI;

/// <summary>
/// Multimodal Gemini 2.5 Flash Flood Analyzer.
/// Combines street photo visual indicators with Tagalog/Taglish NLP parsing,
/// enforcing Google Gemini Structured Outputs matching the Section 5 JSON Schema.
/// Includes graceful fallback to the local TaglishNormalizer when offline or unconfigured.
/// </summary>
public class GeminiFloodAnalyzer : IGeminiVisionService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiFloodAnalyzer> _logger;
    private readonly string _effectiveApiKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiFloodAnalyzer(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IConfiguration configuration,
        ILogger<GeminiFloodAnalyzer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Resolve API key from Options or environment variables
        _effectiveApiKey = !string.IsNullOrWhiteSpace(_options.ApiKey)
            ? _options.ApiKey
            : configuration["GEMINI_API_KEY"] 
              ?? configuration["Google:ApiKey"] 
              ?? configuration["Gemini:ApiKey"] 
              ?? string.Empty;
    }

    public async Task<FloodAnalysisResultDto> AnalyzeIncidentAsync(
        string rawDescription,
        Stream? imageStream = null,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        bool hasImage = imageStream != null && imageStream.Length > 0;

        if (string.IsNullOrWhiteSpace(_effectiveApiKey))
        {
            _logger.LogInformation("Gemini API Key is not configured. Utilizing local TaglishNormalizer engine.");
            return TaglishNormalizer.Normalize(rawDescription, hasImage);
        }

        try
        {
            return await CallGeminiStructuredOutputAsync(rawDescription, imageStream, mimeType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed or timed out. Falling back to TaglishNormalizer.");
            return TaglishNormalizer.Normalize(rawDescription, hasImage);
        }
    }

    private async Task<FloodAnalysisResultDto> CallGeminiStructuredOutputAsync(
        string rawDescription,
        Stream? imageStream,
        string? mimeType,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.5-flash" : _options.Model;
        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_effectiveApiKey}";

        var partsArray = new JsonArray
        {
            new JsonObject
            {
                ["text"] = $"Analyze commuter report: \"{rawDescription}\""
            }
        };

        // Attach image inlineData if present
        if (imageStream != null && imageStream.Length > 0)
        {
            if (imageStream.CanSeek)
            {
                imageStream.Seek(0, SeekOrigin.Begin);
            }

            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var base64Data = Convert.ToBase64String(memoryStream.ToArray());

            partsArray.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType,
                    ["data"] = base64Data
                }
            });
        }

        var requestBody = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = "You are a Metro Manila flood and traffic intelligence analyzer. Analyze the uploaded street photo and Tagalog/Taglish commuter report. Estimate water depth using visual references (curbs, sidewalks, vehicle tires, jeepney step-boards, pedestrians). Reject images unrelated to streets/weather/flooding by setting isValidFloodReport to false."
                    }
                }
            },
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = partsArray
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["isValidFloodReport"] = new JsonObject { ["type"] = "BOOLEAN" },
                        ["rejectionReason"] = new JsonObject { ["type"] = "STRING", ["nullable"] = true },
                        ["normalizedLocation"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["description"] = "Clean intersection or landmark name in Metro Manila extracted from Taglish text"
                        },
                        ["estimatedDepthInches"] = new JsonObject { ["type"] = "INTEGER" },
                        ["depthCategory"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["enum"] = new JsonArray { "Dry", "Gutter", "HalfTire", "Knee", "Waist", "ChestPlus" }
                        },
                        ["passability"] = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["properties"] = new JsonObject
                            {
                                ["motorcycle"] = new JsonObject { ["type"] = "BOOLEAN" },
                                ["sedan"] = new JsonObject { ["type"] = "BOOLEAN" },
                                ["suvOrJeepney"] = new JsonObject { ["type"] = "BOOLEAN" }
                            },
                            ["required"] = new JsonArray { "motorcycle", "sedan", "suvOrJeepney" }
                        },
                        ["confidenceScore"] = new JsonObject { ["type"] = "NUMBER" },
                        ["commuterAdvisory"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["description"] = "1-sentence practical warning in clear English/Taglish for drivers"
                        }
                    },
                    ["required"] = new JsonArray
                    {
                        "isValidFloodReport",
                        "normalizedLocation",
                        "estimatedDepthInches",
                        "depthCategory",
                        "passability",
                        "confidenceScore",
                        "commuterAdvisory"
                    }
                }
            }
        };

        var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var candidate = doc.RootElement.GetProperty("candidates")[0];
        var text = candidate.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini returned empty structured output text.");
        }

        var result = JsonSerializer.Deserialize<FloodAnalysisResultDto>(text, JsonOpts);
        return result ?? throw new InvalidOperationException("Failed to deserialize Gemini structured response.");
    }
}
