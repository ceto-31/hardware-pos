using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HardwarePOS.Helpers;

public static class Paging
{
    public static readonly DependencyProperty GridSourceProperty = DependencyProperty.RegisterAttached(
        "GridSource", typeof(IPagedList), typeof(Paging), new PropertyMetadata(null, Changed));
    public static void SetGridSource(DependencyObject target, IPagedList value) => target.SetValue(GridSourceProperty, value);
    public static IPagedList? GetGridSource(DependencyObject target) => target.GetValue(GridSourceProperty) as IPagedList;
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not DataGrid grid) return;
        grid.Sorting -= Sort;
        if (e.NewValue is IPagedList) grid.Sorting += Sort;
    }
    private static void Sort(object sender, DataGridSortingEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (GetGridSource(grid) is not IPagedList pager || string.IsNullOrEmpty(e.Column.SortMemberPath)) return;
        e.Handled = true;
        var direction = e.Column.SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        foreach (var column in grid.Columns) column.SortDirection = null;
        pager.Sort(e.Column.SortMemberPath, direction);
        e.Column.SortDirection = direction;
    }
}
