namespace BlazorDataGrid;

/// <summary>Describes the sort state applied to a single column.</summary>
public sealed class SortDescriptor
{
    public required string ColumnId { get; init; }
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
    /// <summary>Priority for multi-column sorting (1 = primary).</summary>
    public int Priority { get; set; }
}

/// <summary>Describes a filter applied to a single column.</summary>
public sealed class FilterDescriptor
{
    public required string ColumnId { get; init; }
    public FilterOperator Operator { get; set; } = FilterOperator.Contains;
    public object? Value { get; set; }
}

/// <summary>Describes a grouping applied to a column.</summary>
public sealed class GroupDescriptor
{
    public required string ColumnId { get; init; }
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}
