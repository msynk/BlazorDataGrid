namespace BlazorDataGrid;

/// <summary>Describes the sort state applied to a single column.</summary>
public sealed class BlazorDataGridSortDescriptor
{
    public required string ColumnId { get; init; }
    public BlazorDataGridSortDirection Direction { get; set; } = BlazorDataGridSortDirection.Ascending;
    /// <summary>Priority for multi-column sorting (1 = primary).</summary>
    public int Priority { get; set; }
}
