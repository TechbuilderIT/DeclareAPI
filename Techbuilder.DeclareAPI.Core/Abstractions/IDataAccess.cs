using Techbuilder.DeclareAPI.Core.Configuration;

namespace Techbuilder.DeclareAPI.Core.Abstractions;

/// <summary>
/// Data access abstraction for DeclareAPI.
/// Implementations can use Dapper, EF Core, or raw ADO.NET.
/// </summary>
public interface IDataAccess
{
    /// <summary>
    /// Queries a single record from a data source.
    /// </summary>
    Task<T?> QuerySingleAsync<T>(string source, object? parameters = null, CancellationToken ct = default);

    /// <summary>
    /// Queries multiple records from a data source.
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(string source, object? parameters = null, CancellationToken ct = default);

    /// <summary>
    /// Queries records with pagination support.
    /// </summary>
    Task<PagedResult<T>> QueryPagedAsync<T>(
        string source,
        int page,
        int pageSize,
        object? parameters = null,
        string? orderBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Queries records with pagination and filter configuration support.
    /// </summary>
    Task<PagedResult<T>> QueryPagedAsync<T>(
        string source,
        int page,
        int pageSize,
        IEnumerable<FilterConfig>? filters,
        IDictionary<string, object?>? filterValues,
        string? orderBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a command (INSERT, UPDATE, DELETE) and returns the number of affected rows.
    /// </summary>
    Task<int> ExecuteAsync(string source, object? parameters = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a command and returns a scalar value (e.g., newly created ID).
    /// </summary>
    Task<T?> ExecuteScalarAsync<T>(string source, object? parameters = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a database routine and returns the number of affected rows.
    /// Each parameter is passed as a named argument (<c>name => value</c>);
    /// <paramref name="sourceType"/> selects how the routine is invoked (function vs procedure).
    /// </summary>
    Task<int> ExecuteAsync(string source, SourceType sourceType, object? parameters = null, CancellationToken ct = default)
        => ExecuteAsync(source, parameters, ct);

    /// <summary>
    /// Executes a database routine and returns a scalar value (e.g., newly created ID).
    /// Each parameter is passed as a named argument (<c>name => value</c>);
    /// <paramref name="sourceType"/> selects how the routine is invoked (function vs procedure).
    /// </summary>
    Task<T?> ExecuteScalarAsync<T>(string source, SourceType sourceType, object? parameters = null, CancellationToken ct = default)
        => ExecuteScalarAsync<T>(source, parameters, ct);

    /// <summary>
    /// Verifies that the database is reachable. Used by the database health check.
    /// </summary>
    Task CheckConnectionAsync(CancellationToken ct = default)
        => QuerySingleAsync<int>("SELECT 1", ct: ct);
}
