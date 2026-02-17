using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Core.Abstractions;

/// <summary>
/// Result of a code generation operation.
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// Whether the generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The generated content.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Suggested file name for the output.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// Any warnings during generation.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }

    public static GenerationResult Ok(string content, string fileName) =>
        new() { Success = true, Content = content, FileName = fileName };

    public static GenerationResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Interface for code generators that produce output files from project specifications.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Gets the name of this generator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this generator produces.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Generates output from the project specification.
    /// </summary>
    /// <param name="spec">The project specification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generation result.</returns>
    Task<GenerationResult> GenerateAsync(ProjectSpec spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the specification before generation.
    /// </summary>
    /// <param name="spec">The project specification.</param>
    /// <returns>List of validation errors (empty if valid).</returns>
    IEnumerable<string> Validate(ProjectSpec spec);
}
