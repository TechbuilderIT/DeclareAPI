namespace DeclareAPI.Agent.Core.Models;

/// <summary>
/// Database provider options.
/// </summary>
public enum DatabaseProvider
{
    PostgreSQL,
    SqlServer,
    MySql,
    Sqlite
}

/// <summary>
/// Database configuration settings.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Database provider type.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;

    /// <summary>
    /// Database schema name (e.g., "public" for PostgreSQL).
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Connection string environment variable name.
    /// </summary>
    public string ConnectionStringEnvVar { get; set; } = "DB_CONNECTION";
}

/// <summary>
/// API configuration settings.
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Base path for all API endpoints (e.g., "/api").
    /// </summary>
    public string BasePath { get; set; } = "/api";

    /// <summary>
    /// Default page size for paginated endpoints.
    /// </summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// Maximum page size allowed.
    /// </summary>
    public int MaxPageSize { get; set; } = 100;
}

/// <summary>
/// Root model representing the complete project specification.
/// Consolidates all parsed information from input documents.
/// </summary>
public class ProjectSpec
{
    /// <summary>
    /// Project/API name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// API version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Optional project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// All entities in the data model.
    /// </summary>
    public List<Entity> Entities { get; set; } = new();

    /// <summary>
    /// All relationships between entities.
    /// </summary>
    public List<Relationship> Relationships { get; set; } = new();

    /// <summary>
    /// Database configuration.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <summary>
    /// API configuration.
    /// </summary>
    public ApiSettings Api { get; set; } = new();

    /// <summary>
    /// Source file path (for reference).
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Gets an entity by name (case-insensitive).
    /// </summary>
    public Entity? GetEntity(string name) =>
        Entities.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets all relationships for a given entity.
    /// </summary>
    public IEnumerable<Relationship> GetRelationships(string entityName) =>
        Relationships.Where(r =>
            r.FromEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
            r.ToEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"{Name} v{Version} ({Entities.Count} entities, {Relationships.Count} relationships)";
}
