namespace DeclareAPI.Agent.Core.Abstractions;

/// <summary>
/// AI model information.
/// </summary>
public class AIModel
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long? SizeBytes { get; set; }
}

/// <summary>
/// Interface for AI providers (Ollama, OpenAI, etc.).
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks if the provider is available and configured.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available models.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available models.</returns>
    Task<IReadOnlyList<AIModel>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response from a prompt.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="model">Model name to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response.</returns>
    Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response with a system prompt.
    /// </summary>
    /// <param name="systemPrompt">The system/context prompt.</param>
    /// <param name="userPrompt">The user prompt.</param>
    /// <param name="model">Model name to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response.</returns>
    Task<string> GenerateWithContextAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response with streaming output.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="model">Model name to use.</param>
    /// <param name="onToken">Callback for each generated token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete generated response.</returns>
    Task<string> GenerateStreamingAsync(
        string prompt,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default);
}
