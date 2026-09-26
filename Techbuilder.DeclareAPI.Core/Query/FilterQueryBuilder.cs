using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Core.Query;

public class FilterQueryBuilder
{
    private readonly DatabaseDialect _dialect;
    private readonly List<string> _conditions = new();
    private readonly Dictionary<string, object?> _parameters = new();
    private int _paramIndex;

    public FilterQueryBuilder(DatabaseDialect dialect = DatabaseDialect.PostgreSQL)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Adds a filter condition based on the filter configuration and provided value.
    /// Every <c>@placeholder</c> written into the SQL gets its own entry in the parameters
    /// returned by <see cref="Build"/> (<c>in</c> uses <c>@pN_0..@pN_k</c>, <c>between</c> uses <c>@pN_min/@pN_max</c>).
    /// </summary>
    public FilterQueryBuilder AddFilter(FilterConfig filter, object? value)
    {
        if (value == null) return this;

        var paramName = $"p{_paramIndex++}";
        var quotedField = QuoteIdentifier(filter.Field);

        var (sql, parameters) = filter.FilterOperator switch
        {
            FilterOperator.Equals => Single($"{quotedField} = @{paramName}", paramName, value),
            FilterOperator.Contains => Single($"{quotedField} ILIKE @{paramName}", paramName, $"%{value}%"),
            FilterOperator.StartsWith => Single($"{quotedField} ILIKE @{paramName}", paramName, $"{value}%"),
            FilterOperator.EndsWith => Single($"{quotedField} ILIKE @{paramName}", paramName, $"%{value}"),
            FilterOperator.GreaterThan => Single($"{quotedField} > @{paramName}", paramName, value),
            FilterOperator.GreaterThanOrEqual => Single($"{quotedField} >= @{paramName}", paramName, value),
            FilterOperator.LessThan => Single($"{quotedField} < @{paramName}", paramName, value),
            FilterOperator.LessThanOrEqual => Single($"{quotedField} <= @{paramName}", paramName, value),
            FilterOperator.In => BuildInCondition(quotedField, paramName, value),
            FilterOperator.Between => BuildBetweenCondition(quotedField, paramName, value),
            _ => Single($"{quotedField} = @{paramName}", paramName, value)
        };

        _conditions.Add(sql);
        foreach (var (name, paramValue) in parameters)
        {
            _parameters[name] = paramValue;
        }

        return this;
    }

    /// <summary>
    /// Adds a simple equals condition.
    /// </summary>
    public FilterQueryBuilder AddEquals(string field, object? value)
    {
        if (value == null) return this;

        var paramName = $"p{_paramIndex++}";
        var quotedField = QuoteIdentifier(field);
        _conditions.Add($"{quotedField} = @{paramName}");
        _parameters[paramName] = value;
        return this;
    }

    /// <summary>
    /// Builds the WHERE clause and returns parameters dictionary.
    /// </summary>
    public (string WhereClause, Dictionary<string, object?> Parameters) Build()
    {
        if (_conditions.Count == 0)
            return ("", new Dictionary<string, object?>());

        var whereClause = " WHERE " + string.Join(" AND ", _conditions);
        return (whereClause, new Dictionary<string, object?>(_parameters));
    }

    /// <summary>
    /// Builds filters from a dictionary of filter configs and query values.
    /// </summary>
    public static (string WhereClause, Dictionary<string, object?> Parameters) BuildFromFilters(
        IEnumerable<FilterConfig>? filters,
        IDictionary<string, object?>? queryValues,
        DatabaseDialect dialect = DatabaseDialect.PostgreSQL)
    {
        if (filters == null || queryValues == null)
            return ("", new Dictionary<string, object?>());

        var builder = new FilterQueryBuilder(dialect);

        foreach (var filter in filters)
        {
            if (queryValues.TryGetValue(filter.Field, out var value) && value != null)
            {
                builder.AddFilter(filter, value);
            }
        }

        return builder.Build();
    }

    private static (string Sql, IEnumerable<KeyValuePair<string, object?>> Parameters) Single(
        string sql, string paramName, object? value)
    {
        return (sql, new[] { new KeyValuePair<string, object?>(paramName, value) });
    }

    private static (string Sql, IEnumerable<KeyValuePair<string, object?>> Parameters) BuildInCondition(
        string quotedField, string paramName, object value)
    {
        // Value should be a comma-separated string or collection
        var values = ParseInValues(value);

        if (values.Count == 0)
            return ("1=1", Array.Empty<KeyValuePair<string, object?>>()); // Always true if no values

        var parameters = values
            .Select((v, i) => new KeyValuePair<string, object?>($"{paramName}_{i}", v))
            .ToList();
        var sql = $"{quotedField} IN ({string.Join(", ", parameters.Select(p => $"@{p.Key}"))})";

        return (sql, parameters);
    }

    private static (string Sql, IEnumerable<KeyValuePair<string, object?>> Parameters) BuildBetweenCondition(
        string quotedField, string paramName, object value)
    {
        // Value should be "min,max" format
        var parts = value.ToString()?.Split(',', 2);

        if (parts?.Length != 2)
            return ("1=1", Array.Empty<KeyValuePair<string, object?>>()); // Always true if invalid format

        var sql = $"{quotedField} BETWEEN @{paramName}_min AND @{paramName}_max";
        return (sql, new[]
        {
            new KeyValuePair<string, object?>($"{paramName}_min", parts[0].Trim()),
            new KeyValuePair<string, object?>($"{paramName}_max", parts[1].Trim())
        });
    }

    private static List<object> ParseInValues(object value)
    {
        if (value is IEnumerable<object> enumerable)
            return enumerable.ToList();

        var str = value.ToString();
        if (string.IsNullOrEmpty(str))
            return new List<object>();

        return str.Split(',').Select(s => (object)s.Trim()).ToList();
    }

    private string QuoteIdentifier(string identifier)
    {
        // Prevent SQL injection by validating identifier
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"Invalid identifier: {identifier}", nameof(identifier));

        return _dialect switch
        {
            DatabaseDialect.PostgreSQL => $"\"{identifier}\"",
            DatabaseDialect.SqlServer => $"[{identifier}]",
            DatabaseDialect.MySQL => $"`{identifier}`",
            _ => $"\"{identifier}\""
        };
    }

    private static bool IsValidIdentifier(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) &&
               identifier.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
    }
}

/// <summary>
/// Database dialect for SQL generation.
/// </summary>
public enum DatabaseDialect
{
    PostgreSQL,
    SqlServer,
    MySQL,
    SQLite
}

/// <summary>
/// Helper record for BETWEEN values.
/// </summary>
public record BetweenValue(string Min, string Max);
