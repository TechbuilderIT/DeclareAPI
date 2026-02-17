using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Caching;
using Techbuilder.DeclareAPI.Observability;
using Techbuilder.DeclareAPI.RateLimiting;
using Techbuilder.DeclareAPI.Routing;

namespace Techbuilder.DeclareAPI.Extensions;

/// <summary>
/// Extension methods for mapping DeclareAPI endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all DeclareAPI endpoints configured in the YAML file.
    /// </summary>
    public static IEndpointRouteBuilder MapDeclareApi(this IEndpointRouteBuilder app)
    {
        var routeGenerator = app.ServiceProvider.GetRequiredService<RouteGenerator>();
        routeGenerator.MapEndpoints(app);
        return app;
    }

    /// <summary>
    /// Maps DeclareAPI health check endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDeclareApiHealthChecks(
        this IEndpointRouteBuilder app,
        string healthPath = "/health",
        string readyPath = "/health/ready")
    {
        var options = app.ServiceProvider.GetService<DeclareApiOptions>();

        if (options?.EnableHealthChecks == true)
        {
            // Liveness probe - just checks if the app is running
            app.MapHealthChecks(healthPath, new HealthCheckOptions
            {
                Predicate = _ => false // No checks, just returns healthy
            });

            // Readiness probe - checks configuration and optionally database
            app.MapHealthChecks(readyPath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
        }

        return app;
    }
}

/// <summary>
/// Extension methods for adding DeclareAPI middleware to the application pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds DeclareAPI observability middleware (correlation ID and request logging).
    /// Call this early in the pipeline, before other middleware.
    /// </summary>
    public static IApplicationBuilder UseDeclareApiObservability(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<DeclareApiOptions>();

        if (options?.EnableCorrelationId == true)
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
        }

        if (options?.EnableRequestLogging == true)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
        }

        return app;
    }

    /// <summary>
    /// Adds DeclareAPI rate limiting and caching middleware.
    /// Call this after authentication/authorization middleware.
    /// </summary>
    public static IApplicationBuilder UseDeclareApiPolicies(this IApplicationBuilder app)
    {
        app.UseDeclareApiRateLimiting();
        app.UseDeclareApiCaching();
        return app;
    }
}
