namespace Techbuilder.DeclareAPI.Core.Observability;

/// <summary>
/// Provides access to the correlation ID for the current request.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the correlation ID for the current request.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Sets the correlation ID for the current request.
    /// </summary>
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// Context for correlation ID using AsyncLocal for thread-safe access.
/// </summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public string? CorrelationId => _correlationId.Value;

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
