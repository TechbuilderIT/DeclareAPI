using System.Text;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Generators;

/// <summary>
/// Generates DeclareAPI YAML configuration from a project specification.
/// This generator creates the configuration without AI assistance.
/// </summary>
public class DeclareApiYamlGenerator : ICodeGenerator
{
    public string Name => "DeclareAPI YAML";
    public string Description => "Generates declareapi.yaml configuration file";

    public Task<GenerationResult> GenerateAsync(ProjectSpec spec, CancellationToken cancellationToken = default)
    {
        var errors = Validate(spec).ToList();
        if (errors.Any())
        {
            return Task.FromResult(GenerationResult.Fail(string.Join("; ", errors)));
        }

        var yaml = GenerateYaml(spec);
        return Task.FromResult(GenerationResult.Ok(yaml, "declareapi.yaml"));
    }

    public IEnumerable<string> Validate(ProjectSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Name))
            yield return "Project name is required";

        if (!spec.Entities.Any())
            yield return "At least one entity is required";

        foreach (var entity in spec.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
                yield return $"Entity name is required";

            if (!entity.Fields.Any())
                yield return $"Entity '{entity.Name}' has no fields";

            var pk = entity.Fields.FirstOrDefault(f => f.IsPrimaryKey);
            if (pk == null)
                yield return $"Entity '{entity.Name}' has no primary key";
        }
    }

    private static string GenerateYaml(ProjectSpec spec)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# DeclareAPI Configuration for {spec.Name}");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Version
        sb.AppendLine($"version: \"{spec.Version}\"");
        sb.AppendLine();

        // Database
        sb.AppendLine("database:");
        sb.AppendLine($"  provider: {spec.Database.Provider.ToString().ToLowerInvariant()}");
        sb.AppendLine($"  connection: \"${{{spec.Database.ConnectionStringEnvVar}}}\"");
        sb.AppendLine();

        // Settings
        sb.AppendLine("settings:");
        sb.AppendLine($"  base_path: {spec.Api.BasePath}");
        sb.AppendLine($"  default_page_size: {spec.Api.DefaultPageSize}");
        sb.AppendLine($"  max_page_size: {spec.Api.MaxPageSize}");
        sb.AppendLine();

        // Entities
        sb.AppendLine("entities:");
        foreach (var entity in spec.Entities)
        {
            GenerateEntityYaml(sb, entity, spec);
        }

        return sb.ToString();
    }

    private static void GenerateEntityYaml(StringBuilder sb, Entity entity, ProjectSpec spec)
    {
        var indent = "  ";
        sb.AppendLine($"{indent}{entity.Name}:");

        if (!string.IsNullOrEmpty(entity.Description))
        {
            sb.AppendLine($"{indent}  description: \"{EscapeYamlString(entity.Description)}\"");
        }

        sb.AppendLine($"{indent}  endpoints:");

        // List endpoint
        GenerateListEndpoint(sb, entity, indent + "    ");

        // Get by ID endpoint
        GenerateGetEndpoint(sb, entity, indent + "    ");

        // Create endpoint
        GenerateCreateEndpoint(sb, entity, indent + "    ");

        // Update endpoint
        GenerateUpdateEndpoint(sb, entity, indent + "    ");

        // Delete endpoint
        GenerateDeleteEndpoint(sb, entity, indent + "    ");

        sb.AppendLine();
    }

    private static void GenerateListEndpoint(StringBuilder sb, Entity entity, string indent)
    {
        var pk = entity.GetPrimaryKey();

        sb.AppendLine($"{indent}list:");
        sb.AppendLine($"{indent}  method: GET");
        sb.AppendLine($"{indent}  path: /{entity.TableName}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");

        // Generate filters
        var filterableFields = GetFilterableFields(entity);
        if (filterableFields.Any())
        {
            sb.AppendLine($"{indent}  filters:");
            foreach (var field in filterableFields)
            {
                var op = GetFilterOperator(field);
                sb.AppendLine($"{indent}    - {{ field: {field.Name}, operator: {op} }}");
            }
        }

        sb.AppendLine($"{indent}  paginated: true");
    }

    private static void GenerateGetEndpoint(StringBuilder sb, Entity entity, string indent)
    {
        var pk = entity.GetPrimaryKey();
        var pkType = pk?.Type ?? "uuid";

        sb.AppendLine($"{indent}get:");
        sb.AppendLine($"{indent}  method: GET");
        sb.AppendLine($"{indent}  path: /{entity.TableName}/{{id}}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");
        sb.AppendLine($"{indent}  params:");
        sb.AppendLine($"{indent}    - {{ name: id, type: {pkType}, from: route }}");
    }

    private static void GenerateCreateEndpoint(StringBuilder sb, Entity entity, string indent)
    {
        var pk = entity.GetPrimaryKey();

        sb.AppendLine($"{indent}create:");
        sb.AppendLine($"{indent}  method: POST");
        sb.AppendLine($"{indent}  path: /{entity.TableName}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");

        // Generate fields with validations
        var inputFields = GetInputFields(entity, isCreate: true);
        if (inputFields.Any())
        {
            sb.AppendLine($"{indent}  fields:");
            foreach (var field in inputFields)
            {
                var fieldYaml = GenerateFieldValidation(field);
                sb.AppendLine($"{indent}    - {fieldYaml}");
            }
        }

        sb.AppendLine($"{indent}  returns: {pk?.Type ?? "uuid"}");
    }

    private static void GenerateUpdateEndpoint(StringBuilder sb, Entity entity, string indent)
    {
        var pk = entity.GetPrimaryKey();
        var pkType = pk?.Type ?? "uuid";

        sb.AppendLine($"{indent}update:");
        sb.AppendLine($"{indent}  method: PUT");
        sb.AppendLine($"{indent}  path: /{entity.TableName}/{{id}}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");
        sb.AppendLine($"{indent}  params:");
        sb.AppendLine($"{indent}    - {{ name: id, type: {pkType}, from: route }}");

        // Generate fields (all optional for update)
        var inputFields = GetInputFields(entity, isCreate: false);
        if (inputFields.Any())
        {
            sb.AppendLine($"{indent}  fields:");
            foreach (var field in inputFields)
            {
                var fieldYaml = GenerateFieldValidation(field, forUpdate: true);
                sb.AppendLine($"{indent}    - {fieldYaml}");
            }
        }
    }

    private static void GenerateDeleteEndpoint(StringBuilder sb, Entity entity, string indent)
    {
        var pk = entity.GetPrimaryKey();
        var pkType = pk?.Type ?? "uuid";

        sb.AppendLine($"{indent}delete:");
        sb.AppendLine($"{indent}  method: DELETE");
        sb.AppendLine($"{indent}  path: /{entity.TableName}/{{id}}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");
        sb.AppendLine($"{indent}  params:");
        sb.AppendLine($"{indent}    - {{ name: id, type: {pkType}, from: route }}");
    }

    #region Helpers

    private static IEnumerable<Field> GetFilterableFields(Entity entity)
    {
        // Filter on: FKs, unique keys, status/type fields, names, emails
        return entity.Fields.Where(f =>
            !f.IsPrimaryKey &&
            !IsExcludedFromFilter(f) &&
            (f.IsForeignKey ||
             f.IsUnique ||
             f.Name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("type", StringComparison.OrdinalIgnoreCase) ||
             f.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("name", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
             f.Type == "boolean"));
    }

    private static bool IsExcludedFromFilter(Field field)
    {
        var lowerName = field.Name.ToLowerInvariant();
        return lowerName.Contains("password") ||
               lowerName.Contains("hash") ||
               lowerName.Contains("token") ||
               lowerName.Contains("secret");
    }

    private static string GetFilterOperator(Field field)
    {
        if (field.Type == "boolean")
            return "equals";

        if (field.IsForeignKey || field.Name.EndsWith("_id"))
            return "equals";

        if (field.Name.Contains("status") || field.Name.Contains("type"))
            return "in";

        if (field.Type.Contains("varchar") || field.Type == "text")
            return "contains";

        if (field.Type.Contains("timestamp") || field.Type == "date")
            return "gte";

        return "equals";
    }

    private static IEnumerable<Field> GetInputFields(Entity entity, bool isCreate)
    {
        return entity.Fields.Where(f =>
            !f.IsPrimaryKey &&
            !f.IsAutoGenerated &&
            !IsAutoTimestamp(f) &&
            !IsExcludedFromInput(f));
    }

    private static bool IsAutoTimestamp(Field field)
    {
        var lowerName = field.Name.ToLowerInvariant();
        return (lowerName == "created_at" || lowerName == "updated_at") &&
               field.Type.Contains("timestamp");
    }

    private static bool IsExcludedFromInput(Field field)
    {
        var lowerName = field.Name.ToLowerInvariant();
        return lowerName.Contains("_hash") && !lowerName.Contains("password");
    }

    private static string GenerateFieldValidation(Field field, bool forUpdate = false)
    {
        var parts = new List<string>
        {
            $"name: {field.Name}",
            $"type: {MapToValidationType(field.Type)}"
        };

        // Required only for create, and only if not nullable and no default
        if (!forUpdate && !field.IsNullable && string.IsNullOrEmpty(field.DefaultValue))
        {
            parts.Add("required: true");
        }

        if (field.MaxLength.HasValue)
        {
            parts.Add($"max: {field.MaxLength.Value}");
        }

        // Add pattern for specific field names
        if (field.Name.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("pattern: email");
        }

        return "{ " + string.Join(", ", parts) + " }";
    }

    private static string MapToValidationType(string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "uuid" => "uuid",
            "integer" or "int" or "bigint" or "smallint" => "integer",
            "numeric" or "decimal" or "float" or "double" => "decimal",
            "boolean" or "bool" => "boolean",
            "date" => "date",
            "timestamp" or "timestamp with time zone" or "timestamptz" => "datetime",
            "jsonb" or "json" => "object",
            _ when dbType.StartsWith("varchar") => "string",
            _ => "string"
        };
    }

    private static string EscapeYamlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    #endregion
}
