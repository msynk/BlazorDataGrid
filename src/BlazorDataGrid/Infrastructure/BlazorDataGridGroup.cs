namespace BlazorDataGrid;

/// <summary>A materialized group of rows produced by grouping.</summary>
public sealed class BlazorDataGridGroup<TItem>
{
    public required string ColumnId { get; init; }
    public required object? Key { get; init; }
    public string KeyText { get; init; } = string.Empty;
    public List<TItem> Items { get; init; } = new();
    public List<BlazorDataGridAggregateResult> Aggregates { get; init; } = new();
}
