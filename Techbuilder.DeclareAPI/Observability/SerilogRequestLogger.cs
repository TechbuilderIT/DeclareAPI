using Microsoft.Extensions.Logging;
using Techbuilder.DeclareAPI.Core.Observability;

namespace Techbuilder.DeclareAPI.Observability;

/// <summary>
/// Default implementation of IRequestLogger using Microsoft.Extensions.Logging.
/// Works with any ILogger provider including Serilog.
/// </summary>
public class DefaultRequestLogger : IRequestLogger
{
    private readonly ILogger<DefaultRequestLogger> _logger;

    public DefaultRequestLogger(ILogger<DefaultRequestLogger> logger)
    {
        _logger = logger;
    }

    public void LogRequestStart(RequestLogContext context)
    {
        _logger.LogInformation(
            "[{CorrelationId}] Request started: {Method} {Path}{QueryString} from {ClientIp}",
            context.CorrelationId,
            context.Method,
            context.Path,
            context.QueryString ?? string.Empty,
            context.ClientIp ?? "unknown");
    }

    public void LogRequestEnd(RequestLogContext context, ResponseLogContext response)
    {
        var logLevel = response.StatusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "[{CorrelationId}] Request completed: {Method} {Path} -> {StatusCode} in {DurationMs}ms ({ContentLength} bytes)",
            context.CorrelationId,
            context.Method,
            context.Path,
            response.StatusCode,
            response.DurationMs,
            response.ContentLength ?? 0);
    }

    public void LogRequestError(RequestLogContext context, Exception exception)
    {
        _logger.LogError(exception,
            "[{CorrelationId}] Request failed: {Method} {Path} - {ExceptionType}: {ExceptionMessage}",
            context.CorrelationId,
            context.Method,
            context.Path,
            exception.GetType().Name,
            exception.Message);
    }
}
