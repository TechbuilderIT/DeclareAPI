using System.Text;
using System.Text.RegularExpressions;
using DeclareAPI.Agent.AI.Prompts;
using DeclareAPI.Agent.Core.Abstractions;
using DeclareAPI.Agent.Core.Models;

namespace DeclareAPI.Agent.Generators;

/// <summary>
/// AI-enhanced YAML generator that uses LLM to suggest better filters and validations.
/// </summary>
public partial class AiEnhancedYamlGenerator : ICodeGenerator
{
    private readonly DeclareApiYamlGenerator _baseGenerator;
    private readonly IAIProvider _aiProvider;
    private readonly string _model;
    private readonly Action<string>? _onProgress;

    public string Name => "DeclareAPI YAML (AI-Enhanced)";
    public string Description => "Generates declareapi.yaml with AI-suggested filters and validations";

    public AiEnhancedYamlGenerator(
        IAIProvider aiProvider,
        string model,
        Action<string>? onProgress = null)
    {
        _baseGenerator = new DeclareApiYamlGenerator();
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
            var yaml = await GenerateYamlWithAiAsync(spec, cancellationToken);
            return GenerationResult.Ok(yaml, "declareapi.yaml");
        }
        catch (Exception ex)
        {
            // Fallback to base generator on AI failure
            _onProgress?.Invoke($"AI enhancement failed: {ex.Message}. Using rule-based generation.");
            return await _baseGenerator.GenerateAsync(spec, cancellationToken);
        }
    }

    private async Task<string> GenerateYamlWithAiAsync(ProjectSpec spec, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# DeclareAPI Configuration for {spec.Name}");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("# Enhanced with AI suggestions");
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

        // Entities with AI enhancement
        sb.AppendLine("entities:");

        var totalEntities = spec.Entities.Count;
        var current = 0;

        foreach (var entity in spec.Entities)
        {
            current++;
            _onProgress?.Invoke($"Enhancing entity {current}/{totalEntities}: {entity.Name}");

            await GenerateEntityYamlAsync(sb, entity, spec, cancellationToken);
        }

        return sb.ToString();
    }

    private async Task GenerateEntityYamlAsync(
        StringBuilder sb,
        Entity entity,
        ProjectSpec spec,
        CancellationToken cancellationToken)
    {
        var indent = "  ";
        sb.AppendLine($"{indent}{entity.Name}:");

        // Generate description with AI if not present
        if (string.IsNullOrEmpty(entity.Description))
        {
            try
            {
                var description = await GenerateDescriptionAsync(entity, cancellationToken);
                if (!string.IsNullOrEmpty(description))
                {
                    sb.AppendLine($"{indent}  description: \"{EscapeYamlString(description)}\"");
                }
            }
            catch
            {
                // Skip description on failure
            }
        }
        else
        {
            sb.AppendLine($"{indent}  description: \"{EscapeYamlString(entity.Description)}\"");
        }

        sb.AppendLine($"{indent}  endpoints:");

        // List endpoint with AI-suggested filters
        await GenerateListEndpointAsync(sb, entity, indent + "    ", cancellationToken);

        // Other endpoints (standard generation)
        GenerateGetEndpoint(sb, entity, indent + "    ");
        GenerateCreateEndpoint(sb, entity, indent + "    ");
        GenerateUpdateEndpoint(sb, entity, indent + "    ");
        GenerateDeleteEndpoint(sb, entity, indent + "    ");

        sb.AppendLine();
    }

    private async Task GenerateListEndpointAsync(
        StringBuilder sb,
        Entity entity,
        string indent,
        CancellationToken cancellationToken)
    {
        var pk = entity.GetPrimaryKey();

        sb.AppendLine($"{indent}list:");
        sb.AppendLine($"{indent}  method: GET");
        sb.AppendLine($"{indent}  path: /{entity.TableName}");
        sb.AppendLine($"{indent}  source:");
        sb.AppendLine($"{indent}    type: table");
        sb.AppendLine($"{indent}    name: {entity.TableName}");

        // Get AI-suggested filters
        try
        {
            var filters = await GetAiSuggestedFiltersAsync(entity, cancellationToken);
            if (filters.Any())
            {
                sb.AppendLine($"{indent}  filters:");
                foreach (var (field, op) in filters)
                {
                    sb.AppendLine($"{indent}    - {{ field: {field}, operator: {op} }}");
                }
            }
        }
        catch
        {
            // Fallback to rule-based filters
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
        }

        sb.AppendLine($"{indent}  paginated: true");
    }

    private async Task<List<(string field, string op)>> GetAiSuggestedFiltersAsync(
        Entity entity,
        CancellationToken cancellationToken)
    {
        var fields = entity.Fields
            .Where(f => !f.IsPrimaryKey && !IsExcludedFromFilter(f))
            .Select(f => (f.Name, f.Type, f.IsForeignKey));

        var userPrompt = GenerationPrompts.CreateFilterPrompt(entity.Name, entity.TableName, fields);

        var response = await _aiProvider.GenerateWithContextAsync(
            GenerationPrompts.YamlFilterSystemPrompt,
            userPrompt,
            _model,
            cancellationToken);

        return ParseFilterResponse(response);
    }

    private static List<(string field, string op)> ParseFilterResponse(string response)
    {
        var filters = new List<(string, string)>();

        // Parse YAML-like filter responses
        var matches = FilterPattern().Matches(response);
        foreach (Match match in matches)
        {
            var field = match.Groups["field"].Value.Trim();
            var op = match.Groups["op"].Value.Trim();

            if (!string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(op))
            {
                filters.Add((field, op));
            }
        }

        return filters;
    }

    private async Task<string> GenerateDescriptionAsync(Entity entity, CancellationToken cancellationToken)
    {
        var fieldNames = entity.Fields.Select(f => f.Name);
        var userPrompt = GenerationPrompts.CreateDescriptionPrompt(entity.Name, fieldNames);

        var response = await _aiProvider.GenerateWithContextAsync(
            GenerationPrompts.EntityDescriptionSystemPrompt,
            userPrompt,
            _model,
            cancellationToken);

        // Clean up response
        return response.Trim().Trim('"').Trim();
    }

    #region Standard Generation Methods (copied from base)

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

        var inputFields = GetInputFields(entity);
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

        var inputFields = GetInputFields(entity);
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

    #endregion

    #region Helpers

    private static IEnumerable<Field> GetFilterableFields(Entity entity)
    {
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
        if (field.Type == "boolean") return "equals";
        if (field.IsForeignKey || field.Name.EndsWith("_id")) return "equals";
        if (field.Name.Contains("status") || field.Name.Contains("type")) return "in";
        if (field.Type.Contains("varchar") || field.Type == "text") return "contains";
        if (field.Type.Contains("timestamp") || field.Type == "date") return "gte";
        return "equals";
    }

    private static IEnumerable<Field> GetInputFields(Entity entity)
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

        if (!forUpdate && !field.IsNullable && string.IsNullOrEmpty(field.DefaultValue))
        {
            parts.Add("required: true");
        }

        if (field.MaxLength.HasValue)
        {
            parts.Add($"max: {field.MaxLength.Value}");
        }

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

    [GeneratedRegex(@"field:\s*(?<field>\w+).*?operator:\s*(?<op>\w+)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FilterPattern();
}
