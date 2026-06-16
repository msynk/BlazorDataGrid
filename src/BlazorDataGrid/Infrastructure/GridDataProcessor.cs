using System.Globalization;

namespace BlazorDataGrid;

/// <summary>A materialized group of rows produced by grouping.</summary>
public sealed class GridGroup<TItem>
{
    public required string ColumnId { get; init; }
    public required object? Key { get; init; }
    public string KeyText { get; init; } = string.Empty;
    public List<TItem> Items { get; init; } = new();
    public List<AggregateResult> Aggregates { get; init; } = new();
}

/// <summary>
/// Client-side data pipeline: filtering, multi-sorting, grouping and aggregation.
/// </summary>
public static class GridDataProcessor
{
    public static IReadOnlyList<TItem> Filter<TItem>(
        IEnumerable<TItem> source,
        IReadOnlyList<FilterDescriptor> filters,
        IReadOnlyDictionary<string, DataGridColumn<TItem>> columns)
    {
        if (filters.Count == 0)
            return source as IReadOnlyList<TItem> ?? source.ToList();

        var query = source;
        foreach (var filter in filters)
        {
            if (!columns.TryGetValue(filter.ColumnId, out var column) || column.Accessor is null)
                continue;
            var f = filter;
            var col = column;
            query = query.Where(item => Matches(col.Accessor!.GetValue(item), f));
        }
        return query.ToList();
    }

    public static IReadOnlyList<TItem> Sort<TItem>(
        IReadOnlyList<TItem> source,
        IReadOnlyList<SortDescriptor> sorts,
        IReadOnlyDictionary<string, DataGridColumn<TItem>> columns)
    {
        var active = sorts.Where(s => s.Direction != SortDirection.None).OrderBy(s => s.Priority).ToList();
        if (active.Count == 0) return source;

        IOrderedEnumerable<TItem>? ordered = null;
        foreach (var sort in active)
        {
            if (!columns.TryGetValue(sort.ColumnId, out var column) || column.Accessor is null)
                continue;
            var accessor = column.Accessor;
            Func<TItem, object?> key = item => accessor.GetValue(item);
            var comparer = ValueComparer.Instance;
            if (ordered is null)
            {
                ordered = sort.Direction == SortDirection.Ascending
                    ? source.OrderBy(key, comparer)
                    : source.OrderByDescending(key, comparer);
            }
            else
            {
                ordered = sort.Direction == SortDirection.Ascending
                    ? ordered.ThenBy(key, comparer)
                    : ordered.ThenByDescending(key, comparer);
            }
        }
        return ordered?.ToList() ?? source;
    }

    public static List<GridGroup<TItem>> Group<TItem>(
        IReadOnlyList<TItem> source,
        IReadOnlyList<GroupDescriptor> groups,
        IReadOnlyDictionary<string, DataGridColumn<TItem>> columns)
    {
        var result = new List<GridGroup<TItem>>();
        if (groups.Count == 0) return result;

        // Only single-level grouping is materialized here (top-level group).
        var group = groups[0];
        if (!columns.TryGetValue(group.ColumnId, out var column) || column.Accessor is null)
            return result;

        var grouped = source
            .GroupBy(item => column.Accessor!.GetValue(item))
            .Select(g => new GridGroup<TItem>
            {
                ColumnId = group.ColumnId,
                Key = g.Key,
                KeyText = column.FormatValue(g.Key),
                Items = g.ToList()
            });

        grouped = group.Direction == SortDirection.Descending
            ? grouped.OrderByDescending(g => g.Key, ValueComparer.Instance)
            : grouped.OrderBy(g => g.Key, ValueComparer.Instance);

        result = grouped.ToList();

        foreach (var g in result)
            g.Aggregates.AddRange(Aggregate(g.Items, columns.Values));

        return result;
    }

    public static List<AggregateResult> Aggregate<TItem>(
        IReadOnlyList<TItem> source,
        IEnumerable<DataGridColumn<TItem>> columns)
    {
        var results = new List<AggregateResult>();
        foreach (var column in columns)
        {
            if (column.Aggregate == AggregateType.None || column.Accessor is null) continue;
            var value = ComputeAggregate(source, column);
            var format = column.AggregateFormat ?? column.Format;
            var formatted = value is IFormattable fmt && !string.IsNullOrEmpty(format)
                ? fmt.ToString(format, CultureInfo.CurrentCulture)
                : value?.ToString() ?? string.Empty;
            results.Add(new AggregateResult
            {
                ColumnId = column.Id,
                Type = column.Aggregate,
                Value = value,
                FormattedValue = formatted
            });
        }
        return results;
    }

