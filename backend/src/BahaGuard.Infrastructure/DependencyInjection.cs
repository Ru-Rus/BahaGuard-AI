using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure.AI;
using BahaGuard.Infrastructure.Data;
using BahaGuard.Infrastructure.Options;
using BahaGuard.Infrastructure.Services;
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

        // Configure Gemini AI service with resilient HTTP Client
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<IGeminiVisionService, GeminiFloodAnalyzer>((sp, client) =>
        {
            var options = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
        });

        services.AddHttpClient<IPropertyAuditService, PropertyAuditService>((sp, client) =>
        {
            var options = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
        });

        // Automated News & Official Alert Ingestion Services
        services.Configure<NewsFeedOptions>(configuration.GetSection(NewsFeedOptions.SectionName));
        services.AddHttpClient<INewsFeedReader, RssNewsFeedReader>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient<INewsFloodExtractor, NewsFloodExtractor>((sp, client) =>
        {
            var options = configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
        });
        services.AddScoped<INewsAlertIngestionService, NewsAlertIngestionService>();

        // Hosted Background Workers
        services.AddHostedService<ReportExpirationWorker>();
        services.AddHostedService<NewsAlertIngestionWorker>();

        return services;
    }
}
