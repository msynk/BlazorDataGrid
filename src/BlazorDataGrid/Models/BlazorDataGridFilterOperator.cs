namespace BlazorDataGrid;

/// <summary>Comparison operators available for column filtering.</summary>
public enum BlazorDataGridFilterOperator
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
