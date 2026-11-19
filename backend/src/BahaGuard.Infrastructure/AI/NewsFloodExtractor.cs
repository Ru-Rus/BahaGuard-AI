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
/// Extracts structured street flood incidents, water depth, and passability from raw news alerts and official bulletins.
/// Uses Gemini 2.5 Flash Structured Outputs when available, with resilient local NLP fallback.
/// </summary>
public class NewsFloodExtractor : INewsFloodExtractor
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly ILogger<NewsFloodExtractor> _logger;
    private readonly string _effectiveApiKey;

    private static readonly (string Keyword, string Name, string City, string Barangay, double Lat, double Lng)[] ManilaKnownHotspots =
    [
        ("españa", "España Blvd (UST Basin)", "Manila", "Sampaloc", 14.6091, 120.9903),
        ("ust", "UST España / Dapitan Corridor", "Manila", "Sampaloc", 14.6091, 120.9903),
        ("dapitan", "Dapitan St cor. Lacson Ave", "Manila", "Sampaloc", 14.6115, 120.9918),
        ("lacson", "Lacson Ave cor. España Blvd", "Manila", "Sampaloc", 14.6095, 120.9905),
        ("pedro gil", "Taft Ave cor. Pedro Gil St", "Manila", "Ermita", 14.5776, 120.9886),
        ("taft", "Taft Ave (PGH / UN Ave Corridor)", "Manila", "Ermita", 14.5702, 120.9915),
        ("dlsu", "Taft Ave cor. Vito Cruz (DLSU)", "Manila", "Malate", 14.5648, 120.9932),
        ("vito cruz", "Vito Cruz cor. Taft Ave", "Manila", "Malate", 14.5648, 120.9932),
        ("quiapo", "Quezon Blvd (Quiapo Underpass)", "Manila", "Quiapo", 14.5990, 120.9842),
        ("blumentritt", "Rizal Ave cor. Blumentritt St", "Manila", "Santa Cruz", 14.6225, 120.9830),
        ("tayuman", "Rizal Ave cor. Tayuman St", "Manila", "Santa Cruz", 14.6190, 120.9825),
        ("santa mesa", "Santa Mesa (PUP Basin / Teresa St)", "Manila", "Santa Mesa", 14.5985, 121.0110),
        ("pup", "PUP Main Campus (Anonas St)", "Manila", "Santa Mesa", 14.5985, 121.0110),
        ("araneta", "Araneta Ave cor. E. Rodriguez", "Quezon City", "Tatalon", 14.6200, 121.0135),
        ("talayan", "Araneta Ave near Talayan Creek", "Quezon City", "Talayan", 14.6300, 121.0120),
        ("katipunan", "Katipunan Ave near Ateneo / Miriam", "Quezon City", "Loyola Heights", 14.6400, 121.0750),
        ("edsa", "EDSA Ortigas / Megamall Corridor", "Mandaluyong", "Wack-Wack", 14.5840, 121.0560),
        ("c5", "C5 Bagong Ilog Flyover", "Pasig", "Bagong Ilog", 14.5680, 121.0650),
        ("marikina", "Marikina Riverbanks / Sto. Niño", "Marikina", "Sto. Niño", 14.6350, 121.0960),
        ("buendia", "Gil Puyat Ave (Buendia) cor. Taft", "Pasay", "San Isidro", 14.5540, 120.9960)
    ];

    public NewsFloodExtractor(
        HttpClient httpClient,
        IOptions<GeminiOptions> geminiOptions,
        IConfiguration configuration,
        ILogger<NewsFloodExtractor> logger)
    {
        _httpClient = httpClient;
        _geminiOptions = geminiOptions.Value;
        _logger = logger;

        _effectiveApiKey = !string.IsNullOrWhiteSpace(_geminiOptions.ApiKey)
            ? _geminiOptions.ApiKey
            : configuration["GEMINI_API_KEY"]
              ?? configuration["Google:ApiKey"]
              ?? configuration["Gemini:ApiKey"]
              ?? string.Empty;
    }

    public async Task<ExtractedNewsFloodDto?> ExtractFloodIncidentAsync(
        NewsAlertItem item,
        CancellationToken cancellationToken = default)
    {
        var combinedText = $"{item.Title} {item.Description}".Trim();
        if (string.IsNullOrWhiteSpace(combinedText)) return null;

        // If Gemini API is configured, use Gemini 2.5 Flash Structured Outputs
        if (!string.IsNullOrWhiteSpace(_effectiveApiKey))
        {
            try
            {
                var geminiResult = await ExtractWithGeminiAsync(item, combinedText, cancellationToken);
                if (geminiResult != null) return geminiResult;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini extraction failed for news item '{Title}'. Using local NLP rules.", item.Title);
            }
        }

        // Local rules fallback
        return ExtractUsingLocalRules(item, combinedText);
    }

    private async Task<ExtractedNewsFloodDto?> ExtractWithGeminiAsync(
        NewsAlertItem item,
        string combinedText,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(_geminiOptions.Model) ? "gemini-2.5-flash" : _geminiOptions.Model;
        var url = $"{_geminiOptions.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_effectiveApiKey}";

        var prompt = $"""
            You are a Metro Manila emergency dispatch and flood incident extraction specialist for BahaGuard AI.
            Analyze the following news advisory or official bulletin:
            Title: "{item.Title}"
            Description: "{item.Description}"
            Source: "{item.Source}"

            Instructions:
            1. Determine whether this advisory explicitly reports an active, receding, or imminent street flood / waterlogging on a specific road, intersection, or district in Metro Manila, Philippines.
            2. If it is only a general weather forecast (e.g. "chances of rain"), school/work suspension announcement without specific flooded road names, or non-flood news, set isFloodIncident=false.
            3. If it IS a flood incident, extract the exact intersection/street name, city, barangay, estimated depth in inches, depthCategory (Gutter, HalfTire, Knee, Waist, ChestPlus), vehicle passability, and GPS coordinates (Latitude, Longitude) within Metro Manila.
            4. Provide an accurate summary and set confidenceScore (0.90 to 0.98 for official reports like MMDA/PAGASA).
            """;

        var schemaObj = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["isFloodIncident"] = new JsonObject { ["type"] = "BOOLEAN" },
                ["locationName"] = new JsonObject { ["type"] = "STRING" },
                ["city"] = new JsonObject { ["type"] = "STRING" },
                ["barangay"] = new JsonObject { ["type"] = "STRING" },
                ["latitude"] = new JsonObject { ["type"] = "NUMBER" },
                ["longitude"] = new JsonObject { ["type"] = "NUMBER" },
                ["estimatedDepthInches"] = new JsonObject { ["type"] = "INTEGER" },
                ["depthCategory"] = new JsonObject
                {
                    ["type"] = "STRING",
                    ["enum"] = new JsonArray { "Gutter", "HalfTire", "Knee", "Waist", "ChestPlus" }
                },
                ["passableMotorcycle"] = new JsonObject { ["type"] = "BOOLEAN" },
                ["passableSedan"] = new JsonObject { ["type"] = "BOOLEAN" },
                ["passableSuvJeepney"] = new JsonObject { ["type"] = "BOOLEAN" },
                ["confidenceScore"] = new JsonObject { ["type"] = "NUMBER" },
                ["summary"] = new JsonObject { ["type"] = "STRING" }
            },
            ["required"] = new JsonArray
            {
                "isFloodIncident",
                "locationName",
                "city",
                "latitude",
                "longitude",
                "estimatedDepthInches",
                "depthCategory",
                "passableMotorcycle",
                "passableSedan",
                "passableSuvJeepney",
                "confidenceScore",
                "summary"
            }
        };

        var requestBody = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = prompt }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.1,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = schemaObj
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);

        var candidate = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        using var structuredDoc = JsonDocument.Parse(candidate);
        var root = structuredDoc.RootElement;

        bool isFlood = root.GetProperty("isFloodIncident").GetBoolean();
        if (!isFlood) return null;

        return new ExtractedNewsFloodDto(
            IsFloodIncident: true,
            LocationName: root.GetProperty("locationName").GetString() ?? item.Title,
            City: root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() ?? "Manila" : "Manila",
            Barangay: root.TryGetProperty("barangay", out var brgyEl) ? brgyEl.GetString() : null,
            Latitude: root.GetProperty("latitude").GetDouble(),
            Longitude: root.GetProperty("longitude").GetDouble(),
            EstimatedDepthInches: root.GetProperty("estimatedDepthInches").GetInt32(),
            DepthCategory: root.GetProperty("depthCategory").GetString() ?? "Gutter",
            PassableMotorcycle: root.GetProperty("passableMotorcycle").GetBoolean(),
            PassableSedan: root.GetProperty("passableSedan").GetBoolean(),
            PassableSuvJeepney: root.GetProperty("passableSuvJeepney").GetBoolean(),
            ConfidenceScore: (decimal)root.GetProperty("confidenceScore").GetDouble(),
            Source: item.Source,
            Summary: root.GetProperty("summary").GetString() ?? item.Description
        );
    }

    /// <summary>
    /// Fast and robust rule-based extractor for Metro Manila alerts.
    /// Matches flood severity indicators, vehicle passability, and landmark coordinates.
    /// </summary>
    public static ExtractedNewsFloodDto? ExtractUsingLocalRules(NewsAlertItem item, string combinedText)
    {
        var lower = combinedText.ToLowerInvariant();

        // 1. Check if the text reports active flooding (filter out purely non-flood notices like class suspensions)
        bool hasFloodKeyword = lower.Contains("baha") || lower.Contains("flood") || lower.Contains("submerged")
            || lower.Contains("waterlogged") || lower.Contains("impassable") || lower.Contains("lubog")
            || lower.Contains("gutter") || lower.Contains("knee-deep") || lower.Contains("waist-deep")
            || lower.Contains("tire-deep") || lower.Contains("half-tire");

        if (!hasFloodKeyword)
        {
            return null;
        }

        // 2. Identify Metro Manila Landmark
        var matchedHotspot = ManilaKnownHotspots.FirstOrDefault(h => lower.Contains(h.Keyword));
        string locationName = matchedHotspot != default
            ? matchedHotspot.Name
            : "Metro Manila Flooded Corridor";
        string city = matchedHotspot != default ? matchedHotspot.City : "Manila";
        string? barangay = matchedHotspot != default ? matchedHotspot.Barangay : null;
        double lat = matchedHotspot != default ? matchedHotspot.Lat : 14.5995;
        double lng = matchedHotspot != default ? matchedHotspot.Lng : 120.9842;

        // 3. Determine Depth Category and Passability
        int depthInches = 4;
        string depthCat = "Gutter";
        bool moto = true;
        bool sedan = true;
        bool suv = true;

        if (lower.Contains("chest") || lower.Contains("dibdib") || lower.Contains("lampas tao"))
        {
            depthInches = 48;
            depthCat = "ChestPlus";
            moto = false;
            sedan = false;
            suv = false;
        }
        else if (lower.Contains("waist") || lower.Contains("bewang") || lower.Contains("baywang") || lower.Contains("not passable to all") || lower.Contains("impassable to all"))
        {
            depthInches = 28;
            depthCat = "Waist";
            moto = false;
            sedan = false;
            suv = false;
        }
        else if (lower.Contains("knee") || lower.Contains("tuhod") || lower.Contains("binti") || lower.Contains("not passable to light"))
        {
            depthInches = 15;
            depthCat = "Knee";
            moto = false;
            sedan = false;
            suv = true;
        }
        else if (lower.Contains("tire") || lower.Contains("gulong") || lower.Contains("half-tire") || lower.Contains("kalahating gulong"))
        {
            depthInches = 10;
            depthCat = "HalfTire";
            moto = false;
            sedan = true;
            suv = true;
        }

        decimal confidence = item.Source.Contains("MMDA", StringComparison.OrdinalIgnoreCase) ||
                             item.Source.Contains("PAGASA", StringComparison.OrdinalIgnoreCase)
            ? 0.96m
            : 0.90m;

        return new ExtractedNewsFloodDto(
            IsFloodIncident: true,
            LocationName: locationName,
            City: city,
            Barangay: barangay,
            Latitude: lat,
            Longitude: lng,
            EstimatedDepthInches: depthInches,
            DepthCategory: depthCat,
            PassableMotorcycle: moto,
            PassableSedan: sedan,
            PassableSuvJeepney: suv,
            ConfidenceScore: confidence,
            Source: item.Source,
            Summary: string.IsNullOrWhiteSpace(item.Description) ? item.Title : item.Description
        );
    }
}
