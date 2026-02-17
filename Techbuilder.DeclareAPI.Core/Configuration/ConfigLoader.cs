using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Techbuilder.DeclareAPI.Core.Configuration;

/// <summary>
/// Loads and parses DeclareAPI configuration from YAML files.
/// </summary>
public class ConfigLoader
{
    private static readonly Regex EnvVarPattern = new(@"\$\{(\w+)\}", RegexOptions.Compiled);

    private readonly IDeserializer _deserializer;

    public ConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads configuration from a YAML file path.
    /// </summary>
    public DeclareApiConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);

        var yaml = File.ReadAllText(filePath);
        return LoadFromYaml(yaml);
    }

    /// <summary>
    /// Loads configuration from a YAML string.
    /// </summary>
    public DeclareApiConfig LoadFromYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("YAML content cannot be empty", nameof(yaml));

        // Expand environment variables before parsing
        var expandedYaml = ExpandEnvironmentVariables(yaml);

        var config = _deserializer.Deserialize<DeclareApiConfig>(expandedYaml);

        ValidateConfig(config);

        return config;
    }

    /// <summary>
    /// Expands ${ENV_VAR} patterns in the YAML content.
    /// </summary>
    private string ExpandEnvironmentVariables(string yaml)
    {
        return EnvVarPattern.Replace(yaml, match =>
        {
            var envVarName = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            return envValue ?? match.Value; // Keep original if not found
        });
    }

    /// <summary>
    /// Validates the loaded configuration.
    /// </summary>
    private void ValidateConfig(DeclareApiConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(config.Version))
            errors.Add("Version is required");

        if (string.IsNullOrEmpty(config.Database.Connection))
            errors.Add("Database connection string is required");

        if (!IsValidProvider(config.Database.Provider))
            errors.Add($"Invalid database provider: {config.Database.Provider}. Supported: postgresql, sqlserver, mysql, sqlite");

        foreach (var (entityName, entity) in config.Entities)
        {
            if (entity.Endpoints.Count == 0)
                errors.Add($"Entity '{entityName}' has no endpoints defined");

            foreach (var (endpointName, endpoint) in entity.Endpoints)
            {
                ValidateEndpoint(entityName, endpointName, endpoint, errors);
            }
        }

        if (errors.Count > 0)
            throw new ConfigurationException(errors);
    }

    private void ValidateEndpoint(string entityName, string endpointName, EndpointConfig endpoint, List<string> errors)
    {
        var prefix = $"Entity '{entityName}', endpoint '{endpointName}'";

        if (string.IsNullOrEmpty(endpoint.Path))
            errors.Add($"{prefix}: Path is required");

        if (!IsValidHttpMethod(endpoint.Method))
            errors.Add($"{prefix}: Invalid HTTP method '{endpoint.Method}'");

        // If not a custom handler, source is required
        if (!endpoint.IsCustomHandler && endpoint.Source == null)
            errors.Add($"{prefix}: Either 'handler' or 'source' must be specified");

        if (endpoint.Source != null && string.IsNullOrEmpty(endpoint.Source.Name))
            errors.Add($"{prefix}: Source name is required");
    }

    private static bool IsValidProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "postgresql" or "sqlserver" or "mysql" or "sqlite" => true,
            _ => false
        };
    }

    private static bool IsValidHttpMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" or "POST" or "PUT" or "PATCH" or "DELETE" => true,
            _ => false
        };
    }
}

/// <summary>
/// Exception thrown when configuration validation fails.
/// </summary>
public class ConfigurationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ConfigurationException(IEnumerable<string> errors)
        : base($"Configuration validation failed:\n{string.Join("\n", errors)}")
    {
        Errors = errors.ToList();
    }
}
