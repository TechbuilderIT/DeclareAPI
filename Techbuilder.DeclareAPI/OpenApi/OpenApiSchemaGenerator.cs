using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.OpenApi;

/// <summary>
/// Generates OpenAPI schemas from DeclareAPI configuration.
/// </summary>
public class OpenApiSchemaGenerator
{
    /// <summary>
    /// Generates OpenAPI schema for request body based on field configurations.
    /// </summary>
    public OpenApiSchema GenerateRequestSchema(string entityName, string endpointName, List<FieldConfig> fields)
    {
        var properties = new Dictionary<string, OpenApiSchema>();
        var required = new HashSet<string>();

        foreach (var field in fields)
        {
            var schema = FieldToSchema(field);
            properties[field.Name] = schema;

            if (field.Required)
            {
                required.Add(field.Name);
            }
        }

        return new OpenApiSchema
        {
            Type = "object",
            Description = $"Request body for {entityName} {endpointName}",
            Properties = properties,
            Required = required
        };
    }

    /// <summary>
    /// Generates OpenAPI schema for a single field.
    /// </summary>
    public OpenApiSchema FieldToSchema(FieldConfig field)
    {
        var schema = new OpenApiSchema
        {
            Description = $"Field: {field.Name}"
        };

        switch (field.FieldType)
        {
            case FieldType.String:
                schema.Type = "string";
                if (field.Max.HasValue) schema.MaxLength = field.Max.Value;
                if (field.Min.HasValue) schema.MinLength = field.Min.Value;
                if (!string.IsNullOrEmpty(field.Pattern)) schema.Pattern = field.Pattern;
                if (field.Default != null) schema.Default = new OpenApiString(field.Default.ToString()!);
                break;

            case FieldType.Int:
                schema.Type = "integer";
                schema.Format = "int32";
                if (field.Max.HasValue) schema.Maximum = field.Max.Value;
                if (field.Min.HasValue) schema.Minimum = field.Min.Value;
                if (field.Default != null) schema.Default = new OpenApiInteger(Convert.ToInt32(field.Default));
                break;

            case FieldType.Long:
                schema.Type = "integer";
                schema.Format = "int64";
                if (field.Max.HasValue) schema.Maximum = field.Max.Value;
                if (field.Min.HasValue) schema.Minimum = field.Min.Value;
                if (field.Default != null) schema.Default = new OpenApiLong(Convert.ToInt64(field.Default));
                break;

            case FieldType.Decimal:
                schema.Type = "number";
                schema.Format = "decimal";
                if (field.Max.HasValue) schema.Maximum = field.Max.Value;
                if (field.Min.HasValue) schema.Minimum = field.Min.Value;
                break;

            case FieldType.Bool:
                schema.Type = "boolean";
                if (field.Default != null) schema.Default = new OpenApiBoolean(Convert.ToBoolean(field.Default));
                break;

            case FieldType.Date:
                schema.Type = "string";
                schema.Format = "date";
                break;

            case FieldType.DateTime:
                schema.Type = "string";
                schema.Format = "date-time";
                break;

            case FieldType.Uuid:
                schema.Type = "string";
                schema.Format = "uuid";
                break;

            case FieldType.Json:
                schema.Type = "object";
                schema.AdditionalPropertiesAllowed = true;
                break;
        }

        return schema;
    }

    /// <summary>
    /// Generates OpenAPI schema for filter query parameters.
    /// </summary>
    public List<OpenApiParameter> GenerateFilterParameters(List<FilterConfig> filters)
    {
        var parameters = new List<OpenApiParameter>();

        foreach (var filter in filters)
        {
            var param = new OpenApiParameter
            {
                Name = filter.Field,
                In = ParameterLocation.Query,
                Required = false,
                Description = GetFilterDescription(filter),
                Schema = GetFilterSchema(filter)
            };
            parameters.Add(param);
        }

        return parameters;
    }

    /// <summary>
    /// Generates pagination parameters.
    /// </summary>
    public List<OpenApiParameter> GeneratePaginationParameters(int defaultPageSize, int maxPageSize)
    {
        return new List<OpenApiParameter>
        {
            new OpenApiParameter
            {
                Name = "page",
                In = ParameterLocation.Query,
                Required = false,
                Description = "Page number (1-based)",
                Schema = new OpenApiSchema
                {
                    Type = "integer",
                    Minimum = 1,
                    Default = new OpenApiInteger(1)
                }
            },
            new OpenApiParameter
            {
                Name = "pageSize",
                In = ParameterLocation.Query,
                Required = false,
                Description = $"Number of items per page (max: {maxPageSize})",
                Schema = new OpenApiSchema
                {
                    Type = "integer",
                    Minimum = 1,
                    Maximum = maxPageSize,
                    Default = new OpenApiInteger(defaultPageSize)
                }
            }
        };
    }

    /// <summary>
    /// Generates sort parameter.
    /// </summary>
    public OpenApiParameter? GenerateSortParameter(List<string>? allowedFields)
    {
        if (allowedFields == null || allowedFields.Count == 0)
            return null;

        return new OpenApiParameter
        {
            Name = "orderBy",
            In = ParameterLocation.Query,
            Required = false,
            Description = $"Sort field. Allowed: {string.Join(", ", allowedFields)}. Append ' DESC' for descending.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = allowedFields.SelectMany(f => new IOpenApiAny[]
                {
                    new OpenApiString(f),
                    new OpenApiString($"{f} DESC")
                }).ToList()
            }
        };
    }

    /// <summary>
    /// Generates paged response schema.
    /// </summary>
    public OpenApiSchema GeneratePagedResponseSchema(string entityName)
    {
        return new OpenApiSchema
        {
            Type = "object",
            Description = $"Paged response for {entityName}",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["items"] = new OpenApiSchema
                {
                    Type = "array",
                    Items = new OpenApiSchema { Type = "object" }
                },
                ["page"] = new OpenApiSchema { Type = "integer" },
                ["pageSize"] = new OpenApiSchema { Type = "integer" },
                ["totalCount"] = new OpenApiSchema { Type = "integer" },
                ["totalPages"] = new OpenApiSchema { Type = "integer" }
            }
        };
    }

    private string GetFilterDescription(FilterConfig filter)
    {
        return filter.FilterOperator switch
        {
            FilterOperator.Equals => $"Filter by exact match on {filter.Field}",
            FilterOperator.Contains => $"Filter by substring match on {filter.Field} (case-insensitive)",
            FilterOperator.StartsWith => $"Filter by prefix match on {filter.Field}",
            FilterOperator.EndsWith => $"Filter by suffix match on {filter.Field}",
            FilterOperator.GreaterThan => $"Filter where {filter.Field} is greater than value",
            FilterOperator.GreaterThanOrEqual => $"Filter where {filter.Field} is greater than or equal to value",
            FilterOperator.LessThan => $"Filter where {filter.Field} is less than value",
            FilterOperator.LessThanOrEqual => $"Filter where {filter.Field} is less than or equal to value",
            FilterOperator.In => $"Filter by multiple values (comma-separated) on {filter.Field}",
            FilterOperator.Between => $"Filter by range on {filter.Field} (format: min,max)",
            _ => $"Filter on {filter.Field}"
        };
    }

    private OpenApiSchema GetFilterSchema(FilterConfig filter)
    {
        return filter.FilterOperator switch
        {
            FilterOperator.In => new OpenApiSchema
            {
                Type = "string",
                Description = "Comma-separated values"
            },
            FilterOperator.Between => new OpenApiSchema
            {
                Type = "string",
                Description = "Format: min,max"
            },
            _ => new OpenApiSchema { Type = "string" }
        };
    }
}
