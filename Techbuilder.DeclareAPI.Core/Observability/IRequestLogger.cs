namespace Techbuilder.DeclareAPI.Core.Observability;

/// <summary>
/// Abstraction for request/response logging.
/// </summary>
public interface IRequestLogger
{
    /// <summary>
    /// Logs the start of a request.
    /// </summary>
    void LogRequestStart(RequestLogContext context);

    /// <summary>
    /// Logs the completion of a request.
    /// </summary>
    void LogRequestEnd(RequestLogContext context, ResponseLogContext response);

    /// <summary>
    /// Logs an exception during request processing.
    /// </summary>
    void LogRequestError(RequestLogContext context, Exception exception);
}

/// <summary>
/// Context information about an incoming request.
/// </summary>
public record RequestLogContext
{
    public required string CorrelationId { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? QueryString { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Context information about a response.
/// </summary>
public record ResponseLogContext
{
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
    public long? ContentLength { get; init; }
    public string? ContentType { get; init; }
}
