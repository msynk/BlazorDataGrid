using System.Globalization;

namespace BlazorDataGrid;

/// <summary>
/// Client-side data pipeline: filtering, multi-sorting, grouping and aggregation.
/// </summary>
public static class BlazorDataGridDataProcessor
{
    public static IReadOnlyList<TItem> Filter<TItem>(
        IEnumerable<TItem> source,
        IReadOnlyList<BlazorDataGridFilterDescriptor> filters,
        IReadOnlyDictionary<string, BlazorDataGridColumn<TItem>> columns)
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
        IReadOnlyList<BlazorDataGridSortDescriptor> sorts,
        IReadOnlyDictionary<string, BlazorDataGridColumn<TItem>> columns)
    {
        var active = sorts.Where(s => s.Direction != BlazorDataGridSortDirection.None).OrderBy(s => s.Priority).ToList();
        if (active.Count == 0) return source;

        IOrderedEnumerable<TItem>? ordered = null;
        foreach (var sort in active)
        {
            if (!columns.TryGetValue(sort.ColumnId, out var column) || column.Accessor is null)
                continue;
            var accessor = column.Accessor;
            Func<TItem, object?> key = item => accessor.GetValue(item);
            var comparer = BlazorDataGridValueComparer.Instance;
            if (ordered is null)
            {
                ordered = sort.Direction == BlazorDataGridSortDirection.Ascending
                    ? source.OrderBy(key, comparer)
                    : source.OrderByDescending(key, comparer);
            }
            else
            {
                ordered = sort.Direction == BlazorDataGridSortDirection.Ascending
                    ? ordered.ThenBy(key, comparer)
                    : ordered.ThenByDescending(key, comparer);
            }
        }
        return ordered?.ToList() ?? source;
    }

    public static List<BlazorDataGridGroup<TItem>> Group<TItem>(
        IReadOnlyList<TItem> source,
        IReadOnlyList<BlazorDataGridGroupDescriptor> groups,
        IReadOnlyDictionary<string, BlazorDataGridColumn<TItem>> columns)
    {
        var result = new List<BlazorDataGridGroup<TItem>>();
        if (groups.Count == 0) return result;

        // Only single-level grouping is materialized here (top-level group).
        var group = groups[0];
        if (!columns.TryGetValue(group.ColumnId, out var column) || column.Accessor is null)
            return result;

        var grouped = source
            .GroupBy(item => column.Accessor!.GetValue(item))
            .Select(g => new BlazorDataGridGroup<TItem>
            {
                ColumnId = group.ColumnId,
                Key = g.Key,
                KeyText = column.FormatValue(g.Key),
                Items = g.ToList()
            });

        grouped = group.Direction == BlazorDataGridSortDirection.Descending
            ? grouped.OrderByDescending(g => g.Key, BlazorDataGridValueComparer.Instance)
            : grouped.OrderBy(g => g.Key, BlazorDataGridValueComparer.Instance);

        result = grouped.ToList();

        foreach (var g in result)
            g.Aggregates.AddRange(Aggregate(g.Items, columns.Values));

        return result;
    }

    public static List<BlazorDataGridAggregateResult> Aggregate<TItem>(
        IReadOnlyList<TItem> source,
        IEnumerable<BlazorDataGridColumn<TItem>> columns)
    {
        var results = new List<BlazorDataGridAggregateResult>();
        foreach (var column in columns)
        {
            if (column.Aggregate == BlazorDataGridAggregateType.None || column.Accessor is null) continue;
            var value = ComputeAggregate(source, column);
            var format = column.AggregateFormat ?? column.Format;
            var formatted = value is IFormattable fmt && !string.IsNullOrEmpty(format)
                ? fmt.ToString(format, CultureInfo.CurrentCulture)
                : value?.ToString() ?? string.Empty;
            results.Add(new BlazorDataGridAggregateResult
            {
                ColumnId = column.Id,
                Type = column.Aggregate,
                Value = value,
                FormattedValue = formatted
            });
        }
        return results;
    }

    private static object? ComputeAggregate<TItem>(IReadOnlyList<TItem> source, BlazorDataGridColumn<TItem> column)
    {
        var accessor = column.Accessor!;
        switch (column.Aggregate)
        {
            case BlazorDataGridAggregateType.Count:
                return source.Count;
            case BlazorDataGridAggregateType.Sum:
            case BlazorDataGridAggregateType.Average:
            {
                double sum = 0; int n = 0;
                foreach (var item in source)
                {
                    if (TryToDouble(accessor.GetValue(item), out var d)) { sum += d; n++; }
                }
                if (column.Aggregate == BlazorDataGridAggregateType.Sum) return sum;
                return n == 0 ? 0d : sum / n;
            }
            case BlazorDataGridAggregateType.Min:
            case BlazorDataGridAggregateType.Max:
            {
                object? best = null;
                foreach (var item in source)
                {
                    var v = accessor.GetValue(item);
                    if (v is null) continue;
                    if (best is null) { best = v; continue; }
                    var cmp = BlazorDataGridValueComparer.Instance.Compare(v, best);
                    if (column.Aggregate == BlazorDataGridAggregateType.Min ? cmp < 0 : cmp > 0) best = v;
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

    private static bool Matches(object? value, BlazorDataGridFilterDescriptor filter)
    {
        switch (filter.Operator)
        {
            case BlazorDataGridFilterOperator.IsEmpty:
                return value is null || string.IsNullOrEmpty(value.ToString());
            case BlazorDataGridFilterOperator.IsNotEmpty:
                return value is not null && !string.IsNullOrEmpty(value.ToString());
        }

        if (filter.Value is null)
            return true;

        // Numeric / comparable operators
        if (filter.Operator is BlazorDataGridFilterOperator.GreaterThan or BlazorDataGridFilterOperator.GreaterThanOrEqual
            or BlazorDataGridFilterOperator.LessThan or BlazorDataGridFilterOperator.LessThanOrEqual
            or BlazorDataGridFilterOperator.Equals or BlazorDataGridFilterOperator.NotEquals)
        {
            var cmp = BlazorDataGridValueComparer.Instance.Compare(value, CoerceToValueType(value, filter.Value));
            return filter.Operator switch
            {
                BlazorDataGridFilterOperator.GreaterThan => cmp > 0,
                BlazorDataGridFilterOperator.GreaterThanOrEqual => cmp >= 0,
                BlazorDataGridFilterOperator.LessThan => cmp < 0,
                BlazorDataGridFilterOperator.LessThanOrEqual => cmp <= 0,
                BlazorDataGridFilterOperator.Equals => cmp == 0,
                BlazorDataGridFilterOperator.NotEquals => cmp != 0,
                _ => true
            };
        }

        // String operators
        var text = value?.ToString() ?? string.Empty;
        var term = filter.Value.ToString() ?? string.Empty;
        return filter.Operator switch
        {
            BlazorDataGridFilterOperator.Contains => text.Contains(term, StringComparison.OrdinalIgnoreCase),
            BlazorDataGridFilterOperator.DoesNotContain => !text.Contains(term, StringComparison.OrdinalIgnoreCase),
            BlazorDataGridFilterOperator.StartsWith => text.StartsWith(term, StringComparison.OrdinalIgnoreCase),
            BlazorDataGridFilterOperator.EndsWith => text.EndsWith(term, StringComparison.OrdinalIgnoreCase),
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