    private static object? ComputeAggregate<TItem>(IReadOnlyList<TItem> source, DataGridColumn<TItem> column)
    {
        var accessor = column.Accessor!;
        switch (column.Aggregate)
        {
            case AggregateType.Count:
                return source.Count;
            case AggregateType.Sum:
            case AggregateType.Average:
            {
                double sum = 0; int n = 0;
                foreach (var item in source)
                {
                    if (TryToDouble(accessor.GetValue(item), out var d)) { sum += d; n++; }
                }
                if (column.Aggregate == AggregateType.Sum) return sum;
                return n == 0 ? 0d : sum / n;
            }
            case AggregateType.Min:
            case AggregateType.Max:
            {
                object? best = null;
                foreach (var item in source)
                {
                    var v = accessor.GetValue(item);
                    if (v is null) continue;
                    if (best is null) { best = v; continue; }
                    var cmp = ValueComparer.Instance.Compare(v, best);
                    if (column.Aggregate == AggregateType.Min ? cmp < 0 : cmp > 0) best = v;
                }
                return best;
            }
            default:
                return null;
        }
    }

    private static bool TryToDouble(object? value, out double result)
    {
        result = 0;
        if (value is null) return false;
        try { result = Convert.ToDouble(value, CultureInfo.InvariantCulture); return true; }
        catch { return false; }
    }

    private static bool Matches(object? value, FilterDescriptor filter)
    {
        switch (filter.Operator)
        {
            case FilterOperator.IsEmpty:
                return value is null || string.IsNullOrEmpty(value.ToString());
            case FilterOperator.IsNotEmpty:
                return value is not null && !string.IsNullOrEmpty(value.ToString());
        }

        if (filter.Value is null)
            return true;

        // Numeric / comparable operators
        if (filter.Operator is FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual
            or FilterOperator.LessThan or FilterOperator.LessThanOrEqual
            or FilterOperator.Equals or FilterOperator.NotEquals)
        {
            var cmp = ValueComparer.Instance.Compare(value, CoerceToValueType(value, filter.Value));
            return filter.Operator switch
            {
                FilterOperator.GreaterThan => cmp > 0,
                FilterOperator.GreaterThanOrEqual => cmp >= 0,
                FilterOperator.LessThan => cmp < 0,
                FilterOperator.LessThanOrEqual => cmp <= 0,
                FilterOperator.Equals => cmp == 0,
                FilterOperator.NotEquals => cmp != 0,
                _ => true
            };
        }

        // String operators
        var text = value?.ToString() ?? string.Empty;
        var term = filter.Value.ToString() ?? string.Empty;
        return filter.Operator switch
        {
            FilterOperator.Contains => text.Contains(term, StringComparison.OrdinalIgnoreCase),
            FilterOperator.DoesNotContain => !text.Contains(term, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => text.StartsWith(term, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => text.EndsWith(term, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static object? CoerceToValueType(object? sample, object filterValue)
    {
        if (sample is null) return filterValue;
        var target = Nullable.GetUnderlyingType(sample.GetType()) ?? sample.GetType();
        if (target.IsInstanceOfType(filterValue)) return filterValue;
        try
        {
            if (target.IsEnum)
                return filterValue is string s ? Enum.Parse(target, s, true) : Enum.ToObject(target, filterValue);
            return Convert.ChangeType(filterValue, target, CultureInfo.CurrentCulture);
        }
        catch { return filterValue; }
    }
}

/// <summary>Null-safe comparer that orders nulls first and falls back to string comparison.</summary>
internal sealed class ValueComparer : IComparer<object?>
{
    public static readonly ValueComparer Instance = new();

    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        if (x is IComparable cx && x.GetType() == y.GetType())
            return cx.CompareTo(y);

        if (x is IComparable cx2)
        {
            try { return cx2.CompareTo(Convert.ChangeType(y, x.GetType())); }
            catch { /* fall through */ }
        }

        return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
