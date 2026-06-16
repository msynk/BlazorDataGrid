using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace BlazorDataGrid;

/// <summary>
/// A feature-rich, generic data grid for Blazor: sorting, filtering, paging,
/// virtualization, selection, inline editing, column resize/reorder, frozen
/// columns, grouping, aggregates and theming.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public partial class BlazorDataGrid<TItem> : ComponentBase
{
    // ---------------------------------------------------------------- Data
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Server-side data callback. When set, the grid delegates sort/filter/page to the caller.</summary>
    [Parameter] public Func<BlazorDataGridReadRequest, Task<BlazorDataGridReadResult<TItem>>>? OnRead { get; set; }

    /// <summary>Column definitions and other declarative children.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public bool Loading { get; set; }

    /// <summary>Optional key selector used for selection/edit identity. Defaults to reference equality.</summary>
    [Parameter] public Func<TItem, object>? KeyField { get; set; }

    // ------------------------------------------------------------ Appearance
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }
    /// <summary>Height of the scroll viewport, e.g. "480px". Required for virtualization.</summary>
    [Parameter] public string? Height { get; set; }
    [Parameter] public bool Striped { get; set; } = true;
    [Parameter] public bool Hoverable { get; set; } = true;
    [Parameter] public bool Bordered { get; set; } = true;
    [Parameter] public bool ShowHeader { get; set; } = true;
    [Parameter] public bool ShowFooter { get; set; }
    [Parameter] public BlazorDataGridDirection Direction { get; set; } = BlazorDataGridDirection.Ltr;

    // -------------------------------------------------------- Feature toggles
    [Parameter] public bool Sortable { get; set; } = true;
    [Parameter] public bool MultiSort { get; set; } = true;
    [Parameter] public bool Filterable { get; set; }
    [Parameter] public bool Resizable { get; set; }
    [Parameter] public bool Reorderable { get; set; }
    [Parameter] public bool Groupable { get; set; }
    [Parameter] public bool ShowToolbar { get; set; }
    [Parameter] public bool ShowColumnChooser { get; set; }
    [Parameter] public bool ShowCsvExport { get; set; }

    // ------------------------------------------------------------- Selection
    [Parameter] public BlazorDataGridSelectionMode SelectionMode { get; set; } = BlazorDataGridSelectionMode.None;
    [Parameter] public IReadOnlyList<TItem>? SelectedItems { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<TItem>> SelectedItemsChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }

    // --------------------------------------------------------------- Paging
    [Parameter] public bool Pageable { get; set; }
    [Parameter] public int PageSize { get; set; } = 20;
    [Parameter] public int[] PageSizeOptions { get; set; } = { 10, 20, 50, 100 };
    [Parameter] public BlazorDataGridPagerPosition PagerPosition { get; set; } = BlazorDataGridPagerPosition.Bottom;

    // --------------------------------------------------------- Virtualization
    [Parameter] public bool Virtualize { get; set; }
    [Parameter] public float RowHeight { get; set; } = 36f;

    // -------------------------------------------------------------- Editing
    [Parameter] public bool Editable { get; set; }
    [Parameter] public Func<TItem>? NewItemFactory { get; set; }
    [Parameter] public EventCallback<TItem> OnRowSave { get; set; }
    [Parameter] public EventCallback<TItem> OnRowCancel { get; set; }
    [Parameter] public EventCallback<TItem> OnRowDelete { get; set; }
    [Parameter] public EventCallback<TItem> OnRowCreate { get; set; }

    // ------------------------------------------------------------ Templates
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }
    [Parameter] public RenderFragment? ToolbarTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? DetailTemplate { get; set; }

    // ---------------------------------------------------------------- State
    private readonly List<BlazorDataGridColumn<TItem>> _columns = new();
    private readonly Dictionary<string, BlazorDataGridColumn<TItem>> _columnsById = new();
    private readonly List<BlazorDataGridSortDescriptor> _sorts = new();
    private readonly List<BlazorDataGridFilterDescriptor> _filters = new();
    private readonly List<BlazorDataGridGroupDescriptor> _groups = new();
    private readonly HashSet<TItem> _selected = new();
    private readonly HashSet<object> _expandedDetails = new();
    private readonly HashSet<object> _collapsedGroups = new();

    private IReadOnlyList<TItem> _view = Array.Empty<TItem>();      // filtered + sorted (full)
    private IReadOnlyList<TItem> _pageItems = Array.Empty<TItem>(); // current page slice
    private List<BlazorDataGridGroup<TItem>>? _viewGroups;
    private List<BlazorDataGridAggregateResult> _footerAggregates = new();
    private int _totalCount;

    private int _currentPage = 1;
    private int _effectivePageSize;
    private bool _showColumnChooserPanel;

    // Tracks external data inputs so we only (re)load when they actually change,
    // rather than on every parent re-render (which would loop in server mode).
    private bool _dataInitialized;
    private IEnumerable<TItem>? _lastItems;
    private int _lastPageSize;

    // editing
    private TItem? _editItem;
    private TItem? _pendingNew;
    private bool _isNewItem;
    private Dictionary<string, object?>? _editSnapshot;

    // resizing
    private BlazorDataGridColumn<TItem>? _resizingColumn;
    private double _resizeStartX;
    private double _resizeStartWidth;

    // reordering
    private BlazorDataGridColumn<TItem>? _dragColumn;

    internal IReadOnlyList<BlazorDataGridColumn<TItem>> AllColumns => _columns;
    internal IReadOnlyList<BlazorDataGridColumn<TItem>> VisibleColumns => _columns.Where(c => c.Visible).ToList();
    internal IReadOnlyList<BlazorDataGridSortDescriptor> Sorts => _sorts;
    internal bool IsServerMode => OnRead is not null;
    internal bool IsEditing(TItem item) => _editItem is not null && KeyEquals(_editItem, item);
    internal bool IsRowSelected(TItem item) => _selected.Contains(item);
    internal int TotalCount => IsServerMode ? _totalCount : _view.Count;
    internal int TotalPages => _effectivePageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)_effectivePageSize));
    internal int CurrentPage => _currentPage;
    internal IReadOnlyList<BlazorDataGridAggregateResult> FooterAggregates => _footerAggregates;
    internal TItem? PendingNewItem => _pendingNew;

    // ------------------------------------------------- Column registration
    internal void AddColumn(BlazorDataGridColumn<TItem> column)
    {
        if (_columns.Contains(column)) return;
        _columns.Add(column);
        _columnsById[column.Id] = column;
        InvokeAsync(StateHasChanged);
    }

    internal void RemoveColumn(BlazorDataGridColumn<TItem> column)
    {
        if (_columns.Remove(column))
        {
            _columnsById.Remove(column.Id);
            InvokeAsync(StateHasChanged);
        }
    }

    // ------------------------------------------------------- Lifecycle
    protected override async Task OnParametersSetAsync()
    {
        _effectivePageSize = Pageable ? Math.Max(1, PageSize) : int.MaxValue;

        if (SelectedItems is not null)
        {
            _selected.Clear();
            foreach (var i in SelectedItems) _selected.Add(i);
        }

        // Only (re)load data when an external input that affects it actually changes.
        // Refreshing on every parameter set would cause an infinite loop in server mode:
        // OnRead -> caller StateHasChanged -> parent re-render -> OnParametersSetAsync -> OnRead...
        var inputsChanged = !ReferenceEquals(Items, _lastItems) || PageSize != _lastPageSize;
        if (!_dataInitialized || inputsChanged)
        {
            _lastItems = Items;
            _lastPageSize = PageSize;
            _dataInitialized = true;
            await RefreshAsync();
        }
    }

    /// <summary>Recomputes the data view (filter → sort → group → page).</summary>
    public async Task RefreshAsync()
    {
        if (IsServerMode)
        {
            await LoadServerDataAsync();
        }
        else
        {
            ProcessClientData();
        }
        StateHasChanged();
    }

    private void ProcessClientData()
    {
        var source = Items ?? Enumerable.Empty<TItem>();
        var filtered = BlazorDataGridDataProcessor.Filter(source, _filters, _columnsById);
        _view = BlazorDataGridDataProcessor.Sort(filtered, _sorts, _columnsById);
        _footerAggregates = BlazorDataGridDataProcessor.Aggregate(_view, _columns);

        ClampPage();

        if (_groups.Count > 0)
        {
            _viewGroups = BlazorDataGridDataProcessor.Group(_view, _groups, _columnsById);
            _pageItems = _view; // grouping ignores paging in this implementation
        }
        else
        {
            _viewGroups = null;
            _pageItems = Pageable
                ? _view.Skip((_currentPage - 1) * _effectivePageSize).Take(_effectivePageSize).ToList()
                : _view;
        }
    }

    private async Task LoadServerDataAsync()
    {
        var request = new BlazorDataGridReadRequest
        {
            Skip = Pageable ? (_currentPage - 1) * _effectivePageSize : 0,
            Take = Pageable ? _effectivePageSize : null,
            Sorts = _sorts.Where(s => s.Direction != BlazorDataGridSortDirection.None).OrderBy(s => s.Priority).ToList(),
            Filters = _filters.ToList()
        };
        var result = await OnRead!(request);
        _pageItems = result.Items;
        _view = result.Items;
        _totalCount = result.TotalCount;
        _footerAggregates = BlazorDataGridDataProcessor.Aggregate(_pageItems, _columns);
        _viewGroups = null;
        ClampPage();
    }

    private void ClampPage()
    {
        var pages = TotalPages;
        if (_currentPage > pages) _currentPage = pages;
        if (_currentPage < 1) _currentPage = 1;
    }

    // ------------------------------------------------------------- Sorting
    internal bool ColumnSortable(BlazorDataGridColumn<TItem> column)
        => column.HasField && (column.Sortable ?? Sortable);

    internal BlazorDataGridSortDescriptor? GetSort(BlazorDataGridColumn<TItem> column)
        => _sorts.FirstOrDefault(s => s.ColumnId == column.Id);

    internal async Task ToggleSortAsync(BlazorDataGridColumn<TItem> column, bool additive)
    {
        if (!ColumnSortable(column)) return;
        var existing = GetSort(column);

        if (!additive && (!MultiSort || true))
        {
            // Single sort: clear others unless additive
            if (!additive)
            {
                var keep = existing;
                _sorts.Clear();
                if (keep is not null) _sorts.Add(keep);
            }
        }

        if (existing is null)
        {
            _sorts.Add(new BlazorDataGridSortDescriptor { ColumnId = column.Id, Direction = BlazorDataGridSortDirection.Ascending, Priority = _sorts.Count + 1 });
        }
        else if (existing.Direction == BlazorDataGridSortDirection.Ascending)
        {
            existing.Direction = BlazorDataGridSortDirection.Descending;
        }
        else
        {
            _sorts.Remove(existing);
        }
        Reprioritize();
        await RefreshAsync();
    }

    private void Reprioritize()
    {
        for (int i = 0; i < _sorts.Count; i++) _sorts[i].Priority = i + 1;
    }

    // ----------------------------------------------------------- Filtering
    internal bool ColumnFilterable(BlazorDataGridColumn<TItem> column)
        => column.HasField && (column.Filterable ?? Filterable);

    internal BlazorDataGridFilterDescriptor? GetFilter(BlazorDataGridColumn<TItem> column)
        => _filters.FirstOrDefault(f => f.ColumnId == column.Id);

    internal async Task SetFilterAsync(BlazorDataGridColumn<TItem> column, BlazorDataGridFilterOperator op, object? value)
    {
        var existing = GetFilter(column);
        var isEmpty = value is null || (value is string s && s.Length == 0);
        if (isEmpty && op is not (BlazorDataGridFilterOperator.IsEmpty or BlazorDataGridFilterOperator.IsNotEmpty))
        {
            if (existing is not null) _filters.Remove(existing);
        }
        else if (existing is null)
        {
            _filters.Add(new BlazorDataGridFilterDescriptor { ColumnId = column.Id, Operator = op, Value = value });
        }
        else
        {
            existing.Operator = op;
            existing.Value = value;
        }
        _currentPage = 1;
        await RefreshAsync();
    }

    public async Task ClearFiltersAsync()
    {
        _filters.Clear();
        await RefreshAsync();
    }

    // ----------------------------------------------------------- Grouping
    internal bool ColumnGroupable(BlazorDataGridColumn<TItem> column)
        => column.HasField && (column.Groupable ?? Groupable);

    internal bool IsGrouped(BlazorDataGridColumn<TItem> column) => _groups.Any(g => g.ColumnId == column.Id);

    internal async Task ToggleGroupAsync(BlazorDataGridColumn<TItem> column)
    {
        var existing = _groups.FirstOrDefault(g => g.ColumnId == column.Id);
        if (existing is null)
        {
            _groups.Clear(); // single-level grouping
            _groups.Add(new BlazorDataGridGroupDescriptor { ColumnId = column.Id });
        }
        else
        {
            _groups.Remove(existing);
        }
        await RefreshAsync();
    }

    internal bool IsGroupCollapsed(BlazorDataGridGroup<TItem> group) => _collapsedGroups.Contains(group.Key ?? NullKey);
    private static readonly object NullKey = new();
    internal void ToggleGroup(BlazorDataGridGroup<TItem> group)
    {
        var key = group.Key ?? NullKey;
        if (!_collapsedGroups.Add(key)) _collapsedGroups.Remove(key);
        StateHasChanged();
    }

    // ---------------------------------------------------------- Selection
    internal bool SelectionEnabled => SelectionMode != BlazorDataGridSelectionMode.None;

    internal async Task ToggleRowSelectionAsync(TItem item, bool? value = null)
    {
        if (SelectionMode == BlazorDataGridSelectionMode.None) return;
        var selected = value ?? !_selected.Contains(item);
        if (SelectionMode == BlazorDataGridSelectionMode.Single)
        {
            _selected.Clear();
            if (selected) _selected.Add(item);
        }
        else
        {
            if (selected) _selected.Add(item); else _selected.Remove(item);
        }
        await NotifySelectionAsync();
    }

    internal bool AllPageSelected => _pageItems.Count > 0 && _pageItems.All(_selected.Contains);
    internal bool SomePageSelected => _pageItems.Any(_selected.Contains) && !AllPageSelected;

    internal async Task ToggleSelectAllAsync(bool value)
    {
        foreach (var item in _pageItems)
        {
            if (value) _selected.Add(item); else _selected.Remove(item);
        }
        await NotifySelectionAsync();
    }

    private async Task NotifySelectionAsync()
    {
        if (SelectedItemsChanged.HasDelegate)
            await SelectedItemsChanged.InvokeAsync(_selected.ToList());
        StateHasChanged();
    }

    internal async Task HandleRowClickAsync(TItem item)
    {
        if (OnRowClick.HasDelegate) await OnRowClick.InvokeAsync(item);
        if (SelectionMode == BlazorDataGridSelectionMode.Single && _editItem is null)
            await ToggleRowSelectionAsync(item, true);
    }

    // ------------------------------------------------------ Detail rows
    internal bool IsDetailExpanded(TItem item) => _expandedDetails.Contains(GetKey(item));
    internal void ToggleDetail(TItem item)
    {
        var key = GetKey(item);
        if (!_expandedDetails.Add(key)) _expandedDetails.Remove(key);
        StateHasChanged();
    }

    // ---------------------------------------------------------- Editing
    internal bool ColumnEditable(BlazorDataGridColumn<TItem> column)
        => column.HasField && column.Accessor?.CanWrite == true && (column.Editable ?? Editable);

    internal void BeginEdit(TItem item)
    {
        _editItem = item;
        _isNewItem = false;
        SnapshotEdit(item);
        StateHasChanged();
    }

    internal async Task AddNewRowAsync()
    {
        if (NewItemFactory is null) return;
        var item = NewItemFactory();
        _pendingNew = item;
        _editItem = item;
        _isNewItem = true;
        _editSnapshot = null;
        if (OnRowCreate.HasDelegate) await OnRowCreate.InvokeAsync(item);
        StateHasChanged();
    }

    private void SnapshotEdit(TItem item)
    {
        _editSnapshot = new Dictionary<string, object?>();
        foreach (var col in _columns.Where(ColumnEditable))
            _editSnapshot[col.Id] = col.GetValue(item);
    }

    internal async Task CommitEditAsync()
    {
        if (_editItem is null) return;
        var item = _editItem;
        _editItem = default;
        _pendingNew = default;
        _editSnapshot = null;
        _isNewItem = false;
        if (OnRowSave.HasDelegate) await OnRowSave.InvokeAsync(item);
        await RefreshAsync();
    }

    internal async Task CancelEditAsync()
    {
        if (_editItem is null) return;
        var item = _editItem;
        if (!_isNewItem && _editSnapshot is not null)
        {
            foreach (var (colId, value) in _editSnapshot)
                if (_columnsById.TryGetValue(colId, out var col))
                    col.Accessor?.SetValue(item, value);
        }
        _editItem = default;
        _pendingNew = default;
        _editSnapshot = null;
        _isNewItem = false;
        if (OnRowCancel.HasDelegate) await OnRowCancel.InvokeAsync(item);
        StateHasChanged();
    }

    internal async Task DeleteRowAsync(TItem item)
    {
        if (OnRowDelete.HasDelegate) await OnRowDelete.InvokeAsync(item);
        _selected.Remove(item);
        await RefreshAsync();
    }

    internal void SetEditValue(BlazorDataGridColumn<TItem> column, object? value)
    {
        if (_editItem is null) return;
        column.Accessor?.SetValue(_editItem, value);
    }

    // ---------------------------------------------------------- Resizing
    internal void StartResize(BlazorDataGridColumn<TItem> column, double clientX)
    {
        _resizingColumn = column;
        _resizeStartX = clientX;
        _resizeStartWidth = column.ResizedWidth ?? ParseInitialWidth(column);
        StateHasChanged();
    }

    internal void OnResizeMove(double clientX)
    {
        if (_resizingColumn is null) return;
        var delta = clientX - _resizeStartX;
        if (Direction == BlazorDataGridDirection.Rtl) delta = -delta;
        var newWidth = Math.Max(_resizingColumn.MinWidth, _resizeStartWidth + delta);
        _resizingColumn.ResizedWidth = newWidth;
        StateHasChanged();
    }

    internal void EndResize()
    {
        _resizingColumn = null;
        StateHasChanged();
    }

    internal bool IsResizing => _resizingColumn is not null;

    private static double ParseInitialWidth(BlazorDataGridColumn<TItem> column)
    {
        if (!string.IsNullOrEmpty(column.Width) && column.Width.EndsWith("px")
            && double.TryParse(column.Width[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out var px))
            return px;
        return 150;
    }

    // -------------------------------------------------------- Reordering
    internal void StartColumnDrag(BlazorDataGridColumn<TItem> column) => _dragColumn = column;

    internal void DropColumn(BlazorDataGridColumn<TItem> target)
    {
        if (_dragColumn is null || _dragColumn == target) { _dragColumn = null; return; }
        var from = _columns.IndexOf(_dragColumn);
        var to = _columns.IndexOf(target);
        if (from < 0 || to < 0) { _dragColumn = null; return; }
        _columns.RemoveAt(from);
        _columns.Insert(to, _dragColumn);
        _dragColumn = null;
        StateHasChanged();
    }

    internal bool ColumnResizable(BlazorDataGridColumn<TItem> column) => column.Resizable ?? Resizable;
    internal bool ColumnReorderable(BlazorDataGridColumn<TItem> column) => column.Reorderable ?? Reorderable;

    // ------------------------------------------------------------- Paging
    internal async Task GoToPageAsync(int page)
    {
        _currentPage = Math.Clamp(page, 1, TotalPages);
        await RefreshAsync();
    }

    internal async Task SetPageSizeAsync(int size)
    {
        PageSize = size;
        _effectivePageSize = Math.Max(1, size);
        _currentPage = 1;
        await RefreshAsync();
    }

    // ------------------------------------------------------- Column chooser
    internal void ToggleColumnChooser() { _showColumnChooserPanel = !_showColumnChooserPanel; StateHasChanged(); }
    internal async Task SetColumnVisibilityAsync(BlazorDataGridColumn<TItem> column, bool visible)
    {
        column.Visible = visible;
        await RefreshAsync();
    }

    // ----------------------------------------------------------- Identity
    private object GetKey(TItem item) => KeyField?.Invoke(item) ?? item!;
    private bool KeyEquals(TItem a, TItem b)
        => KeyField is not null ? Equals(KeyField(a), KeyField(b)) : EqualityComparer<TItem>.Default.Equals(a, b);

    // ----------------------------------------------------------- CSV export
    /// <summary>Builds a CSV string of the current (filtered/sorted) data.</summary>
    public string ToCsv()
    {
        var cols = VisibleColumns.Where(c => c.HasField).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.DisplayTitle))));
        var rows = IsServerMode ? _pageItems : _view;
        foreach (var item in rows)
            sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.GetFormattedValue(item)))));
        return sb.ToString();

        static string Escape(string v)
            => v.Contains(',') || v.Contains('"') || v.Contains('\n')
                ? "\"" + v.Replace("\"", "\"\"") + "\""
                : v;
    }

    // ----------------------------------------------------- Layout helpers
    internal bool HasSelectColumn => SelectionMode == BlazorDataGridSelectionMode.Multiple;
    internal bool HasDetailColumn => DetailTemplate is not null;
    internal bool HasCommandColumn => Editable;

    private string ColumnWidthToken(BlazorDataGridColumn<TItem> column)
    {
        if (column.ResizedWidth is { } w) return $"{w.ToString(CultureInfo.InvariantCulture)}px";
        if (!string.IsNullOrEmpty(column.Width)) return column.Width!;
        return "minmax(120px, 1fr)";
    }

    /// <summary>Builds the CSS grid template-columns value for the whole row layout.</summary>
    private string BuildGridTemplate()
    {
        var parts = new List<string>();
        if (HasDetailColumn) parts.Add("44px");
        if (HasSelectColumn) parts.Add("44px");
        foreach (var c in VisibleColumns) parts.Add(ColumnWidthToken(c));
        if (HasCommandColumn) parts.Add("minmax(150px, max-content)");
        return string.Join(" ", parts);
    }

    private int TotalColumnSpan =>
        VisibleColumns.Count + (HasDetailColumn ? 1 : 0) + (HasSelectColumn ? 1 : 0) + (HasCommandColumn ? 1 : 0);

    internal string SelectStickyStyle => HasDetailColumn ? "left:44px;" : "left:0;";

    private string HeaderCellClass(BlazorDataGridColumn<TItem> column)
    {
        var c = "bdg-hcell " + AlignClass(column.Align);
        if (column.Frozen) c += " bdg-sticky";
        if (ColumnSortable(column)) c += " bdg-sortable";
        if (!string.IsNullOrEmpty(column.HeaderClass)) c += " " + column.HeaderClass;
        return c;
    }

    private string RootClasses()
    {
        var c = "bdg";
        if (Bordered) c += " bdg-bordered";
        if (Striped) c += " bdg-striped";
        if (Hoverable) c += " bdg-hoverable";
        if (Direction == BlazorDataGridDirection.Rtl) c += " bdg-rtl";
        if (!string.IsNullOrEmpty(Class)) c += " " + Class;
        return c;
    }

    internal static string AlignClass(BlazorDataGridColumnAlign a) => a switch
    {
        BlazorDataGridColumnAlign.Center => "bdg-center",
        BlazorDataGridColumnAlign.Right => "bdg-right",
        _ => ""
    };

    private double SpecialStickyWidth => (HasDetailColumn ? 44 : 0) + (HasSelectColumn ? 44 : 0);

    private double ColumnPixelWidth(BlazorDataGridColumn<TItem> column)
    {
        if (column.ResizedWidth is { } w) return w;
        return ParseInitialWidth(column);
    }

    /// <summary>Sticky left offset (in px) for a frozen data column.</summary>
    internal double FrozenOffset(BlazorDataGridColumn<TItem> column)
    {
        double offset = SpecialStickyWidth;
        foreach (var c in VisibleColumns)
        {
            if (c == column) break;
            if (c.Frozen) offset += ColumnPixelWidth(c);
        }
        return offset;
    }

    internal string FrozenStyle(BlazorDataGridColumn<TItem> column)
    {
        if (!column.Frozen) return string.Empty;
        var edge = Direction == BlazorDataGridDirection.Rtl ? "right" : "left";
        return $"{edge}:{FrozenOffset(column).ToString(CultureInfo.InvariantCulture)}px;";
    }

    private string AggregateLabel(BlazorDataGridAggregateResult agg) => agg.Type switch
    {
        BlazorDataGridAggregateType.Sum => $"Σ {agg.FormattedValue}",
        BlazorDataGridAggregateType.Average => $"avg {agg.FormattedValue}",
        BlazorDataGridAggregateType.Count => $"count {agg.FormattedValue}",
        BlazorDataGridAggregateType.Min => $"min {agg.FormattedValue}",
        BlazorDataGridAggregateType.Max => $"max {agg.FormattedValue}",
        _ => agg.FormattedValue
    };
}

