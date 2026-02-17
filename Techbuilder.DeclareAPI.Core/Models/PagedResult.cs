namespace Techbuilder.DeclareAPI.Core.Abstractions;

/// <summary>
/// Represents a paginated result set.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult(IEnumerable<T> items, int page, int pageSize, int totalCount)
    {
        Items = items.ToList();
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public static PagedResult<T> Empty(int page, int pageSize)
        => new(Enumerable.Empty<T>(), page, pageSize, 0);
}
