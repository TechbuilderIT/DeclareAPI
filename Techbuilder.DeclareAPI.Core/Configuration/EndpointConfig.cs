using YamlDotNet.Serialization;

namespace Techbuilder.DeclareAPI.Core.Configuration;

/// <summary>
/// Configuration for a single API endpoint.
/// </summary>
public class EndpointConfig
{
    [YamlMember(Alias = "method")]
    public string Method { get; set; } = "GET";

    [YamlMember(Alias = "path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// When set, routes to a custom C# handler instead of the declarative pipeline.
    /// </summary>
    [YamlMember(Alias = "handler")]
    public string? Handler { get; set; }

    [YamlMember(Alias = "source")]
    public SourceConfig? Source { get; set; }

    [YamlMember(Alias = "fields")]
    public List<FieldConfig>? Fields { get; set; }

    [YamlMember(Alias = "params")]
    public List<ParamConfig>? Params { get; set; }

    [YamlMember(Alias = "filters")]
    public List<FilterConfig>? Filters { get; set; }

    [YamlMember(Alias = "sort")]
    public List<string>? Sort { get; set; }

    [YamlMember(Alias = "paginated")]
    public bool Paginated { get; set; }

    [YamlMember(Alias = "returns")]
    public string? Returns { get; set; }

    /// <summary>
    /// When true, requires authentication for this endpoint.
    /// </summary>
    [YamlMember(Alias = "authorize")]
    public bool Authorize { get; set; }

    /// <summary>
    /// Named authorization policy to apply (e.g., "admin", "manager").
    /// </summary>
    [YamlMember(Alias = "policy")]
    public string? Policy { get; set; }

    /// <summary>
    /// Required roles for this endpoint.
    /// </summary>
    [YamlMember(Alias = "roles")]
    public List<string>? Roles { get; set; }

    /// <summary>
    /// Cache configuration for this endpoint.
    /// </summary>
    [YamlMember(Alias = "cache")]
    public CacheConfig? Cache { get; set; }

    /// <summary>
    /// Rate limiting configuration for this endpoint.
    /// </summary>
    [YamlMember(Alias = "rate_limit")]
    public RateLimitConfig? RateLimit { get; set; }

    /// <summary>
    /// Returns true if this endpoint uses a custom handler.
    /// </summary>
    public bool IsCustomHandler => !string.IsNullOrEmpty(Handler);

    /// <summary>
    /// Returns true if this endpoint requires any form of authorization.
    /// </summary>
    public bool RequiresAuthorization => Authorize || !string.IsNullOrEmpty(Policy) || (Roles?.Count > 0);
}

/// <summary>
/// Data source configuration (view, table, procedure, function).
/// </summary>
public class SourceConfig
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "view";

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    public SourceType SourceType => Type.ToLowerInvariant() switch
    {
        "view" => Configuration.SourceType.View,
        "table" => Configuration.SourceType.Table,
        "procedure" => Configuration.SourceType.Procedure,
        "function" => Configuration.SourceType.Function,
        _ => Configuration.SourceType.View
    };
}

public enum SourceType
{
    View,
    Table,
    Procedure,
    Function
}

/// <summary>
/// Input field configuration for POST/PUT endpoints.
/// </summary>
public class FieldConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "string";

    [YamlMember(Alias = "required")]
    public bool Required { get; set; }

    [YamlMember(Alias = "max")]
    public int? Max { get; set; }

    [YamlMember(Alias = "min")]
    public int? Min { get; set; }

    [YamlMember(Alias = "pattern")]
    public string? Pattern { get; set; }

    [YamlMember(Alias = "default")]
    public object? Default { get; set; }

    public FieldType FieldType => Type.ToLowerInvariant() switch
    {
        "string" => Configuration.FieldType.String,
        "int" => Configuration.FieldType.Int,
        "long" => Configuration.FieldType.Long,
        "decimal" => Configuration.FieldType.Decimal,
        "bool" => Configuration.FieldType.Bool,
        "date" => Configuration.FieldType.Date,
        "datetime" => Configuration.FieldType.DateTime,
        "uuid" => Configuration.FieldType.Uuid,
        "json" => Configuration.FieldType.Json,
        _ => Configuration.FieldType.String
    };
}

public enum FieldType
{
    String,
    Int,
    Long,
    Decimal,
    Bool,
    Date,
    DateTime,
    Uuid,
    Json
}

/// <summary>
/// Route/query parameter configuration.
/// </summary>
public class ParamConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "string";

    [YamlMember(Alias = "from")]
    public string From { get; set; } = "route";

    public ParamSource ParamSource => From.ToLowerInvariant() switch
    {
        "route" => Configuration.ParamSource.Route,
        "query" => Configuration.ParamSource.Query,
        "header" => Configuration.ParamSource.Header,
        _ => Configuration.ParamSource.Route
    };
}

public enum ParamSource
{
    Route,
    Query,
    Header
}

/// <summary>
/// Query filter configuration for list endpoints.
/// </summary>
public class FilterConfig
{
    [YamlMember(Alias = "field")]
    public string Field { get; set; } = string.Empty;

    [YamlMember(Alias = "operator")]
    public string Operator { get; set; } = "equals";

    public FilterOperator FilterOperator => Operator.ToLowerInvariant() switch
    {
        "equals" => Configuration.FilterOperator.Equals,
        "contains" => Configuration.FilterOperator.Contains,
        "starts_with" => Configuration.FilterOperator.StartsWith,
        "ends_with" => Configuration.FilterOperator.EndsWith,
        "between" => Configuration.FilterOperator.Between,
        "in" => Configuration.FilterOperator.In,
        "gt" => Configuration.FilterOperator.GreaterThan,
        "gte" => Configuration.FilterOperator.GreaterThanOrEqual,
        "lt" => Configuration.FilterOperator.LessThan,
        "lte" => Configuration.FilterOperator.LessThanOrEqual,
        _ => Configuration.FilterOperator.Equals
    };
}

public enum FilterOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    Between,
    In,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// Cache configuration for an endpoint.
/// </summary>
public class CacheConfig
{
    /// <summary>
    /// Cache duration in seconds.
    /// </summary>
    [YamlMember(Alias = "duration")]
    public int Duration { get; set; } = 60;

    /// <summary>
    /// Whether to vary cache by query string.
    /// </summary>
    [YamlMember(Alias = "vary_by_query")]
    public bool VaryByQuery { get; set; } = true;

    /// <summary>
    /// Whether to vary cache by authenticated user.
    /// </summary>
    [YamlMember(Alias = "vary_by_user")]
    public bool VaryByUser { get; set; }

    /// <summary>
    /// Specific query parameters to include in cache key.
    /// </summary>
    [YamlMember(Alias = "vary_by_params")]
    public List<string>? VaryByParams { get; set; }
}

/// <summary>
/// Rate limiting configuration for an endpoint.
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    /// Maximum number of requests allowed in the time window.
    /// </summary>
    [YamlMember(Alias = "limit")]
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Time window in seconds.
    /// </summary>
    [YamlMember(Alias = "window")]
    public int Window { get; set; } = 60;

    /// <summary>
    /// Rate limit policy name (for shared limits across endpoints).
    /// </summary>
    [YamlMember(Alias = "policy")]
    public string? Policy { get; set; }
}
