namespace BlazorDataGrid;

/// <summary>Describes a filter applied to a single column.</summary>
public sealed class BlazorDataGridFilterDescriptor
{
    public required string ColumnId { get; init; }
    public BlazorDataGridFilterOperator Operator { get; set; } = BlazorDataGridFilterOperator.Contains;
    public object? Value { get; set; }
}
