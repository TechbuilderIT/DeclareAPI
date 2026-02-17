using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.RateLimiting;

/// <summary>
/// Extension methods for configuring DeclareAPI rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds DeclareAPI rate limiting services based on configuration.
    /// </summary>
    public static IServiceCollection AddDeclareApiRateLimiting(
        this IServiceCollection services,
        DeclareApiConfig config)
    {
        var rateLimitPolicies = CollectRateLimitPolicies(config);

        if (rateLimitPolicies.Count == 0)
        {
            return services;
        }

        services.AddRateLimiter(options =>
        {
            // Configure rejection response
            options.RejectionStatusCode = 429;

            // Register each unique policy
            foreach (var policy in rateLimitPolicies)
            {
                options.AddFixedWindowLimiter(policy.Name, limiterOptions =>
                {
                    limiterOptions.PermitLimit = policy.Limit;
                    limiterOptions.Window = TimeSpan.FromSeconds(policy.Window);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
                });
            }
        });

        return services;
    }

    /// <summary>
    /// Applies DeclareAPI rate limiting middleware.
    /// </summary>
    public static IApplicationBuilder UseDeclareApiRateLimiting(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<DeclareApiOptions>();

        if (options?.EnableRateLimiting == true)
        {
            app.UseRateLimiter();
        }

        return app;
    }

    private static List<RateLimitPolicy> CollectRateLimitPolicies(DeclareApiConfig config)
    {
        var policies = new Dictionary<string, RateLimitPolicy>();

        foreach (var entity in config.Entities.Values)
        {
            foreach (var endpoint in entity.Endpoints.Values)
            {
                if (endpoint.RateLimit == null) continue;

                var policyName = endpoint.RateLimit.Policy ??
                    $"ratelimit_{endpoint.RateLimit.Limit}_{endpoint.RateLimit.Window}";

                if (!policies.ContainsKey(policyName))
                {
                    policies[policyName] = new RateLimitPolicy
                    {
                        Name = policyName,
                        Limit = endpoint.RateLimit.Limit,
                        Window = endpoint.RateLimit.Window
                    };
                }
            }
        }

        return policies.Values.ToList();
    }
}

internal record RateLimitPolicy
{
    public required string Name { get; init; }
    public required int Limit { get; init; }
    public required int Window { get; init; }
}
