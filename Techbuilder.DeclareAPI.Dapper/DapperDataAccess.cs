using System.Data;
using Dapper;
using Npgsql;
using Techbuilder.DeclareAPI.Core.Abstractions;
using Techbuilder.DeclareAPI.Core.Configuration;
using Techbuilder.DeclareAPI.Core.Query;

namespace Techbuilder.DeclareAPI.Dapper;

/// <summary>
/// Dapper-based implementation of IDataAccess for PostgreSQL.
/// </summary>
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
        var sql = BuildSelectSql(source, parameters);
        return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string source, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var sql = BuildSelectSql(source, parameters);
        return await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
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
        var whereClause = BuildWhereClause(parameters);

        // Count query
        var countSql = $"SELECT COUNT(*) FROM {quotedSource}{whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        // Data query with pagination
        var orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $" ORDER BY {orderBy}";
        var dataSql = $"SELECT * FROM {quotedSource}{whereClause}{orderByClause} LIMIT @_limit OFFSET @_offset";

        var dataParams = new DynamicParameters(parameters);
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
            new CommandDefinition(countSql, filterParams, cancellationToken: ct));

        // Data query with pagination
        var orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $" ORDER BY {SanitizeOrderBy(orderBy)}";
        var dataSql = $"SELECT * FROM {quotedSource}{whereClause}{orderByClause} LIMIT @_limit OFFSET @_offset";

        var dataParams = new DynamicParameters();
        foreach (var param in filterParams)
        {
            dataParams.Add(param.Key, param.Value);
        }
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

    public async Task<int> ExecuteAsync(string source, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();

        // For stored procedures/functions, use different approach
        var sql = BuildExecuteSql(source);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task<T?> ExecuteScalarAsync<T>(string source, object? parameters = null, CancellationToken ct = default)
    {
        using var connection = CreateConnection();

        // For PostgreSQL functions that return a value
        var sql = BuildFunctionCallSql(source, parameters);
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    private string BuildSelectSql(string source, object? parameters)
    {
        var quotedSource = QuoteIdentifier(source);
        var whereClause = BuildWhereClause(parameters);
        return $"SELECT * FROM {quotedSource}{whereClause}";
    }

    private string BuildWhereClause(object? parameters)
    {
        if (parameters == null) return "";

        var props = parameters.GetType().GetProperties()
            .Where(p => p.GetValue(parameters) != null)
            .Where(p => !p.Name.StartsWith("_")) // Skip internal parameters
            .ToList();

        if (props.Count == 0) return "";

        var conditions = props.Select(p => $"{QuoteIdentifier(p.Name)} = @{p.Name}");
        return " WHERE " + string.Join(" AND ", conditions);
    }

    private string BuildExecuteSql(string source)
    {
        // Assume it's a stored procedure/function call
        return _provider switch
        {
            DatabaseProvider.PostgreSQL => $"CALL {QuoteIdentifier(source)}()",
            _ => $"EXEC {QuoteIdentifier(source)}"
        };
    }

    private string BuildFunctionCallSql(string source, object? parameters)
    {
        var quotedSource = QuoteIdentifier(source);

        if (parameters == null)
            return $"SELECT {quotedSource}()";

        var paramNames = parameters.GetType().GetProperties()
            .Select(p => $"@{p.Name}");

        return $"SELECT {quotedSource}({string.Join(", ", paramNames)})";
    }

    private string QuoteIdentifier(string identifier)
    {
        // Prevent SQL injection by validating identifier
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"Invalid identifier: {identifier}", nameof(identifier));

        return _provider switch
        {
            DatabaseProvider.PostgreSQL => $"\"{identifier}\"",
            _ => $"[{identifier}]"
        };
    }

    private static bool IsValidIdentifier(string identifier)
    {
        // Allow only alphanumeric characters, underscores, and dots (for schema.table)
        return !string.IsNullOrEmpty(identifier) &&
               identifier.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
    }
}

public enum DatabaseProvider
{
    PostgreSQL,
    SqlServer
}
