using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Caching;

/// <summary>
/// Extension methods for configuring DeclareAPI output caching.
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Adds DeclareAPI output caching services based on configuration.
    /// </summary>
    public static IServiceCollection AddDeclareApiCaching(
        this IServiceCollection services,
        DeclareApiConfig config)
    {
        var cachePolicies = CollectCachePolicies(config);

        // Registered even when no endpoint declares a policy: UseDeclareApiCaching() adds
        // the UseOutputCache middleware whenever the feature is enabled, and it requires these services.
        services.AddOutputCache(options =>
        {
            // Register each unique cache policy
            foreach (var policy in cachePolicies)
            {
                options.AddPolicy(policy.Name, builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(policy.Duration));

                    if (policy.VaryByQuery)
                    {
                        builder.SetVaryByQuery("*");
                    }
                    else if (policy.VaryByParams?.Count > 0)
                    {
                        builder.SetVaryByQuery(policy.VaryByParams.ToArray());
                    }

                    if (policy.VaryByUser)
                    {
                        builder.VaryByValue((context, ct) =>
                        {
                            var user = context.User?.Identity?.Name ?? "anonymous";
                            return new ValueTask<KeyValuePair<string, string>>(
                                new KeyValuePair<string, string>("user", user));
                        });
                    }
                });
            }
        });

        return services;
    }

    /// <summary>
    /// Applies DeclareAPI output caching middleware.
    /// </summary>
    public static IApplicationBuilder UseDeclareApiCaching(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<DeclareApiOptions>();

        if (options?.EnableCaching == true)
        {
            app.UseOutputCache();
        }

        return app;
    }

    private static List<CachePolicy> CollectCachePolicies(DeclareApiConfig config)
    {
        var policies = new Dictionary<string, CachePolicy>();

        foreach (var entity in config.Entities.Values)
        {
            foreach (var endpoint in entity.Endpoints.Values)
            {
                if (endpoint.Cache == null) continue;

                var policyName = $"cache_{endpoint.Cache.Duration}";

                if (!policies.ContainsKey(policyName))
                {
                    policies[policyName] = new CachePolicy
                    {
                        Name = policyName,
                        Duration = endpoint.Cache.Duration,
                        VaryByQuery = endpoint.Cache.VaryByQuery,
                        VaryByUser = endpoint.Cache.VaryByUser,
                        VaryByParams = endpoint.Cache.VaryByParams
                    };
                }
            }
        }

        return policies.Values.ToList();
    }
}

internal record CachePolicy
{
    public required string Name { get; init; }
    public required int Duration { get; init; }
    public bool VaryByQuery { get; init; }
    public bool VaryByUser { get; init; }
    public List<string>? VaryByParams { get; init; }
}
