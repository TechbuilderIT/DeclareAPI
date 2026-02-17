using Microsoft.Extensions.DependencyInjection;
using Techbuilder.DeclareAPI.Caching;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Observability;
using Techbuilder.DeclareAPI.Handlers;
using Techbuilder.DeclareAPI.Observability;
using Techbuilder.DeclareAPI.RateLimiting;
using Techbuilder.DeclareAPI.Routing;

namespace Techbuilder.DeclareAPI.Extensions;

/// <summary>
/// Extension methods for registering DeclareAPI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DeclareAPI services to the service collection.
    /// </summary>
    public static IServiceCollection AddDeclareApi(
        this IServiceCollection services,
        Action<DeclareApiOptions>? configure = null)
    {
        var options = new DeclareApiOptions();
        configure?.Invoke(options);

        // Load configuration
        var loader = new ConfigLoader();
        var config = loader.LoadFromFile(options.ConfigFile);

        // Register configuration as singleton
        services.AddSingleton(config);
        services.AddSingleton(options);

        // Register handler registry (before RouteGenerator)
        IHandlerRegistry? handlerRegistry = null;
        if (options.HandlerAssemblies.Count > 0)
        {
            handlerRegistry = new HandlerRegistry(options.HandlerAssemblies);
            services.AddSingleton(handlerRegistry);
        }

        // Register RouteGenerator with handler registry
        services.AddSingleton(sp => new RouteGenerator(config, handlerRegistry));

        // Register data access
        RegisterDataAccess(services, options, config);

        // Scan and register custom handlers in DI
        RegisterCustomHandlers(services, options);

        // Register observability services
        RegisterObservability(services, options);

        // Register rate limiting services
        if (options.EnableRateLimiting)
        {
            services.AddDeclareApiRateLimiting(config);
        }

        // Register caching services
        if (options.EnableCaching)
        {
            services.AddDeclareApiCaching(config);
        }

        return services;
    }

    private static void RegisterDataAccess(
        IServiceCollection services,
        DeclareApiOptions options,
        DeclareApiConfig config)
    {
        if (options.DataAccessFactory != null)
        {
            services.AddScoped(options.DataAccessFactory);
        }
        else if (options.DataAccessType != null)
        {
            services.AddScoped(typeof(IDataAccess), options.DataAccessType);
        }
        else
        {
            throw new InvalidOperationException(
                "No IDataAccess implementation configured. " +
                "Call options.UseDataAccess<T>() or options.UseDataAccess(factory) to configure.");
        }
    }

    private static void RegisterCustomHandlers(IServiceCollection services, DeclareApiOptions options)
    {
        foreach (var assembly in options.HandlerAssemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICustomHandler<,>)));

            foreach (var handlerType in handlerTypes)
            {
                // Register handler by its concrete type
                services.AddScoped(handlerType);

                // Also register by the ICustomHandler<,> interface it implements
                var handlerInterface = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICustomHandler<,>));
                services.AddScoped(handlerInterface, handlerType);
            }
        }
    }

    private static void RegisterObservability(IServiceCollection services, DeclareApiOptions options)
    {
        // Register correlation ID accessor (always, as it's a lightweight dependency)
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

        // Register request logger
        if (options.EnableRequestLogging)
        {
            services.AddSingleton<IRequestLogger, DefaultRequestLogger>();
        }

        // Register health checks
        if (options.EnableHealthChecks)
        {
            var healthChecksBuilder = services.AddHealthChecks()
                .AddCheck<DeclareApiConfigHealthCheck>("declareapi-config", tags: new[] { "ready" });

            if (options.IncludeDatabaseHealthCheck)
            {
                healthChecksBuilder.AddCheck<DeclareApiHealthCheck>("declareapi-database", tags: new[] { "ready", "db" });
            }
        }
    }
}
