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
}
