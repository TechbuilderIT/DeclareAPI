# Observability

DeclareAPI provides comprehensive observability features for monitoring, debugging, and maintaining your API.

## Overview

| Feature | Purpose |
|---------|---------|
| Correlation IDs | Track requests across services |
| Request Logging | Structured logging of requests/responses |
| Health Checks | Monitor API and database health |

## Correlation IDs

Every request is assigned a unique correlation ID for distributed tracing.

### How It Works

1. Check incoming `X-Correlation-ID` header
2. If present, use it; if not, generate a new UUID
3. Add to response headers
4. Available throughout request pipeline

### Configuration

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableCorrelationId = true;  // Enabled by default
});
```

### Usage

Access the correlation ID in your code:

```csharp
public class MyService
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<MyService> _logger;

    public MyService(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<MyService> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public void DoSomething()
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        _logger.LogInformation(
            "Processing request {CorrelationId}",
            correlationId);

        // Pass to external services
        _httpClient.DefaultRequestHeaders.Add(
            "X-Correlation-ID",
            correlationId);
    }
}
```

### Headers

Request/Response includes:
```
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000
```

---

## Request Logging

Structured logging of all HTTP requests and responses.

### Configuration

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableRequestLogging = true;  // Enabled by default
});
```

### Log Output

Each request generates structured logs:

```json
{
  "Timestamp": "2024-01-15T10:30:00.000Z",
  "Level": "Information",
  "MessageTemplate": "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
  "Properties": {
    "Method": "GET",
    "Path": "/api/products",
    "StatusCode": 200,
    "ElapsedMs": 45,
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440000",
    "QueryString": "?page=1&pageSize=10"
  }
}
```

### Custom Request Logger

Implement `IRequestLogger` for custom logging:

```csharp
public class CustomRequestLogger : IRequestLogger
{
    private readonly ILogger<CustomRequestLogger> _logger;
    private readonly IMetricsCollector _metrics;

    public CustomRequestLogger(
        ILogger<CustomRequestLogger> logger,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public void LogRequest(RequestLogEntry entry)
    {
        // Log to your preferred destination
        _logger.LogInformation(
            "Request: {Method} {Path} - {StatusCode} ({ElapsedMs}ms)",
            entry.Method,
            entry.Path,
            entry.StatusCode,
            entry.ElapsedMilliseconds);

        // Collect metrics
        _metrics.RecordRequestDuration(
            entry.Method,
            entry.Path,
            entry.StatusCode,
            entry.ElapsedMilliseconds);

        // Send to APM
        if (entry.StatusCode >= 500)
        {
            _metrics.RecordError(entry.Path, entry.Exception);
        }
    }
}

// Register
builder.Services.AddSingleton<IRequestLogger, CustomRequestLogger>();
```

---

## Health Checks

DeclareAPI provides health check endpoints for Kubernetes probes and monitoring.

### Configuration

```csharp
builder.Services.AddDeclareApi(options =>
{
    options.EnableHealthChecks = true;              // Enabled by default
    options.IncludeDatabaseHealthCheck = true;      // Enabled by default
});

var app = builder.Build();

app.MapDeclareApiHealthChecks();
// Or with custom paths:
app.MapDeclareApiHealthChecks(
    healthPath: "/health",
    readyPath: "/health/ready");
```

### Endpoints

#### Liveness Probe: `/health`

Simple check that the application is running.

```bash
curl http://localhost:5000/health
```

Response:
```json
{
  "status": "Healthy"
}
```

#### Readiness Probe: `/health/ready`

Comprehensive check including database connectivity.

```bash
curl http://localhost:5000/health/ready
```

Response (healthy):
```json
{
  "status": "Healthy",
  "results": {
    "declareapi-config": {
      "status": "Healthy",
      "description": "Configuration is valid"
    },
    "declareapi-database": {
      "status": "Healthy",
      "description": "Database connection successful"
    }
  }
}
```

Response (unhealthy):
```json
{
  "status": "Unhealthy",
  "results": {
    "declareapi-config": {
      "status": "Healthy"
    },
    "declareapi-database": {
      "status": "Unhealthy",
      "description": "Unable to connect to database",
      "exception": "Npgsql.NpgsqlException: Connection refused"
    }
  }
}
```

### Kubernetes Configuration

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
    - name: api
      livenessProbe:
        httpGet:
          path: /health
          port: 8080
        initialDelaySeconds: 5
        periodSeconds: 10

      readinessProbe:
        httpGet:
          path: /health/ready
          port: 8080
        initialDelaySeconds: 10
        periodSeconds: 5
```

### Custom Health Checks

Add your own health checks:

```csharp
public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly IExternalService _externalService;

    public ExternalServiceHealthCheck(IExternalService externalService)
    {
        _externalService = externalService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _externalService.PingAsync(cancellationToken);

            return isHealthy
                ? HealthCheckResult.Healthy("External service is responsive")
                : HealthCheckResult.Degraded("External service is slow");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "External service is unavailable",
                ex);
        }
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<ExternalServiceHealthCheck>(
        "external-service",
        tags: new[] { "ready" });
```

---

## Middleware Order

The correct middleware order is important:

```csharp
var app = builder.Build();

// 1. Observability first (captures all requests)
app.UseDeclareApiObservability();

// 2. Authentication
app.UseAuthentication();

// 3. Authorization
app.UseAuthorization();

// 4. Policies (rate limiting, caching)
app.UseDeclareApiPolicies();

// 5. Endpoints last
app.MapDeclareApi();
app.MapDeclareApiHealthChecks();

app.Run();
```

---

## Integration with APM Tools

### Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry();

// Correlation IDs are automatically included in telemetry
```

### Datadog

```csharp
builder.Services.AddDatadogTracing(options =>
{
    options.ServiceName = "my-api";
});

// Inject correlation ID into Datadog spans
public class DatadogCorrelationMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrEmpty(correlationId))
        {
            Tracer.Instance.ActiveScope?.Span.SetTag("correlation_id", correlationId);
        }
        await next(context);
    }
}
```

### Serilog

```csharp
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()  // Custom enricher
        .WriteTo.Console()
        .WriteTo.Seq("http://localhost:5341");
});
```

---

## Best Practices

1. **Always use correlation IDs** - Essential for debugging distributed systems
2. **Log at appropriate levels** - Info for normal, Warning for issues, Error for failures
3. **Include context** - Add relevant data to logs (user ID, entity ID, etc.)
4. **Monitor health checks** - Set up alerts on health check failures
5. **Don't log sensitive data** - Exclude passwords, tokens, PII
6. **Use structured logging** - Enables querying and aggregation
