using System.Text;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Generators;

/// <summary>
/// Generates PostgreSQL DDL scripts from a project specification.
/// </summary>
public class PostgreSqlGenerator : ICodeGenerator
{
    public string Name => "PostgreSQL DDL";
    public string Description => "Generates init.sql with CREATE TABLE, indexes, and constraints";

    public Task<GenerationResult> GenerateAsync(ProjectSpec spec, CancellationToken cancellationToken = default)
    {
        var errors = Validate(spec).ToList();
        if (errors.Any())
        {
            return Task.FromResult(GenerationResult.Fail(string.Join("; ", errors)));
        }

        var sql = GenerateSql(spec);
        return Task.FromResult(GenerationResult.Ok(sql, "init.sql"));
    }

    public IEnumerable<string> Validate(ProjectSpec spec)
    {
        if (!spec.Entities.Any())
            yield return "At least one entity is required";

        foreach (var entity in spec.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.TableName))
                yield return $"Entity '{entity.Name}' has no table name";

            if (!entity.Fields.Any())
                yield return $"Entity '{entity.Name}' has no fields";
        }
    }

    private static string GenerateSql(ProjectSpec spec)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine($"-- {spec.Name} - Database Schema");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        // Extensions
        sb.AppendLine("-- Enable UUID generation");
        sb.AppendLine("CREATE EXTENSION IF NOT EXISTS \"pgcrypto\";");
        sb.AppendLine();

        // Order entities by dependency
        var orderedEntities = OrderByDependencies(spec.Entities, spec.Relationships);

        // Drop tables in reverse order
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- DROP EXISTING TABLES (reverse dependency order)");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        foreach (var entity in orderedEntities.AsEnumerable().Reverse())
        {
            sb.AppendLine($"DROP TABLE IF EXISTS {entity.TableName} CASCADE;");
        }
        sb.AppendLine();

        // Create tables
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- CREATE TABLES");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        foreach (var entity in orderedEntities)
        {
            GenerateCreateTable(sb, entity);
            sb.AppendLine();
        }

        // Create indexes
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- INDEXES");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        foreach (var entity in orderedEntities)
        {
            GenerateIndexes(sb, entity);
        }
        sb.AppendLine();

        // Create foreign keys
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- FOREIGN KEY CONSTRAINTS");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        foreach (var entity in orderedEntities)
        {
            GenerateForeignKeys(sb, entity);
        }
        sb.AppendLine();

        // Table comments
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- COMMENTS");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        foreach (var entity in orderedEntities)
        {
            GenerateComments(sb, entity);
        }

        return sb.ToString();
    }

    private static void GenerateCreateTable(StringBuilder sb, Entity entity)
    {
        sb.AppendLine($"CREATE TABLE {entity.TableName} (");

        var columns = new List<string>();

        foreach (var field in entity.Fields)
        {
            columns.Add(GenerateColumnDefinition(field));
        }

        // Add timestamps if entity has them enabled but not in fields
        if (entity.HasTimestamps)
        {
            if (!entity.Fields.Any(f => f.Name.Equals("created_at", StringComparison.OrdinalIgnoreCase)))
            {
                columns.Add("    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL");
            }
            if (!entity.Fields.Any(f => f.Name.Equals("updated_at", StringComparison.OrdinalIgnoreCase)))
            {
                columns.Add("    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL");
            }
        }

        sb.AppendLine(string.Join(",\n", columns));
        sb.AppendLine(");");
    }

    private static string GenerateColumnDefinition(Field field)
    {
        var parts = new List<string>
        {
            $"    {field.Name}",
            MapToPostgresType(field)
        };

        // Default value
        if (!string.IsNullOrEmpty(field.DefaultValue))
        {
            parts.Add($"DEFAULT {field.DefaultValue}");
        }

        // Constraints
        if (!field.IsNullable)
        {
            parts.Add("NOT NULL");
        }

        if (field.IsPrimaryKey)
        {
            parts.Add("PRIMARY KEY");
        }
        else if (field.IsUnique)
        {
            parts.Add("UNIQUE");
        }

        return string.Join(" ", parts);
    }

    private static string MapToPostgresType(Field field)
    {
        var type = field.Type.ToLowerInvariant();

        return type switch
        {
            "uuid" => "UUID",
            "varchar" when field.MaxLength.HasValue => $"VARCHAR({field.MaxLength.Value})",
            "varchar" => "VARCHAR(255)",
            "text" => "TEXT",
            "integer" or "int" => "INTEGER",
            "bigint" => "BIGINT",
            "smallint" => "SMALLINT",
            "numeric" or "decimal" => "NUMERIC",
            "boolean" or "bool" => "BOOLEAN",
            "timestamp" or "datetime" => "TIMESTAMP WITH TIME ZONE",
            "timestamp with time zone" or "timestamptz" => "TIMESTAMP WITH TIME ZONE",
            "date" => "DATE",
            "time" => "TIME",
            "jsonb" => "JSONB",
            "json" => "JSON",
            "text[]" or "text_array" => "TEXT[]",
            "inet" => "INET",
            _ when type.StartsWith("varchar(") => type.ToUpperInvariant(),
            _ when type.StartsWith("numeric(") => type.ToUpperInvariant(),
            _ => type.ToUpperInvariant()
        };
    }

    private static void GenerateIndexes(StringBuilder sb, Entity entity)
    {
        var indexes = new List<string>();

        foreach (var field in entity.Fields)
        {
            // Skip PK (already indexed)
            if (field.IsPrimaryKey) continue;

            // Index foreign keys
            if (field.IsForeignKey || field.Name.EndsWith("_id"))
            {
                indexes.Add($"CREATE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
            }
            // Index commonly searched fields
            else if (IsIndexableField(field))
            {
                if (field.IsUnique)
                {
                    indexes.Add($"CREATE UNIQUE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
                }
                else
                {
                    indexes.Add($"CREATE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
                }
            }
        }

        // Index created_at for sorting
        if (entity.HasTimestamps || entity.Fields.Any(f => f.Name == "created_at"))
        {
            indexes.Add($"CREATE INDEX idx_{entity.TableName}_created_at ON {entity.TableName}(created_at DESC);");
        }

        foreach (var index in indexes)
        {
            sb.AppendLine(index);
        }
    }

    private static bool IsIndexableField(Field field)
    {
        var name = field.Name.ToLowerInvariant();
        return name.Contains("email") ||
               name.Contains("status") ||
               name.Contains("type") ||
               name == "key" ||
               name == "name" ||
               field.IsUnique;
    }

    private static void GenerateForeignKeys(StringBuilder sb, Entity entity)
    {
        foreach (var field in entity.Fields.Where(f => f.IsForeignKey))
        {
            if (string.IsNullOrEmpty(field.ForeignKeyTable)) continue;

            var constraintName = $"fk_{entity.TableName}_{field.Name}";
            var referencedColumn = field.ForeignKeyColumn ?? "id";
            var onDelete = field.IsNullable ? "SET NULL" : "CASCADE";

            sb.AppendLine($"ALTER TABLE {entity.TableName}");
            sb.AppendLine($"    ADD CONSTRAINT {constraintName}");
            sb.AppendLine($"    FOREIGN KEY ({field.Name})");
            sb.AppendLine($"    REFERENCES {field.ForeignKeyTable}({referencedColumn})");
            sb.AppendLine($"    ON DELETE {onDelete};");
            sb.AppendLine();
        }
    }

    private static void GenerateComments(StringBuilder sb, Entity entity)
    {
        if (!string.IsNullOrEmpty(entity.Description))
        {
            sb.AppendLine($"COMMENT ON TABLE {entity.TableName} IS '{EscapeSqlString(entity.Description)}';");
        }

        foreach (var field in entity.Fields.Where(f => !string.IsNullOrEmpty(f.Description)))
        {
            sb.AppendLine($"COMMENT ON COLUMN {entity.TableName}.{field.Name} IS '{EscapeSqlString(field.Description!)}';");
        }
    }

    private static List<Entity> OrderByDependencies(List<Entity> entities, List<Relationship> relationships)
    {
        // Simple topological sort based on foreign keys
        var result = new List<Entity>();
        var remaining = new HashSet<string>(entities.Select(e => e.Name));
        var added = new HashSet<string>();

        // Find entities without dependencies first
        while (remaining.Any())
        {
            var batch = new List<Entity>();

            foreach (var entityName in remaining.ToList())
            {
                var entity = entities.First(e => e.Name == entityName);

                // Check if all FK references are already added
                var fkDependencies = entity.Fields
                    .Where(f => f.IsForeignKey && !string.IsNullOrEmpty(f.ForeignKeyTable))
                    .Select(f => entities.FirstOrDefault(e =>
                        e.TableName.Equals(f.ForeignKeyTable, StringComparison.OrdinalIgnoreCase)))
                    .Where(e => e != null)
                    .Select(e => e!.Name);

                if (fkDependencies.All(dep => added.Contains(dep) || dep == entityName))
                {
                    batch.Add(entity);
                }
            }

            // If no progress, add remaining entities (circular dependency)
            if (!batch.Any())
            {
                batch.AddRange(remaining.Select(name => entities.First(e => e.Name == name)));
            }

            foreach (var entity in batch)
            {
                result.Add(entity);
                remaining.Remove(entity.Name);
                added.Add(entity.Name);
            }
        }

        return result;
    }

    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''");
    }
}
