using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace BahaGuard.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FloodReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ReporterAlias = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RawDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExtractedLocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Barangay = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Manila"),
                    Coordinates = table.Column<Point>(type: "geography", nullable: false),
                    EstimatedDepthInches = table.Column<int>(type: "int", nullable: false),
                    DepthCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PassableMotorcycle = table.Column<bool>(type: "bit", nullable: false),
                    PassableSedan = table.Column<bool>(type: "bit", nullable: false),
                    PassableSuvJeepney = table.Column<bool>(type: "bit", nullable: false),
                    AiConfidenceScore = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloodReports", x => x.Id);
                });

            // Spatial Index for fast radius & route intersection queries (SRID 4326)
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_FloodReports_Coordinates') " +
                "CREATE SPATIAL INDEX SIX_FloodReports_Coordinates ON FloodReports(Coordinates);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_FloodReports_Coordinates') " +
                "DROP INDEX SIX_FloodReports_Coordinates ON FloodReports;");

            migrationBuilder.DropTable(
                name: "FloodReports");
        }
    }
}
