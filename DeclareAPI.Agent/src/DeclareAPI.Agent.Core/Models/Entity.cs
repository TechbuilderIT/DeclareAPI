namespace DeclareAPI.Agent.Core.Models;

/// <summary>
/// Represents an entity (table) in the data model.
/// </summary>
public class Entity
{
    /// <summary>
    /// Entity name in PascalCase (e.g., "User", "OrderItem").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Table name in snake_case (e.g., "users", "order_items").
    /// </summary>
    public string TableName { get; set; } = "";

    /// <summary>
    /// Optional description/comment for the entity.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of fields/columns in this entity.
    /// </summary>
    public List<Field> Fields { get; set; } = new();

    /// <summary>
    /// Fields that compose the primary key.
    /// </summary>
    public List<string> PrimaryKeyFields { get; set; } = new();

    /// <summary>
    /// Unique constraints (list of field names per constraint).
    /// </summary>
    public List<List<string>> UniqueConstraints { get; set; } = new();

    /// <summary>
    /// Whether to automatically add created_at/updated_at timestamps.
    /// </summary>
    public bool HasTimestamps { get; set; } = true;

    /// <summary>
    /// Gets the primary key field (assumes single-column PK).
    /// </summary>
    public Field? GetPrimaryKey() => Fields.FirstOrDefault(f => f.IsPrimaryKey);

    /// <summary>
    /// Gets all foreign key fields.
    /// </summary>
    public IEnumerable<Field> GetForeignKeys() => Fields.Where(f => f.IsForeignKey);

    public override string ToString() => $"{Name} ({Fields.Count} fields)";
}
