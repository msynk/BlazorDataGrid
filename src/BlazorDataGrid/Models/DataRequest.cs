namespace BlazorDataGrid.Models;

/// <summary>
/// Describes the data the grid needs from an external/server-side source.
/// Passed to the grid's <c>OnRead</c> callback so callers can perform their own
/// sorting, filtering and paging (e.g. against a database).
/// </summary>
public sealed class DataGridReadRequest
{
    /// <summary>Zero-based number of items to skip (for paging/virtualization).</summary>
    public int Skip { get; init; }

    /// <summary>Maximum number of items to return. <c>null</c> means "all".</summary>
    public int? Take { get; init; }

    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = Array.Empty<SortDescriptor>();

    public IReadOnlyList<FilterDescriptor> Filters { get; init; } = Array.Empty<FilterDescriptor>();

    public CancellationToken CancellationToken { get; init; }
}

/// <summary>Result returned from a grid's <c>OnRead</c> callback.</summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public sealed class DataGridReadResult<TItem>
{
    public DataGridReadResult(IReadOnlyList<TItem> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    /// <summary>The items for the current page/window.</summary>
    public IReadOnlyList<TItem> Items { get; }

    /// <summary>The total number of items matching the current filters (across all pages).</summary>
    public int TotalCount { get; }
}
