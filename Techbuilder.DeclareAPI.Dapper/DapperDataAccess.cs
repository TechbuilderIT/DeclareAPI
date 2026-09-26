using System.Data;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Query;

namespace Techbuilder.DeclareAPI.Dapper;

public class DapperDataAccess : IDataAccess
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;

    public DapperDataAccess(string connectionString, DatabaseProvider provider = DatabaseProvider.PostgreSQL)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _provider = provider;
    }

    private IDbConnection CreateConnection()
    {
        return _provider switch
        {
            DatabaseProvider.PostgreSQL => new NpgsqlConnection(_connectionString),
            _ => throw new NotSupportedException($"Database provider {_provider} is not supported yet")
        };
    }

    public async Task<T?> QuerySingleAsync<T>(string source, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var arguments = ToArguments(parameters);
        var sql = BuildSelectSql(source, arguments);
        return await connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, CreateParameters(arguments), cancellationToken: ct));
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string source, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var arguments = ToArguments(parameters);
        var sql = BuildSelectSql(source, arguments);
        return await connection.QueryAsync<T>(
            new CommandDefinition(sql, CreateParameters(arguments), cancellationToken: ct));
    }

    public async Task<PagedResult<T>> QueryPagedAsync<T>(
        string source,
        int page,
        int pageSize,
        object? parameters = null,
        string? orderBy = null,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection();

        var offset = (page - 1) * pageSize;
        var quotedSource = QuoteIdentifier(source);

        // Build WHERE clause from parameters
        var arguments = ToArguments(parameters);
        var whereClause = BuildWhereClause(arguments);

        // Count query
        var countSql = $"SELECT COUNT(*) FROM {quotedSource}{whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, CreateParameters(arguments), cancellationToken: ct));

        // Data query with pagination
        var orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $" ORDER BY {orderBy}";
        var dataSql = $"SELECT * FROM {quotedSource}{whereClause}{orderByClause} LIMIT @_limit OFFSET @_offset";

        var dataParams = CreateParameters(arguments);
        dataParams.Add("_limit", pageSize);
        dataParams.Add("_offset", offset);

        var items = await connection.QueryAsync<T>(
            new CommandDefinition(dataSql, dataParams, cancellationToken: ct));

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<T>> QueryPagedAsync<T>(
        string source,
        int page,
        int pageSize,
        IEnumerable<FilterConfig>? filters,
        IDictionary<string, object?>? filterValues,
        string? orderBy = null,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection();

        var offset = (page - 1) * pageSize;
        var quotedSource = QuoteIdentifier(source);

        // Build WHERE clause using FilterQueryBuilder
        var dialect = _provider switch
        {
            DatabaseProvider.PostgreSQL => DatabaseDialect.PostgreSQL,
            DatabaseProvider.SqlServer => DatabaseDialect.SqlServer,
            _ => DatabaseDialect.PostgreSQL
        };

        var (whereClause, filterParams) = FilterQueryBuilder.BuildFromFilters(filters, filterValues, dialect);

        // Count query
        var countSql = $"SELECT COUNT(*) FROM {quotedSource}{whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, CreateParameters(filterParams), cancellationToken: ct));

        // Data query with pagination
        var orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $" ORDER BY {SanitizeOrderBy(orderBy)}";
        var dataSql = $"SELECT * FROM {quotedSource}{whereClause}{orderByClause} LIMIT @_limit OFFSET @_offset";

        var dataParams = CreateParameters(filterParams);
        dataParams.Add("_limit", pageSize);
        dataParams.Add("_offset", offset);

        var items = await connection.QueryAsync<T>(
            new CommandDefinition(dataSql, dataParams, cancellationToken: ct));

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }

    private static string SanitizeOrderBy(string orderBy)
    {
        // Only allow field names with optional ASC/DESC
        var parts = orderBy.Trim().Split(' ', 2);
        var field = parts[0];

        if (!IsValidIdentifier(field))
            throw new ArgumentException($"Invalid order by field: {field}");

        var direction = parts.Length > 1 && parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";

        return $"\"{field}\" {direction}";
    }

    /// <summary>
    /// Executes <paramref name="source"/> as a procedure (<c>CALL</c>). Kept for callers that don't pass
    /// a <see cref="SourceType"/>; use the overload that takes one to call a function.
    /// </summary>
    public Task<int> ExecuteAsync(string source, object? parameters = null, CancellationToken ct = default)
        => ExecuteAsync(source, SourceType.Procedure, parameters, ct);

    /// <summary>
    /// Executes <paramref name="source"/> as a function and returns the first column of the first row.
    /// Kept for callers that don't pass a <see cref="SourceType"/>.
    /// </summary>
    public Task<T?> ExecuteScalarAsync<T>(string source, object? parameters = null, CancellationToken ct = default)
        => ExecuteScalarAsync<T>(source, SourceType.Function, parameters, ct);

    public async Task<int> ExecuteAsync(string source, SourceType sourceType, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var arguments = ToArguments(parameters);
        var sql = BuildRoutineCallSql(source, sourceType, arguments);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, CreateParameters(arguments), cancellationToken: ct));
    }

    public async Task<T?> ExecuteScalarAsync<T>(string source, SourceType sourceType, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var arguments = ToArguments(parameters);
        var sql = BuildRoutineCallSql(source, sourceType, arguments);
        return await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, CreateParameters(arguments), cancellationToken: ct));
    }

    public async Task CheckConnectionAsync(CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
    }

    private string BuildSelectSql(string source, IReadOnlyList<KeyValuePair<string, object?>> arguments)
    {
        var quotedSource = QuoteIdentifier(source);
        var whereClause = BuildWhereClause(arguments);
        return $"SELECT * FROM {quotedSource}{whereClause}";
    }

    /// <summary>
    /// <c>WHERE "key" = @key AND ...</c>: one equality per argument, column name = argument name.
    /// Null values and internal arguments (leading <c>_</c>) are skipped.
    /// </summary>
    private string BuildWhereClause(IReadOnlyList<KeyValuePair<string, object?>> arguments)
    {
        var conditions = arguments
            .Where(a => a.Value != null)
            .Where(a => !a.Key.StartsWith('_')) // Skip internal parameters
            .Select(a => $"{QuoteIdentifier(RequireParameterName(a.Key))} = @{a.Key}")
            .ToList();

        if (conditions.Count == 0) return "";

        return " WHERE " + string.Join(" AND ", conditions);
    }

    /// <summary>
    /// Calls a database routine with named arguments, one per key: <c>arg => @arg</c>.
    /// PostgreSQL: a function is called with <c>SELECT * FROM "fn"(...)</c> (the scalar result is the first
    /// column of the first row), a procedure with <c>CALL "proc"(...)</c>. Argument names are written
    /// unquoted, so PostgreSQL folds them to lower case exactly as in a hand-written call. Arguments left out
    /// take the routine's DEFAULT.
    /// </summary>
    private string BuildRoutineCallSql(string source, SourceType sourceType, IReadOnlyList<KeyValuePair<string, object?>> arguments)
    {
        var quotedSource = QuoteIdentifier(source);
        var names = arguments.Select(a => RequireParameterName(a.Key)).ToList();

        return (_provider, sourceType) switch
        {
            (DatabaseProvider.PostgreSQL, SourceType.Procedure) =>
                $"CALL {quotedSource}({string.Join(", ", names.Select(n => $"{n} => @{n}"))})",
            (DatabaseProvider.PostgreSQL, _) =>
                $"SELECT * FROM {quotedSource}({string.Join(", ", names.Select(n => $"{n} => @{n}"))})",
            (_, SourceType.Procedure) =>
                $"EXEC {quotedSource} {string.Join(", ", names.Select(n => $"@{n} = @{n}"))}".TrimEnd(),
            _ =>
                $"SELECT {quotedSource}({string.Join(", ", names.Select(n => $"@{n}"))})"
        };
    }

    /// <summary>
    /// Turns the caller's parameters into an ordered list of name/value pairs. Accepts a dictionary
    /// (what the router passes), <see cref="DynamicParameters"/>, or an object whose public properties
    /// are the parameters (anonymous types in custom handlers).
    /// </summary>
    private static IReadOnlyList<KeyValuePair<string, object?>> ToArguments(object? parameters)
    {
        return parameters switch
        {
            null => Array.Empty<KeyValuePair<string, object?>>(),
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs.ToList(),
            DynamicParameters dynamicParameters => dynamicParameters.ParameterNames
                .Select(name => new KeyValuePair<string, object?>(name, dynamicParameters.Get<object?>(name)))
                .ToList(),
            _ => parameters.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => new KeyValuePair<string, object?>(p.Name, p.GetValue(parameters)))
                .ToList()
        };
    }

    /// <summary>
    /// Builds Dapper parameters. On PostgreSQL, strings and nulls are sent with the <c>unknown</c> type, so the
    /// server coerces them to the target column/argument type exactly like a quoted literal
    /// (<c>'2024-01-01'</c> against a date, <c>'42'</c> against an int, a uuid string against a uuid).
    /// HTTP inputs (query-string filters, JSON strings) are always text; without this PostgreSQL rejects
    /// <c>uuid = text</c>, <c>integer &gt; text</c>, and routine calls whose arguments are not text.
    /// </summary>
    private DynamicParameters CreateParameters(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var parameters = new DynamicParameters();

        foreach (var (name, rawValue) in arguments)
        {
            var value = NormalizeValue(rawValue);

            if (_provider == DatabaseProvider.PostgreSQL && value is null or string)
            {
                parameters.Add(name, new UntypedPostgresParameter(value as string));
            }
            else
            {
                parameters.Add(name, value);
            }
        }

        return parameters;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement json) return value;

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.TryGetInt64(out var l) ? l : json.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => json.GetRawText() // object/array: JSON text, coerced to json/jsonb by the server
        };
    }

    private static string RequireParameterName(string name)
    {
        var valid = !string.IsNullOrEmpty(name) &&
                    (char.IsAsciiLetter(name[0]) || name[0] == '_') &&
                    name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

        if (!valid)
            throw new ArgumentException($"Invalid parameter name: {name}", nameof(name));

        return name;
    }

    /// <summary>
    /// Quotes each part of a possibly schema-qualified name: <c>clinic.rooms</c> → <c>"clinic"."rooms"</c>.
    /// </summary>
    private string QuoteIdentifier(string identifier)
    {
        // Prevent SQL injection by validating identifier
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"Invalid identifier: {identifier}", nameof(identifier));

        var parts = identifier.Split('.');

        return string.Join(".", parts.Select(part => _provider switch
        {
            DatabaseProvider.PostgreSQL => $"\"{part}\"",
            _ => $"[{part}]"
        }));
    }

    private static bool IsValidIdentifier(string identifier)
    {
        // Allow only alphanumeric characters, underscores, and dots (for schema.table); no empty parts
        return !string.IsNullOrEmpty(identifier) &&
               identifier.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.') &&
               identifier.Split('.').All(part => part.Length > 0);
    }

    /// <summary>
    /// A PostgreSQL parameter sent with the <c>unknown</c> type (text or NULL), resolved by the server.
    /// </summary>
    private sealed class UntypedPostgresParameter : SqlMapper.ICustomQueryParameter
    {
        private readonly string? _value;

        public UntypedPostgresParameter(string? value) => _value = value;

        public void AddParameter(IDbCommand command, string name)
        {
            command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Unknown)
            {
                Value = (object?)_value ?? DBNull.Value
            });
        }
    }
}

public enum DatabaseProvider
{
    PostgreSQL,
    SqlServer
}
