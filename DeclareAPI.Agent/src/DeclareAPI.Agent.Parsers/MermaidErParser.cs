using System.Text.RegularExpressions;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Parsers;

/// <summary>
/// Parser for Mermaid ER diagrams.
/// Extracts entities, fields, and relationships from erDiagram blocks.
/// </summary>
public partial class MermaidErParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".mermaid", ".mmd", ".md"];

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<ProjectSpec> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await ParseAsync(content, filePath, cancellationToken);
    }

    public Task<ProjectSpec> ParseAsync(string content, string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        var spec = new ProjectSpec
        {
            Name = ExtractProjectName(sourcePath),
            SourceFile = sourcePath
        };

        // Remove comments
        var cleanedContent = RemoveComments(content);

        // Parse entities
        var entities = ParseEntities(cleanedContent);
        spec.Entities.AddRange(entities);

        // Parse relationships
        var relationships = ParseRelationships(cleanedContent, entities);
        spec.Relationships.AddRange(relationships);

        // Apply foreign key information from relationships
        ApplyForeignKeys(spec);

        return Task.FromResult(spec);
    }

    private static string ExtractProjectName(string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return "GeneratedProject";

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        // Convert "er-diagram" to "ErDiagram"
        return ToPascalCase(fileName.Replace("-", " ").Replace("_", " "));
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w =>
            char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    private static string RemoveComments(string content)
    {
        // Remove %% comments (line comments in Mermaid)
        return CommentPattern().Replace(content, "");
    }

    #region Entity Parsing

    private static List<Entity> ParseEntities(string content)
    {
        var entities = new List<Entity>();
        var matches = EntityBlockPattern().Matches(content);

        foreach (Match match in matches)
        {
            var entityName = match.Groups["name"].Value.Trim();
            var fieldsBlock = match.Groups["fields"].Value;

            var entity = new Entity
            {
                // Convert snake_case/kebab-case to PascalCase
                Name = ToPascalCase(entityName.Replace("_", " ").Replace("-", " ")),
                TableName = ToSnakeCase(entityName)
            };

            var fields = ParseFields(fieldsBlock);
            entity.Fields.AddRange(fields);

            // Set primary key fields
            entity.PrimaryKeyFields.AddRange(
                fields.Where(f => f.IsPrimaryKey).Select(f => f.Name));

            // Add timestamps if not already present
            if (!fields.Any(f => f.Name.Equals("created_at", StringComparison.OrdinalIgnoreCase)))
            {
                entity.HasTimestamps = true;
            }

            entities.Add(entity);
        }

        return entities;
    }

    private static List<Field> ParseFields(string fieldsBlock)
    {
        var fields = new List<Field>();
        var lines = fieldsBlock.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            var field = ParseField(trimmedLine);
            if (field != null)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static Field? ParseField(string line)
    {
        // Pattern: type name [PK|FK|UK|FK_UK|PK_FK] ["description"]
        var match = FieldPattern().Match(line);
        if (!match.Success) return null;

        var dataType = match.Groups["type"].Value.Trim();
        var fieldName = match.Groups["name"].Value.Trim();
        var constraints = match.Groups["constraints"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();

        var field = new Field
        {
            Name = fieldName,
            Type = NormalizeType(dataType),
            Description = string.IsNullOrEmpty(description) ? null : description
        };

        // Parse constraints
        ParseConstraints(field, constraints);

        // Infer nullability
        field.IsNullable = !field.IsPrimaryKey && !constraints.Contains("UK");

        // Parse max length for varchar
        var lengthMatch = VarcharLengthPattern().Match(dataType);
        if (lengthMatch.Success && int.TryParse(lengthMatch.Groups[1].Value, out var length))
        {
            field.MaxLength = length;
        }

        // Set default values based on type
        SetDefaultValues(field);

        return field;
    }

    private static void ParseConstraints(Field field, string constraints)
    {
        if (string.IsNullOrEmpty(constraints)) return;

        constraints = constraints.ToUpperInvariant();

        if (constraints.Contains("PK"))
        {
            field.IsPrimaryKey = true;
            field.IsNullable = false;
        }

        if (constraints.Contains("FK"))
        {
            field.IsForeignKey = true;
        }

        if (constraints.Contains("UK") || constraints.Contains("_UK"))
        {
            field.IsUnique = true;
        }
    }

    private static void SetDefaultValues(Field field)
    {
        // Auto-generate UUIDs for primary keys
        if (field.IsPrimaryKey && field.Type == "uuid")
        {
            field.DefaultValue = "gen_random_uuid()";
            field.IsAutoGenerated = true;
        }

        // Timestamps default to NOW()
        if (field.Type == "timestamp" &&
            (field.Name.EndsWith("_at") || field.Name == "created_at" || field.Name == "updated_at"))
        {
            field.DefaultValue = "NOW()";
        }
    }

    private static string NormalizeType(string type)
    {
        // Normalize Mermaid types to PostgreSQL types
        var lowerType = type.ToLowerInvariant();

        return lowerType switch
        {
            "string" => "varchar",
            "int" => "integer",
            "bool" => "boolean",
            "datetime" => "timestamp",
            "guid" => "uuid",
            "decimal" => "numeric",
            "float" or "double" => "numeric",
            "json" => "jsonb",
            "array" => "text[]",
            _ when lowerType.StartsWith("varchar") => type.ToLowerInvariant(),
            _ => type.ToLowerInvariant()
        };
    }

    #endregion

    #region Relationship Parsing

    private static List<Relationship> ParseRelationships(string content, List<Entity> entities)
    {
        var relationships = new List<Relationship>();
        var entityNames = entities.Select(e => e.TableName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = RelationshipPattern().Matches(content);

        foreach (Match match in matches)
        {
            var fromEntity = match.Groups["from"].Value.Trim();
            var leftCardinality = match.Groups["leftCard"].Value.Trim();
            var rightCardinality = match.Groups["rightCard"].Value.Trim();
            var toEntity = match.Groups["to"].Value.Trim();
            var label = match.Groups["label"].Value.Trim();

            // Skip if either entity doesn't exist
            if (!entityNames.Contains(fromEntity) || !entityNames.Contains(toEntity))
                continue;

            var relType = DetermineRelationshipType(leftCardinality, rightCardinality);

            relationships.Add(new Relationship
            {
                FromEntity = ToPascalCase(fromEntity),
                ToEntity = ToPascalCase(toEntity),
                Type = relType,
                Description = string.IsNullOrEmpty(label) ? null : label.Trim('"'),
                IsRequired = IsRelationshipRequired(rightCardinality)
            });
        }

        return relationships;
    }

    private static RelationshipType DetermineRelationshipType(string leftCard, string rightCard)
    {
        // Mermaid cardinalities:
        // || = exactly one
        // |o = zero or one
        // }| = one or more (required many)
        // }o = zero or more (optional many)
        // o{ = zero or more
        // |{ = one or more

        var hasLeftMany = leftCard.Contains('}');
        var hasRightMany = rightCard.Contains('{');

        if (hasLeftMany && hasRightMany)
            return RelationshipType.ManyToMany;

        if (!hasLeftMany && !hasRightMany)
            return RelationshipType.OneToOne;

        return RelationshipType.OneToMany;
    }

    private static bool IsRelationshipRequired(string cardinality)
    {
        // |{ or }| means required (at least one)
        // o{ or }o means optional (zero or more)
        return cardinality.Contains('|') && !cardinality.Contains('o');
    }

    #endregion

    #region Foreign Key Application

    private static void ApplyForeignKeys(ProjectSpec spec)
    {
        foreach (var relationship in spec.Relationships)
        {
            var toEntity = spec.GetEntity(relationship.ToEntity);
            if (toEntity == null) continue;

            // Find FK field in the "to" entity
            var fkField = toEntity.Fields.FirstOrDefault(f =>
                f.IsForeignKey &&
                (f.Name.EndsWith("_id") || f.Name.EndsWith("Id")));

            if (fkField != null)
            {
                // Try to match FK to the "from" entity
                var fromTableName = ToSnakeCase(relationship.FromEntity);
                var expectedFkName = $"{fromTableName}_id";

                var matchingFk = toEntity.Fields.FirstOrDefault(f =>
                    f.IsForeignKey &&
                    f.Name.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));

                if (matchingFk != null)
                {
                    matchingFk.ForeignKeyTable = ToSnakeCase(relationship.FromEntity);
                    matchingFk.ForeignKeyColumn = "id";
                    relationship.ForeignKeyField = matchingFk.Name;
                }
            }

            // For 1:1 relationships, also check the "from" entity
            if (relationship.Type == RelationshipType.OneToOne)
            {
                var fromEntity = spec.GetEntity(relationship.FromEntity);
                if (fromEntity != null)
                {
                    var toTableName = ToSnakeCase(relationship.ToEntity);
                    var expectedFkName = $"{toTableName}_id";

                    var matchingFk = fromEntity.Fields.FirstOrDefault(f =>
                        f.IsForeignKey &&
                        f.Name.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));

                    if (matchingFk != null)
                    {
                        matchingFk.ForeignKeyTable = toTableName;
                        matchingFk.ForeignKeyColumn = "id";
                    }
                }
            }
        }
    }

    #endregion

    #region Helpers

    private static string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Already snake_case
        if (text.Contains('_')) return text.ToLowerInvariant();

        // PascalCase to snake_case
        return string.Concat(text.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }

    #endregion

    #region Regex Patterns (Generated)

    [GeneratedRegex(@"%%.*$", RegexOptions.Multiline)]
    private static partial Regex CommentPattern();

    // Negative lookbehind (?<!--) ensures we don't match "o{" in relationship patterns like "||--o{"
    [GeneratedRegex(@"(?<!--)(?<name>\w+)\s*\{(?<fields>[^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex EntityBlockPattern();

    [GeneratedRegex(@"^\s*(?<type>\w+(?:\([^)]+\))?(?:\[\])?)\s+(?<name>\w+)\s*(?<constraints>(?:PK|FK|UK|PK_FK|FK_UK|_UK|_FK)*)\s*(?:""(?<description>[^""]*)"")?", RegexOptions.Multiline)]
    private static partial Regex FieldPattern();

    [GeneratedRegex(@"varchar\((\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex VarcharLengthPattern();

    [GeneratedRegex(@"(?<from>\w+)\s+(?<leftCard>[\|\}][\|o])\s*--\s*(?<rightCard>[o\|][\{\|])\s+(?<to>\w+)\s*(?::\s*(?<label>""[^""]*""|[^\n]*))?", RegexOptions.Multiline)]
    private static partial Regex RelationshipPattern();

    #endregion
}
