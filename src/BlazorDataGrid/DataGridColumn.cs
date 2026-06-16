using BlazorDataGrid.Infrastructure;
using BlazorDataGrid.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorDataGrid;

/// <summary>
/// Defines a column inside a <see cref="DataGrid{TItem}"/>. Place these as child
/// content of the grid. A column can be bound to a property via <see cref="Field"/>
/// or be a purely template-driven column.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public class DataGridColumn<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] internal DataGrid<TItem>? Grid { get; set; }

    /// <summary>Name of the property this column is bound to. Supports nested paths ("Address.City").</summary>
    [Parameter] public string? Field { get; set; }

    /// <summary>Stable identifier for the column. Defaults to <see cref="Field"/>.</summary>
    [Parameter] public string? ColumnId { get; set; }

    /// <summary>Header text. Defaults to a humanized <see cref="Field"/>.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>CSS width, e.g. "120px" or "20%". When null the column shares remaining space.</summary>
    [Parameter] public string? Width { get; set; }

    [Parameter] public int MinWidth { get; set; } = 60;

    [Parameter] public bool? Sortable { get; set; }
    [Parameter] public bool? Filterable { get; set; }
    [Parameter] public bool? Resizable { get; set; }
    [Parameter] public bool? Reorderable { get; set; }
    [Parameter] public bool? Editable { get; set; }
    [Parameter] public bool? Groupable { get; set; }

    /// <summary>Pin the column to the start edge so it stays visible while scrolling horizontally.</summary>
    [Parameter] public bool Frozen { get; set; }

    [Parameter] public bool Visible { get; set; } = true;

    [Parameter] public ColumnAlign Align { get; set; } = ColumnAlign.Left;

    /// <summary>A .NET format string applied to the value (e.g. "C2", "yyyy-MM-dd").</summary>
    [Parameter] public string? Format { get; set; }

    [Parameter] public ColumnDataType DataType { get; set; } = ColumnDataType.Auto;

    [Parameter] public AggregateType Aggregate { get; set; } = AggregateType.None;

    /// <summary>Format string for the aggregate value. Falls back to <see cref="Format"/>.</summary>
    [Parameter] public string? AggregateFormat { get; set; }

    [Parameter] public string? HeaderClass { get; set; }
    [Parameter] public string? CellClass { get; set; }

    /// <summary>Custom rendering for a data cell.</summary>
    [Parameter] public RenderFragment<TItem>? Template { get; set; }

    /// <summary>Custom rendering for the header cell content.</summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>Custom editor rendered when the row/cell is in edit mode.</summary>
    [Parameter] public RenderFragment<TItem>? EditTemplate { get; set; }

    /// <summary>Custom rendering for the footer/aggregate cell.</summary>
    [Parameter] public RenderFragment<AggregateResult>? FooterTemplate { get; set; }

    // ---- Runtime state (managed by the grid) ----

    /// <summary>Current resolved width applied via inline style (set by resizing).</summary>
    internal double? ResizedWidth { get; set; }

    internal PropertyAccessor<TItem>? Accessor { get; private set; }

    internal string Id => ColumnId ?? Field ?? $"col-{GetHashCode():x}";

    internal string DisplayTitle => Title ?? Humanize(Field) ?? Id;

    internal bool HasField => !string.IsNullOrEmpty(Field);

    internal ColumnDataType EffectiveDataType
    {
        get
        {
            if (DataType != ColumnDataType.Auto) return DataType;
            if (Accessor is null) return ColumnDataType.Text;
            var t = Accessor.UnderlyingType;
            if (t == typeof(bool)) return ColumnDataType.Boolean;
            if (t.IsEnum) return ColumnDataType.Enum;
            if (t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(DateTimeOffset)) return ColumnDataType.Date;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(double) || t == typeof(float) || t == typeof(decimal))
                return ColumnDataType.Number;
            return ColumnDataType.Text;
        }
    }

    protected override void OnInitialized()
    {
        if (Grid is null)
            throw new InvalidOperationException($"{nameof(DataGridColumn<TItem>)} must be used inside a {nameof(DataGrid<TItem>)}.");
        Grid.AddColumn(this);
    }

    protected override void OnParametersSet()
    {
        if (HasField)
            Accessor = PropertyAccessor<TItem>.For(Field!);
    }

    public void Dispose() => Grid?.RemoveColumn(this);

    internal object? GetValue(TItem item) => Accessor?.GetValue(item);

    internal string GetFormattedValue(TItem item)
    {
        var value = GetValue(item);
        return FormatValue(value);
    }

    internal string FormatValue(object? value)
    {
        if (value is null) return string.Empty;
        if (!string.IsNullOrEmpty(Format) && value is IFormattable f)
            return f.ToString(Format, System.Globalization.CultureInfo.CurrentCulture);
        return value.ToString() ?? string.Empty;
    }

    private static string? Humanize(string? field)
    {
        if (string.IsNullOrEmpty(field)) return null;
        var name = field.Contains('.') ? field[(field.LastIndexOf('.') + 1)..] : field;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }
}
