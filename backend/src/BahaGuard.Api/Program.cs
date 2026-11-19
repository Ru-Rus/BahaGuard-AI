using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BahaGuard.Api.Hubs;
using BahaGuard.Application;
using BahaGuard.Application.Interfaces;
using BahaGuard.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Load optional local developer overrides (ignored by .gitignore)
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);

// 1. Core Framework Services & Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// 2. Application & Infrastructure Layers (Clean Architecture)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IFloodAlertNotifier, SignalRFloodAlertNotifier>();

// 3. CORS Policy for Angular Frontend
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? ["http://localhost:4200", "https://localhost:4200", "http://127.0.0.1:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularAppPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 4. Rate Limiting Middleware (Security & Anti-Spam for Commuter Reports)
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiterOptions.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Submission limit exceeded. Please wait a minute before submitting more flood reports."
        }, cancellationToken: token);
    };

    // Limits each client IP to 10 report submissions per minute
    rateLimiterOptions.AddPolicy("ReportsPostPolicy", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// 5. Forwarded headers for reverse proxies (Nginx, Docker, Cloudflare)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// 6. Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// 7. OpenAPI & Swagger UI Documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BahaGuard AI API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "BahaGuard AI - Swagger UI";
        options.DisplayRequestDuration();
    });
}

// 8. Static Files for Uploaded Report Photos
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(); // Default wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseCors("AngularAppPolicy");
app.UseRateLimiter();

app.MapControllers();
app.MapHub<FloodAlertHub>("/hubs/flood-alerts");

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "BahaGuard AI API",
    Timestamp = DateTimeOffset.UtcNow,
    Version = "1.0.0"
}));

// Auto-seed historical flood records for Property Risk Auditor
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<BahaGuard.Infrastructure.Data.BahaGuardDbContext>();
        await BahaGuard.Infrastructure.Data.HistoricalDataSeeder.SeedHistoricalDataAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Historical flood data seeding skipped on startup.");
    }
}

app.Run();

// Needed for integration tests if using WebApplicationFactory
public partial class Program { }
