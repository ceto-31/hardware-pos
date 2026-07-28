using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HardwarePOS.ViewModels;

namespace HardwarePOS.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncColumns();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReportsViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is ReportsViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            SyncColumns(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReportsViewModel.Col1Header) or nameof(ReportsViewModel.Col2Header)
            or nameof(ReportsViewModel.Col3Header) or nameof(ReportsViewModel.Col4Header)
            or nameof(ReportsViewModel.ShowCol3) or nameof(ReportsViewModel.ShowCol4))
        {
            SyncColumns();
        }
    }

    private void SyncColumns() => SyncColumns(DataContext as ReportsViewModel);

    private void SyncColumns(ReportsViewModel? vm)
    {
        if (vm is null || ReportGrid.Columns.Count < 4) return;

        ReportGrid.Columns[0].Header = vm.Col1Header;
        ReportGrid.Columns[1].Header = vm.Col2Header;
        ReportGrid.Columns[2].Header = vm.Col3Header;
        ReportGrid.Columns[2].Visibility = vm.ShowCol3 ? Visibility.Visible : Visibility.Collapsed;
        ReportGrid.Columns[3].Header = vm.Col4Header;
        ReportGrid.Columns[3].Visibility = vm.ShowCol4 ? Visibility.Visible : Visibility.Collapsed;
    }
}
