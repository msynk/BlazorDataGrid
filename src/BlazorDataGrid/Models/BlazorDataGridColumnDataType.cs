namespace BlazorDataGrid;

/// <summary>The kind of editor/filter rendered for a column based on its data type.</summary>
public enum BlazorDataGridColumnDataType
{
    Auto = 0,
    Text,
    Number,
    Boolean,
    Date,
    Enum
}
