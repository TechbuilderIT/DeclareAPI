using System.Text;
using System.Text.RegularExpressions;
using DeclareAPI.Agent.AI.Prompts;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Generators;

/// <summary>
/// AI-enhanced SQL generator that uses LLM to suggest better indexes.
/// </summary>
public partial class AiEnhancedSqlGenerator : ICodeGenerator
{
    private readonly PostgreSqlGenerator _baseGenerator;
    private readonly IAIProvider _aiProvider;
    private readonly string _model;
    private readonly Action<string>? _onProgress;

    public string Name => "PostgreSQL DDL (AI-Enhanced)";
    public string Description => "Generates init.sql with AI-optimized indexes";

    public AiEnhancedSqlGenerator(
        IAIProvider aiProvider,
        string model,
        Action<string>? onProgress = null)
    {
        _baseGenerator = new PostgreSqlGenerator();
        _aiProvider = aiProvider;
        _model = model;
        _onProgress = onProgress;
    }

    public IEnumerable<string> Validate(ProjectSpec spec) => _baseGenerator.Validate(spec);

    public async Task<GenerationResult> GenerateAsync(ProjectSpec spec, CancellationToken cancellationToken = default)
    {
        var errors = Validate(spec).ToList();
        if (errors.Any())
        {
            return GenerationResult.Fail(string.Join("; ", errors));
        }

        try
        {
            var sql = await GenerateSqlWithAiAsync(spec, cancellationToken);
            return GenerationResult.Ok(sql, "init.sql");
        }
        catch (Exception ex)
        {
            // Fallback to base generator on AI failure
            _onProgress?.Invoke($"AI enhancement failed: {ex.Message}. Using rule-based generation.");
            return await _baseGenerator.GenerateAsync(spec, cancellationToken);
        }
    }

    private async Task<string> GenerateSqlWithAiAsync(ProjectSpec spec, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine($"-- {spec.Name} - Database Schema");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- Enhanced with AI-optimized indexes");
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

        // Create AI-enhanced indexes
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine("-- INDEXES (AI-optimized)");
        sb.AppendLine("-- ============================================================================");
        sb.AppendLine();

        var totalEntities = orderedEntities.Count;
        var current = 0;

        foreach (var entity in orderedEntities)
        {
            current++;
            _onProgress?.Invoke($"Optimizing indexes {current}/{totalEntities}: {entity.TableName}");

            await GenerateAiIndexesAsync(sb, entity, cancellationToken);
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

    private async Task GenerateAiIndexesAsync(
        StringBuilder sb,
        Entity entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var indexes = await GetAiSuggestedIndexesAsync(entity, cancellationToken);
            foreach (var index in indexes)
            {
                sb.AppendLine(index);
            }
        }
        catch
        {
            // Fallback to rule-based indexes
            GenerateStandardIndexes(sb, entity);
        }
    }

    private async Task<List<string>> GetAiSuggestedIndexesAsync(
        Entity entity,
        CancellationToken cancellationToken)
    {
        var columns = entity.Fields.Select(f => (
            f.Name,
            f.Type,
            f.IsPrimaryKey,
            f.IsForeignKey,
            f.IsUnique
        ));

        var userPrompt = GenerationPrompts.CreateIndexPrompt(entity.TableName, columns);

        var response = await _aiProvider.GenerateWithContextAsync(
            GenerationPrompts.SqlIndexSystemPrompt,
            userPrompt,
            _model,
            cancellationToken);

        return ParseIndexResponse(response, entity.TableName);
    }

    private static List<string> ParseIndexResponse(string response, string tableName)
    {
        var indexes = new List<string>();
        var addedIndexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Parse CREATE INDEX statements from response
        var matches = CreateIndexPattern().Matches(response);
        foreach (Match match in matches)
        {
            var indexStatement = match.Value.Trim();

            // Validate it references the correct table
            if (indexStatement.Contains(tableName, StringComparison.OrdinalIgnoreCase))
            {
                // Extract index name to avoid duplicates
                var nameMatch = IndexNamePattern().Match(indexStatement);
                if (nameMatch.Success)
                {
                    var indexName = nameMatch.Groups["name"].Value;
                    if (!addedIndexNames.Contains(indexName))
                    {
                        // Ensure it ends with semicolon
                        if (!indexStatement.EndsWith(';'))
                            indexStatement += ";";

                        indexes.Add(indexStatement);
                        addedIndexNames.Add(indexName);
                    }
                }
            }
        }

        // If AI didn't provide indexes, return empty (will fallback to standard)
        return indexes;
    }

    private static void GenerateStandardIndexes(StringBuilder sb, Entity entity)
    {
        foreach (var field in entity.Fields)
        {
            if (field.IsPrimaryKey) continue;

            if (field.IsForeignKey || field.Name.EndsWith("_id"))
            {
                sb.AppendLine($"CREATE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
            }
            else if (IsIndexableField(field))
            {
                if (field.IsUnique)
                {
                    sb.AppendLine($"CREATE UNIQUE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
                }
                else
                {
                    sb.AppendLine($"CREATE INDEX idx_{entity.TableName}_{field.Name} ON {entity.TableName}({field.Name});");
                }
            }
        }

        if (entity.HasTimestamps || entity.Fields.Any(f => f.Name == "created_at"))
        {
            sb.AppendLine($"CREATE INDEX idx_{entity.TableName}_created_at ON {entity.TableName}(created_at DESC);");
        }
    }

    #region Standard Generation Methods (from base)

    private static void GenerateCreateTable(StringBuilder sb, Entity entity)
    {
        sb.AppendLine($"CREATE TABLE {entity.TableName} (");

        var columns = new List<string>();

        foreach (var field in entity.Fields)
        {
            columns.Add(GenerateColumnDefinition(field));
        }

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

        if (!string.IsNullOrEmpty(field.DefaultValue))
        {
            parts.Add($"DEFAULT {field.DefaultValue}");
        }

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
        var result = new List<Entity>();
        var remaining = new HashSet<string>(entities.Select(e => e.Name));
        var added = new HashSet<string>();

        while (remaining.Any())
        {
            var batch = new List<Entity>();

            foreach (var entityName in remaining.ToList())
            {
                var entity = entities.First(e => e.Name == entityName);

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

    #endregion

    [GeneratedRegex(@"CREATE\s+(UNIQUE\s+)?INDEX\s+\w+\s+ON\s+\w+\s*\([^)]+\)(\s+WHERE[^;]+)?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateIndexPattern();

    [GeneratedRegex(@"INDEX\s+(?<name>\w+)\s+ON", RegexOptions.IgnoreCase)]
    private static partial Regex IndexNamePattern();
}
