using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Techbuilder.DeclareAPI.Core.Observability;

namespace Techbuilder.DeclareAPI.Observability;

/// <summary>
/// Middleware that logs incoming requests and outgoing responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = correlationIdAccessor.CorrelationId ?? "unknown";

        var requestContext = new RequestLogContext
        {
            CorrelationId = correlationId,
            Method = context.Request.Method,
            Path = context.Request.Path,
            QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
            ClientIp = GetClientIp(context),
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault()
        };

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestMethod"] = requestContext.Method,
            ["RequestPath"] = requestContext.Path
        }))
        {
            try
            {
                _logger.LogInformation(
                    "Request started: {Method} {Path}{QueryString}",
                    requestContext.Method,
                    requestContext.Path,
                    requestContext.QueryString ?? string.Empty);

                await _next(context);

                stopwatch.Stop();

                var responseContext = new ResponseLogContext
                {
                    StatusCode = context.Response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ContentLength = context.Response.ContentLength,
                    ContentType = context.Response.ContentType
                };

                var logLevel = GetLogLevelForStatusCode(responseContext.StatusCode);

                _logger.Log(logLevel,
                    "Request completed: {Method} {Path} responded {StatusCode} in {DurationMs}ms",
                    requestContext.Method,
                    requestContext.Path,
                    responseContext.StatusCode,
                    responseContext.DurationMs);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex,
                    "Request failed: {Method} {Path} after {DurationMs}ms - {ExceptionMessage}",
                    requestContext.Method,
                    requestContext.Path,
                    stopwatch.ElapsedMilliseconds,
                    ex.Message);

                throw;
            }
        }
    }

    private static string? GetClientIp(HttpContext context)
    {
        // Check for forwarded headers first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs; take the first one
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static LogLevel GetLogLevelForStatusCode(int statusCode) => statusCode switch
    {
        >= 500 => LogLevel.Error,
        >= 400 => LogLevel.Warning,
        _ => LogLevel.Information
    };
}
