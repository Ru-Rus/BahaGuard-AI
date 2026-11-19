using BahaGuard.Application.Interfaces;
using BahaGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BahaGuard.Infrastructure.Data;

public class BahaGuardDbContext : DbContext, IBahaGuardDbContext
{
    public BahaGuardDbContext(DbContextOptions<BahaGuardDbContext> options)
        : base(options)
    {
    }

    public DbSet<FloodReport> FloodReports => Set<FloodReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FloodReport>(entity =>
        {
            entity.ToTable("FloodReports");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.ReporterAlias)
                .HasMaxLength(50);

            entity.Property(e => e.RawDescription)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.ExtractedLocationName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Barangay)
                .HasMaxLength(100);

            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasDefaultValue("Manila")
                .IsRequired();

            // SQL Server Spatial column (SRID 4326)
            entity.Property(e => e.Coordinates)
                .HasColumnType("geography")
                .IsRequired();

            entity.Property(e => e.EstimatedDepthInches)
                .IsRequired();

            entity.Property(e => e.DepthCategory)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.PassableMotorcycle)
                .IsRequired();

            entity.Property(e => e.PassableSedan)
                .IsRequired();

            entity.Property(e => e.PassableSuvJeepney)
                .IsRequired();

            entity.Property(e => e.AiConfidenceScore)
                .HasColumnType("decimal(3,2)")
                .IsRequired();

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("SYSDATETIMEOFFSET()")
                .IsRequired();

            entity.Property(e => e.ExpiresAt)
                .IsRequired();
        });
    }
}
