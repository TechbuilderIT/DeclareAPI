namespace DeclareAPI.Agent.Core.Models;

/// <summary>
/// Type of relationship between entities.
/// </summary>
public enum RelationshipType
{
    /// <summary>One-to-one relationship (1:1)</summary>
    OneToOne,

    /// <summary>One-to-many relationship (1:N)</summary>
    OneToMany,

    /// <summary>Many-to-many relationship (N:M)</summary>
    ManyToMany
}

/// <summary>
/// Represents a relationship between two entities.
/// </summary>
public class Relationship
{
    /// <summary>
    /// Name of the source entity (the "one" side in 1:N).
    /// </summary>
    public string FromEntity { get; set; } = "";

    /// <summary>
    /// Name of the target entity (the "many" side in 1:N).
    /// </summary>
    public string ToEntity { get; set; } = "";

    /// <summary>
    /// Type of relationship.
    /// </summary>
    public RelationshipType Type { get; set; }

    /// <summary>
    /// Name of the foreign key field in the target entity.
    /// </summary>
    public string? ForeignKeyField { get; set; }

    /// <summary>
    /// Name of the junction table for many-to-many relationships.
    /// </summary>
    public string? JunctionTable { get; set; }

    /// <summary>
    /// Optional description/label for the relationship.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the relationship is required (NOT NULL FK).
    /// </summary>
    public bool IsRequired { get; set; }

    public override string ToString() => $"{FromEntity} -> {ToEntity} ({Type})";
}
