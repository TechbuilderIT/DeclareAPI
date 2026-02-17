using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Core.Query;

/// <summary>
/// Builds SQL WHERE clauses from filter configurations.
/// </summary>
public class FilterQueryBuilder
{
    private readonly DatabaseDialect _dialect;
    private readonly List<(string Sql, string ParamName, object? Value)> _conditions = new();
    private int _paramIndex;

    public FilterQueryBuilder(DatabaseDialect dialect = DatabaseDialect.PostgreSQL)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Adds a filter condition based on the filter configuration and provided value.
    /// </summary>
    public FilterQueryBuilder AddFilter(FilterConfig filter, object? value)
    {
        if (value == null) return this;

        var paramName = $"p{_paramIndex++}";
        var quotedField = QuoteIdentifier(filter.Field);

        var (sql, actualValue) = filter.FilterOperator switch
        {
            FilterOperator.Equals => ($"{quotedField} = @{paramName}", value),
            FilterOperator.Contains => ($"{quotedField} ILIKE @{paramName}", $"%{value}%"),
            FilterOperator.StartsWith => ($"{quotedField} ILIKE @{paramName}", $"{value}%"),
            FilterOperator.EndsWith => ($"{quotedField} ILIKE @{paramName}", $"%{value}"),
            FilterOperator.GreaterThan => ($"{quotedField} > @{paramName}", value),
            FilterOperator.GreaterThanOrEqual => ($"{quotedField} >= @{paramName}", value),
            FilterOperator.LessThan => ($"{quotedField} < @{paramName}", value),
            FilterOperator.LessThanOrEqual => ($"{quotedField} <= @{paramName}", value),
            FilterOperator.In => BuildInCondition(quotedField, paramName, value),
            FilterOperator.Between => BuildBetweenCondition(quotedField, paramName, value),
            _ => ($"{quotedField} = @{paramName}", value)
        };

        _conditions.Add((sql, paramName, actualValue));
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
        _conditions.Add(($"{quotedField} = @{paramName}", paramName, value));
        return this;
    }

    /// <summary>
    /// Builds the WHERE clause and returns parameters dictionary.
    /// </summary>
    public (string WhereClause, Dictionary<string, object?> Parameters) Build()
    {
        if (_conditions.Count == 0)
            return ("", new Dictionary<string, object?>());

        var whereClause = " WHERE " + string.Join(" AND ", _conditions.Select(c => c.Sql));
        var parameters = _conditions.ToDictionary(c => c.ParamName, c => c.Value);

        return (whereClause, parameters);
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

    private (string Sql, object Value) BuildInCondition(string quotedField, string paramName, object value)
    {
        // Value should be a comma-separated string or collection
        var values = ParseInValues(value);

        if (values.Count == 0)
            return ($"1=1", value); // Always true if no values

        var placeholders = values.Select((_, i) => $"@{paramName}_{i}").ToList();
        var sql = $"{quotedField} IN ({string.Join(", ", placeholders)})";

        // For IN clause, we'll store the values array and handle specially
        return (sql, values);
    }

    private (string Sql, object Value) BuildBetweenCondition(string quotedField, string paramName, object value)
    {
        // Value should be "min,max" format
        var parts = value.ToString()?.Split(',', 2);

        if (parts?.Length != 2)
            return ($"1=1", value); // Always true if invalid format

        var sql = $"{quotedField} BETWEEN @{paramName}_min AND @{paramName}_max";
        return (sql, new BetweenValue(parts[0].Trim(), parts[1].Trim()));
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
