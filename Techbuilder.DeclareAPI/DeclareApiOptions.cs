using System.Reflection;
using Techbuilder.DeclareAPI.Core.Abstractions;

namespace Techbuilder.DeclareAPI;

/// <summary>
/// Configuration options for DeclareAPI.
/// </summary>
public class DeclareApiOptions
{
    /// <summary>
    /// Path to the YAML configuration file.
    /// Default: "declareapi.yaml"
    /// </summary>
    public string ConfigFile { get; set; } = "declareapi.yaml";

    /// <summary>
    /// The data access implementation type.
    /// </summary>
    public Type? DataAccessType { get; private set; }

    /// <summary>
    /// Factory function to create the data access implementation.
    /// </summary>
    public Func<IServiceProvider, IDataAccess>? DataAccessFactory { get; private set; }

    /// <summary>
    /// Assemblies to scan for custom handlers.
    /// </summary>
    public List<Assembly> HandlerAssemblies { get; } = new();

    /// <summary>
    /// Whether to enable request logging middleware.
    /// Default: true
    /// </summary>
    public bool EnableRequestLogging { get; set; } = true;

    /// <summary>
    /// Whether to enable correlation ID tracking.
    /// Default: true
    /// </summary>
    public bool EnableCorrelationId { get; set; } = true;

    /// <summary>
    /// Whether to enable health checks.
    /// Default: true
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Whether to include database connectivity in health checks.
    /// Default: true
    /// </summary>
    public bool IncludeDatabaseHealthCheck { get; set; } = true;

    /// <summary>
    /// Whether to enable rate limiting.
    /// Default: true
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Whether to enable response caching.
    /// Default: true
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Uses the specified IDataAccess implementation type.
    /// </summary>
    public DeclareApiOptions UseDataAccess<TDataAccess>() where TDataAccess : class, IDataAccess
    {
        DataAccessType = typeof(TDataAccess);
        return this;
    }

    /// <summary>
    /// Uses the specified IDataAccess implementation with a factory function.
    /// </summary>
    public DeclareApiOptions UseDataAccess(Func<IServiceProvider, IDataAccess> factory)
    {
        DataAccessFactory = factory;
        return this;
    }

    /// <summary>
    /// Scans the assembly containing the specified type for custom handlers.
    /// </summary>
    public DeclareApiOptions ScanHandlersFrom<T>()
    {
        return ScanHandlersFrom(typeof(T));
    }

    /// <summary>
    /// Scans the assembly containing the specified type for custom handlers.
    /// </summary>
    public DeclareApiOptions ScanHandlersFrom(Type type)
    {
        return ScanHandlersFrom(type.Assembly);
    }

    /// <summary>
    /// Scans the specified assembly for custom handlers.
    /// </summary>
    public DeclareApiOptions ScanHandlersFrom(Assembly assembly)
    {
        if (!HandlerAssemblies.Contains(assembly))
        {
            HandlerAssemblies.Add(assembly);
        }
        return this;
    }
}
