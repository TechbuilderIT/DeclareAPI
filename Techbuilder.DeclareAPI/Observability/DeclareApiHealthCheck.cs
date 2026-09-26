using Microsoft.Extensions.Diagnostics.HealthChecks;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Observability;

/// <summary>
/// Health check for DeclareAPI that verifies configuration and database connectivity.
/// </summary>
public class DeclareApiHealthCheck : IHealthCheck
{
    private readonly DeclareApiConfig _config;
    private readonly IDataAccess _dataAccess;

    public DeclareApiHealthCheck(DeclareApiConfig config, IDataAccess dataAccess)
    {
        _config = config;
        _dataAccess = dataAccess;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["version"] = _config.Version,
            ["entities"] = _config.Entities.Count,
            ["endpoints"] = _config.Entities.Values.Sum(e => e.Endpoints.Count)
        };

        try
        {
            // Try a simple database connectivity check
            await _dataAccess.CheckConnectionAsync(cancellationToken);

            data["database"] = "connected";

            return HealthCheckResult.Healthy(
                "DeclareAPI is healthy",
                data);
        }
        catch (Exception ex)
        {
            data["database"] = "disconnected";
            data["error"] = ex.Message;

            return HealthCheckResult.Unhealthy(
                "DeclareAPI database connection failed",
                ex,
                data);
        }
    }
}

/// <summary>
/// Lightweight health check that only validates configuration is loaded.
/// </summary>
public class DeclareApiConfigHealthCheck : IHealthCheck
{
    private readonly DeclareApiConfig _config;

    public DeclareApiConfigHealthCheck(DeclareApiConfig config)
    {
        _config = config;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["version"] = _config.Version,
            ["entities"] = _config.Entities.Count,
            ["endpoints"] = _config.Entities.Values.Sum(e => e.Endpoints.Count)
        };

        if (_config.Entities.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "DeclareAPI configuration has no entities",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "DeclareAPI configuration is valid",
            data));
    }
}
