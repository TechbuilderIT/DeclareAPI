namespace Techbuilder.DeclareAPI.Core.Abstractions;

/// <summary>
/// Interface for custom endpoint handlers (escape hatch from declarative to imperative).
/// Implement this when you need logic beyond what the declarative pipeline supports.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICustomHandler<TRequest, TResponse>
{
    /// <summary>
    /// Handles the request and returns a response.
    /// </summary>
    Task<TResponse> HandleAsync(TRequest request, IDataAccess data, CancellationToken ct);
}

/// <summary>
/// Marker interface for custom handlers.
/// Used for service registration and discovery.
/// </summary>
public interface ICustomHandler { }
