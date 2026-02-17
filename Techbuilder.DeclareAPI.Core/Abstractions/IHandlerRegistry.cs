namespace Techbuilder.DeclareAPI.Core.Abstractions;

/// <summary>
/// Registry for resolving custom handlers by name.
/// </summary>
public interface IHandlerRegistry
{
    /// <summary>
    /// Gets handler metadata by name.
    /// </summary>
    /// <param name="handlerName">The name of the handler (from YAML config or HandlerName attribute).</param>
    /// <returns>Handler metadata if found, null otherwise.</returns>
    HandlerMetadata? GetHandler(string handlerName);

    /// <summary>
    /// Gets all registered handlers.
    /// </summary>
    IReadOnlyCollection<HandlerMetadata> GetAllHandlers();

    /// <summary>
    /// Checks if a handler with the given name exists.
    /// </summary>
    bool HasHandler(string handlerName);
}

/// <summary>
/// Metadata about a registered custom handler.
/// </summary>
public record HandlerMetadata
{
    /// <summary>
    /// The name used to reference this handler in configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The concrete handler type.
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    /// The request type (TRequest in ICustomHandler{TRequest, TResponse}).
    /// </summary>
    public required Type RequestType { get; init; }

    /// <summary>
    /// The response type (TResponse in ICustomHandler{TRequest, TResponse}).
    /// </summary>
    public required Type ResponseType { get; init; }
}
