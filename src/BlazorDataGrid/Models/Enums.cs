namespace BlazorDataGrid.Models;

/// <summary>Sort direction for a column.</summary>
public enum SortDirection
{
    None = 0,
    Ascending = 1,
    Descending = 2
}

/// <summary>How rows can be selected in the grid.</summary>
public enum GridSelectionMode
{
    None = 0,
    Single = 1,
    Multiple = 2
}

/// <summary>Built-in aggregate functions for summary/footer rows.</summary>
public enum AggregateType
{
    None = 0,
    Sum,
    Average,
    Count,
    Min,
    Max
}

/// <summary>Horizontal alignment of cell content.</summary>
public enum ColumnAlign
{
    Left = 0,
    Center,
    Right
}

/// <summary>The kind of editor/filter rendered for a column based on its data type.</summary>
public enum ColumnDataType
{
    Auto = 0,
    Text,
    Number,
    Boolean,
    Date,
    Enum
}

/// <summary>Comparison operators available for column filtering.</summary>
public enum FilterOperator
{
    Contains = 0,
    DoesNotContain,
    StartsWith,
    EndsWith,
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsEmpty,
    IsNotEmpty
}

/// <summary>Where the pager is rendered relative to the grid.</summary>
public enum PagerPosition
{
    Bottom = 0,
    Top,
    TopAndBottom
}

/// <summary>Text direction for the grid.</summary>
public enum GridDirection
{
    Ltr = 0,
    Rtl
}
