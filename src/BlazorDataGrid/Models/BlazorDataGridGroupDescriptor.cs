namespace BlazorDataGrid;

/// <summary>Describes a grouping applied to a column.</summary>
public sealed class BlazorDataGridGroupDescriptor
{
    public required string ColumnId { get; init; }
    public BlazorDataGridSortDirection Direction { get; set; } = BlazorDataGridSortDirection.Ascending;
}
