using YamlDotNet.Serialization;

namespace Techbuilder.DeclareAPI.Core.Configuration;

/// <summary>
/// Root configuration for DeclareAPI. Represents the entire declareapi.yaml file.
/// </summary>
public class DeclareApiConfig
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "1.0";

    [YamlMember(Alias = "database")]
    public DatabaseConfig Database { get; set; } = new();

    [YamlMember(Alias = "settings")]
    public SettingsConfig Settings { get; set; } = new();

    [YamlMember(Alias = "entities")]
    public Dictionary<string, EntityConfig> Entities { get; set; } = new();
}

/// <summary>
/// Database connection configuration.
/// </summary>
public class DatabaseConfig
{
    [YamlMember(Alias = "provider")]
    public string Provider { get; set; } = "postgresql";

    [YamlMember(Alias = "connection")]
    public string Connection { get; set; } = string.Empty;
}

/// <summary>
/// General API settings.
/// </summary>
public class SettingsConfig
{
    [YamlMember(Alias = "base_path")]
    public string BasePath { get; set; } = "/api";

    [YamlMember(Alias = "default_page_size")]
    public int DefaultPageSize { get; set; } = 25;

    [YamlMember(Alias = "max_page_size")]
    public int MaxPageSize { get; set; } = 100;

    [YamlMember(Alias = "generate_openapi")]
    public bool GenerateOpenApi { get; set; } = true;
}
