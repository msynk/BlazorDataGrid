**Note**: moved to bit BlazorUI (https://blazorui.bitplatform.dev)

---

# BlazorDataGrid

A feature-rich, **native Blazor** data grid for **.NET 10**, built with minimal JavaScript.

The component takes its feature inspiration from the leading grids on the web
(react-data-grid, MUI X, AG Grid, Syncfusion) and implements them idiomatically in Blazor:

- **CSS Grid** layout for rows/cells
- Blazor's built-in **`Virtualize`** component for large datasets
- **`position: sticky`** for frozen columns and headers
- **Native HTML drag-and-drop** for column reordering
- A **pure-Blazor pointer overlay** for column resizing (zero custom JavaScript)
- **`light-dark()`** CSS + custom properties for theming

> The library ships a single tiny `.js` module used **only** for infinite scrolling —
> the one feature that genuinely needs to read scroll position (which Blazor's scroll
> events don't expose). Everything else, including CSV export (a `data:` URL
> `<a download>` link), works with no JS interop.

## Features

| Area | Capabilities |
|------|--------------|
| Data | Strongly-typed generic binding, nested property paths, server-side `OnRead` |
| Sorting | Single &amp; multi-column (Ctrl/⌘+click) with priority badges, per-column descending-first |
| Filtering | Per-column quick filters + rich `BlazorDataGridFilterOperator` API |
| Paging | Configurable page sizes, top/bottom pager |
| Virtualization | Smooth rendering of 100k+ rows |
| Infinite scrolling | Append the next batch on scroll via `OnLoadMore` — no total count or paging UI needed |
| Selection | Single / multiple, select-all header checkbox, per-row selection disabling, two-way binding |
| Editing | Add / edit / save / cancel / delete, type-aware editors, `EditTemplate` |
| Grouping | Collapsible **multi-level** row groups with per-group aggregates |
| Tree | **Hierarchical tree-grid** via `ChildrenSelector`: expand/collapse, indentation, expand/collapse-all |
| Aggregates | Sum, Average, Count, Min, Max footer rows |
| Columns | Resize (min/max width), reorder, freeze/pin, **header groups**, **cell spanning**, show/hide chooser, alignment, formatting |
| Rows | Fixed or per-row dynamic heights, **drag-and-drop reordering** |
| Events | Row click + **cell click / double-click / context-menu** callbacks |
| Keyboard | **Cell navigation** with roving tabindex: arrows, `Home`/`End`, `Ctrl`+`Home`/`End`, `PageUp`/`PageDown`, `Enter`/`F2` to edit, `Esc` to cancel |
| Templates | Cell, header, editor, footer and expandable detail-row templates |
| Master detail | Expand rows to reveal nested detail content — including a nested grid — via `DetailTemplate` |
| Theming | CSS variable tokens, automatic light/dark, RTL support |
| Export | One-click CSV of the current view |
| A11y | ARIA grid roles, keyboard-friendly controls |

## Project layout

```
src/BlazorDataGrid/                  Razor Class Library (the component)
  BlazorDataGrid.razor(.cs)          Main generic grid component
  BlazorDataGridColumn.cs            Declarative column definition
  BlazorDataGridRow.razor            Row renderer
  BlazorDataGridCellEditor.razor     Default type-aware editor
  Models/                            Enums, descriptors, request/result types
  Infrastructure/                    Compiled property accessors + data pipeline
  wwwroot/blazordatagrid.css         Styles (theming via CSS variables)

src/BlazorDataGrid.Demo/       Blazor Web App (.NET 10) showcasing every feature
src/BlazorDataGrid.slnx        Solution file
```

## Quick start

```razor
@using BlazorDataGrid

<BlazorDataGrid TItem="Product" Items="products"
                Pageable="true" PageSize="10"
                Filterable="true" Sortable="true"
                SelectionMode="BlazorDataGridSelectionMode.Multiple"
                ShowFooter="true">
    <BlazorDataGridColumn TItem="Product" Field="Id" Title="ID" Align="BlazorDataGridColumnAlign.Right" Frozen="true" />
    <BlazorDataGridColumn TItem="Product" Field="Name" />
    <BlazorDataGridColumn TItem="Product" Field="Price" Format="C2" Align="BlazorDataGridColumnAlign.Right"
                          Aggregate="BlazorDataGridAggregateType.Sum" />
</BlazorDataGrid>
```

1. Reference the library from your Blazor app:
   ```xml
   <ProjectReference Include="..\src\BlazorDataGrid\BlazorDataGrid.csproj" />
   ```
2. Add the stylesheet to your host page (`App.razor` / `index.html`):
   ```html
   <link rel="stylesheet" href="_content/BlazorDataGrid/blazordatagrid.css" />
   ```

## Running the demo

```sh
dotnet run --project src/BlazorDataGrid.Demo
```

Then open the printed `http://localhost:...` URL. Use the sidebar to explore each
feature (sorting, filtering, paging, selection, editing, grouping, resize/reorder/freeze,
templates, virtualization, server-side data, theming/RTL).

> The demo uses **Interactive Server** rendering with **prerendering disabled**. This is
> deliberate: prerendering would try to render very large virtualized datasets on the
> server before the client connects.

## Server-side data

Set the `OnRead` callback to take over sorting/filtering/paging (e.g. against a database):

```csharp
async Task<BlazorDataGridReadResult<Product>> Load(BlazorDataGridReadRequest req)
{
    var query = db.Products.AsQueryable();
    // apply req.Filters / req.Sorts ...
    var total = query.Count();
    var items = query.Skip(req.Skip).Take(req.Take ?? total).ToList();
    return new BlazorDataGridReadResult<Product>(items, total);
}
```

## Theming

Override any token on `.bdg` or a custom class passed via `Class`:

```css
.theme-emerald {
    --bdg-accent: #0f9d58;
    --bdg-header-bg: light-dark(#e7f6ee, #10241a);
    --bdg-row-selected: light-dark(#c9efda, #14402a);
}
```

## Requirements

- .NET 10 SDK
