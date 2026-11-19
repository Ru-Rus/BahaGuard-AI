using BahaGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace BahaGuard.Infrastructure.Data;

/// <summary>
/// Seeds realistic historical flood events across Metro Manila flood-prone corridors
/// (España, Taft, Quiapo, Sampaloc, Malate, Blumentritt, Santa Mesa, Araneta).
/// Used by the Property Flood-Risk Auditor to compute spatial risk metrics, frequency, and receding durations.
/// </summary>
public static class HistoricalDataSeeder
{
    private static readonly GeometryFactory Srid4326Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static async Task SeedHistoricalDataAsync(BahaGuardDbContext context)
    {
        // Only seed if no historical records exist
        if (await context.FloodReports.AnyAsync(r => r.Status == "Resolved"))
        {
            return;
        }

        var baseTime = DateTimeOffset.UtcNow.AddMonths(-6);

        var historicalHotspots = new List<FloodReport>
        {
            // --- España Blvd & UST Basin ---
            CreateHistorical(
                "España Blvd cor. Lacson Ave, Sampaloc, Manila",
                "Severe Habagat monsoon ponding. Waist-deep flood outside UST hospital gate.",
                14.6091, 120.9903, 30, "Waist", false, false, false,
                "Sampaloc", "Manila", baseTime.AddDays(-120), TimeSpan.FromHours(4.5)),

            CreateHistorical(
                "España Blvd cor. Lacson Ave, Sampaloc, Manila",
                "Typhoon Carina runoff. Flood reached knee level on both north and southbound lanes.",
                14.6091, 120.9903, 20, "Knee", false, false, true,
                "Sampaloc", "Manila", baseTime.AddDays(-90), TimeSpan.FromHours(3.5)),

            CreateHistorical(
                "España Blvd cor. P. Noval St, Sampaloc, Manila",
                "Flash flood after torrential 2-hour rain. Gutter to half-tire water depth.",
                14.6068, 120.9880, 10, "HalfTire", false, true, true,
                "Sampaloc", "Manila", baseTime.AddDays(-65), TimeSpan.FromHours(2.0)),

            CreateHistorical(
                "Dapitan St cor. Navarra St (near UST Dapitan Gate), Sampaloc, Manila",
                "Dapitan corridor submerged waist-deep. Ground floor dorm lobbies inundated.",
                14.6115, 120.9918, 32, "Waist", false, false, false,
                "Sampaloc", "Manila", baseTime.AddDays(-140), TimeSpan.FromHours(5.0)),

            CreateHistorical(
                "Dapitan St cor. Lacson Ave, Sampaloc, Manila",
                "Chest-level flood at Lacson-Dapitan intersection. Power shut down in local sub-grid.",
                14.6120, 120.9930, 42, "ChestPlus", false, false, false,
                "Sampaloc", "Manila", baseTime.AddDays(-180), TimeSpan.FromHours(6.0)),

            // --- Taft Avenue Corridor ---
            CreateHistorical(
                "Taft Ave cor. Pedro Gil St, Ermita, Manila",
                "Heavy downpour caused drain overflow. PGH front gates impassable for light vehicles.",
                14.5776, 120.9886, 18, "Knee", false, false, true,
                "Ermita", "Manila", baseTime.AddDays(-110), TimeSpan.FromHours(3.0)),

            CreateHistorical(
                "Taft Ave cor. United Nations (UN) Ave, Ermita, Manila",
                "Gutter to tire deep water at LRT UN station stairs.",
                14.5828, 120.9845, 9, "HalfTire", false, true, true,
                "Ermita", "Manila", baseTime.AddDays(-80), TimeSpan.FromHours(1.8)),

            CreateHistorical(
                "Taft Ave cor. Vito Cruz (P. Ocampo St), Malate, Manila",
                "DLSU perimeter flooded knee-deep. Underpass filled with backflow water.",
                14.5648, 120.9932, 22, "Knee", false, false, true,
                "Malate", "Manila", baseTime.AddDays(-130), TimeSpan.FromHours(3.2)),

            CreateHistorical(
                "Quirino Ave cor. Taft Ave, Malate, Manila",
                "Tire-deep street flooding slowing traffic along LRT Quirino station.",
                14.5702, 120.9915, 12, "HalfTire", false, true, true,
                "Malate", "Manila", baseTime.AddDays(-50), TimeSpan.FromHours(2.2)),

            // --- Quiapo & Plaza Miranda ---
            CreateHistorical(
                "Quezon Blvd Underpass, Quiapo, Manila",
                "Underpass completely submerged during peak monsoon season.",
                14.5990, 120.9842, 48, "ChestPlus", false, false, false,
                "Quiapo", "Manila", baseTime.AddDays(-160), TimeSpan.FromHours(7.0)),

            CreateHistorical(
                "Plaza Miranda / Carlos Palanca St, Quiapo, Manila",
                "Knee-deep water accumulating in front of Quiapo Church and market stalls.",
                14.5982, 120.9835, 16, "Knee", false, false, true,
                "Quiapo", "Manila", baseTime.AddDays(-95), TimeSpan.FromHours(2.8)),

            // --- Blumentritt & Rizal Avenue ---
            CreateHistorical(
                "Rizal Ave cor. Blumentritt St, Santa Cruz, Manila",
                "Blumentritt market road inundated waist-deep. Light delivery vehicles stranded.",
                14.6225, 120.9830, 26, "Waist", false, false, false,
                "Santa Cruz", "Manila", baseTime.AddDays(-105), TimeSpan.FromHours(4.0)),

            // --- Santa Mesa (PUP Basin) ---
            CreateHistorical(
                "Anonas St cor. Teresa St (PUP Main Campus), Santa Mesa, Manila",
                "Pasig river high tide backflow. Road leading to PUP campus impassable for tricycles and sedans.",
                14.5985, 121.0110, 24, "Waist", false, false, false,
                "Santa Mesa", "Manila", baseTime.AddDays(-115), TimeSpan.FromHours(4.2)),

            // --- Araneta Avenue (Quezon City Basin) ---
            CreateHistorical(
                "Araneta Ave cor. E. Rodriguez Sr. Ave, Tatalon, Quezon City",
                "Severe San Juan River overflow. Multi-vehicle stalling at intersection.",
                14.6200, 121.0135, 50, "ChestPlus", false, false, false,
                "Tatalon", "Quezon City", baseTime.AddDays(-170), TimeSpan.FromHours(8.0)),

            CreateHistorical(
                "Araneta Ave cor. Quezon Ave Underpass, Quezon City",
                "Underpass filled with runoff floodwater, waist-deep.",
                14.6300, 121.0080, 36, "Waist", false, false, false,
                "Talayan", "Quezon City", baseTime.AddDays(-125), TimeSpan.FromHours(5.5))
        };

        context.FloodReports.AddRange(historicalHotspots);
        await context.SaveChangesAsync();
    }

    private static FloodReport CreateHistorical(
        string locationName,
        string description,
        double lat,
        double lng,
        int depthInches,
        string depthCategory,
        bool passMoto,
        bool passSedan,
        bool passSuv,
        string barangay,
        string city,
        DateTimeOffset timestamp,
        TimeSpan recedingDuration)
    {
        var pt = Srid4326Factory.CreatePoint(new Coordinate(lng, lat));

        return new FloodReport
        {
            Id = Guid.NewGuid(),
            ReporterAlias = "Historical City Flood Archive",
            RawDescription = description,
            ExtractedLocationName = locationName,
            Barangay = barangay,
            City = city,
            Coordinates = pt,
            EstimatedDepthInches = depthInches,
            DepthCategory = depthCategory,
            PassableMotorcycle = passMoto,
            PassableSedan = passSedan,
            PassableSuvJeepney = passSuv,
            AiConfidenceScore = 0.98m,
            ImageUrl = null,
            Status = "Resolved",
            CreatedAt = timestamp,
            ExpiresAt = timestamp.Add(recedingDuration)
        };
    }
}
