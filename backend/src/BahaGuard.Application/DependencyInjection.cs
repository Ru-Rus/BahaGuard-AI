using BahaGuard.Application.Interfaces;
using BahaGuard.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BahaGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFloodReportService, FloodReportService>();
        return services;
    }
}
