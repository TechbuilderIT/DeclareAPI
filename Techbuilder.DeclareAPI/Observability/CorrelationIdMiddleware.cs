using Microsoft.AspNetCore.Http;
using Techbuilder.DeclareAPI.Core.Observability;

namespace Techbuilder.DeclareAPI.Observability;

/// <summary>
/// Middleware that generates or propagates correlation IDs for request tracking.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        // Try to get correlation ID from incoming request header
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

        // Generate new correlation ID if not provided
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = GenerateCorrelationId();
        }

        // Set correlation ID in accessor for downstream use
        correlationIdAccessor.SetCorrelationId(correlationId);

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Store in HttpContext.Items for easy access
        context.Items[CorrelationIdHeaderName] = correlationId;

        await _next(context);
    }

    private static string GenerateCorrelationId()
    {
        // Format: timestamp-random for sortability and uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}-{random}";
    }
}
