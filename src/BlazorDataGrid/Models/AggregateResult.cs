namespace BlazorDataGrid;

/// <summary>Holds the computed aggregate value for a column footer or group.</summary>
public sealed class AggregateResult
{
    public required string ColumnId { get; init; }
    public AggregateType Type { get; init; }
    public object? Value { get; init; }
    public string FormattedValue { get; init; } = string.Empty;
}
