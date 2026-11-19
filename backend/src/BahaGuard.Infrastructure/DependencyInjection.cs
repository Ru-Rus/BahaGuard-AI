using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure.AI;
using BahaGuard.Infrastructure.Data;
using BahaGuard.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BahaGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext != null)
        {
            services.AddDbContext<BahaGuardDbContext>(configureDbContext);
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=localhost,1433;Database=BahaGuardDb;User Id=sa;Password=BahaGuard@2026!;TrustServerCertificate=True;MultipleActiveResultSets=True;";

            services.AddDbContext<BahaGuardDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.UseNetTopologySuite();
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            });
        }

        services.AddScoped<IBahaGuardDbContext>(sp => sp.GetRequiredService<BahaGuardDbContext>());
        services.AddScoped<IGeminiVisionService, GeminiFloodAnalyzer>();
        services.AddHostedService<ReportExpirationWorker>();

        return services;
    }
}
