using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Core.Abstractions;

/// <summary>
/// Interface for document parsers that extract project specifications from various formats.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Gets the supported file extensions for this parser.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Checks if this parser can handle the given file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>True if this parser can handle the file.</returns>
    bool CanParse(string filePath);

    /// <summary>
    /// Parses the document and extracts a project specification.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <param name="sourcePath">Optional source file path for reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted project specification.</returns>
    Task<ProjectSpec> ParseAsync(string content, string? sourcePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a document from a file path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted project specification.</returns>
    Task<ProjectSpec> ParseFileAsync(string filePath, CancellationToken cancellationToken = default);
}
