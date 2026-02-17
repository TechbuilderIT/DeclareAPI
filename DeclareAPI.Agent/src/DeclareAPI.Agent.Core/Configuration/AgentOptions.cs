namespace DeclareAPI.Agent.Core.Configuration;

/// <summary>
/// Configuration options for the DeclareAPI Agent.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Ollama server URL.
    /// </summary>
    public string OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default AI model to use.
    /// Recommended: qwen2.5-coder:3b (96/100 quality, 63.7 tok/s on RTX 3050 6GB)
    /// </summary>
    public string DefaultModel { get; set; } = "qwen2.5-coder:3b";

    /// <summary>
    /// Output directory for generated files.
    /// </summary>
    public string OutputDirectory { get; set; } = "./output";

    /// <summary>
    /// Whether to overwrite existing files.
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// Whether to run in interactive mode (wizard).
    /// </summary>
    public bool InteractiveMode { get; set; } = true;

    /// <summary>
    /// Whether to show verbose output.
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// Timeout for AI generation in seconds.
    /// </summary>
    public int GenerationTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum retry attempts for AI generation.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Generation options for a specific run.
/// </summary>
public class GenerationOptions
{
    /// <summary>
    /// Input file path (ER diagram, etc.).
    /// </summary>
    public string InputFile { get; set; } = "";

    /// <summary>
    /// Project name for the generated API.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Output directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "./output";

    /// <summary>
    /// AI model to use.
    /// </summary>
    public string Model { get; set; } = "qwen2.5-coder:3b";

    /// <summary>
    /// Entities to include (empty = all).
    /// </summary>
    public List<string> IncludeEntities { get; set; } = new();

    /// <summary>
    /// Entities to exclude.
    /// </summary>
    public List<string> ExcludeEntities { get; set; } = new();

    /// <summary>
    /// Whether to generate YAML configuration.
    /// </summary>
    public bool GenerateYaml { get; set; } = true;

    /// <summary>
    /// Whether to generate SQL DDL.
    /// </summary>
    public bool GenerateSql { get; set; } = true;

    /// <summary>
    /// Database provider.
    /// </summary>
    public string DatabaseProvider { get; set; } = "postgresql";
}
