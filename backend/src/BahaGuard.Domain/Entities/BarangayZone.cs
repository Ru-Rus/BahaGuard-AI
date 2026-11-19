using NetTopologySuite.Geometries;

namespace BahaGuard.Domain.Entities;

public class BarangayZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = "Manila";
    public Geometry Boundary { get; set; } = null!;
    public int RiskLevel { get; set; } // 1-5 flood risk level
}
