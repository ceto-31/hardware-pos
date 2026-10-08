using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HardwarePOS.ViewModels;
using LiveChartsCore.SkiaSharpView.WPF;

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
            RebuildChart(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReportsViewModel.PeriodColumnHeader)
            or nameof(ReportsViewModel.Col1Header)
            or nameof(ReportsViewModel.Col2Header)
            or nameof(ReportsViewModel.Col3Header)
            or nameof(ReportsViewModel.Col4Header)
            or nameof(ReportsViewModel.ShowCol3)
            or nameof(ReportsViewModel.ShowCol4)
            or nameof(ReportsViewModel.SelectedView))
        {
            SyncColumns();
        }

        if (e.PropertyName is nameof(ReportsViewModel.HasChart)
            or nameof(ReportsViewModel.ChartSeries))
        {
            RebuildChart();
        }
    }

    private void RebuildChart() => RebuildChart(DataContext as ReportsViewModel);

    private void RebuildChart(ReportsViewModel? vm)
    {
        if (ChartHost is null || vm is null || !vm.HasChart)
        {
            if (ChartHost is not null)
                ChartHost.Content = null;
            return;
        }

        ChartHost.Content = new CartesianChart
        {
            Height = 220,
            Series = vm.ChartSeries,
            XAxes = vm.ChartXAxes,
            YAxes = vm.ChartYAxes,
            LegendPosition = vm.ChartLegendPosition,
            TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
            AnimationsSpeed = TimeSpan.Zero
        };
    }

    private void SyncColumns() => SyncColumns(DataContext as ReportsViewModel);

    private void SyncColumns(ReportsViewModel? vm)
    {
        if (vm is null) return;

        if (SalesGrid.Columns.Count > 0)
            SalesGrid.Columns[0].Header = vm.PeriodColumnHeader;

        if (OtherGrid.Columns.Count < 4) return;

        OtherGrid.Columns[0].Header = vm.Col1Header;
        OtherGrid.Columns[1].Header = vm.Col2Header;
        OtherGrid.Columns[2].Header = vm.Col3Header;
        OtherGrid.Columns[2].Visibility = vm.ShowCol3 ? Visibility.Visible : Visibility.Collapsed;
        OtherGrid.Columns[3].Header = vm.Col4Header;
        OtherGrid.Columns[3].Visibility = vm.ShowCol4 ? Visibility.Visible : Visibility.Collapsed;

        var textHeader = (Style)FindResource("ReportsColumnHeader");
        var moneyHeader = (Style)FindResource("ReportsNumericHeader");
        var textCell = (Style)FindResource("ReportsText");
        var moneyCell = (Style)FindResource("ReportsNumericText");
        for (var i = 0; i < OtherGrid.Columns.Count; i++)
        {
            if (OtherGrid.Columns[i] is not DataGridTextColumn column) continue;
            var right = vm.IsRightAligned(i);
            column.HeaderStyle = right ? moneyHeader : textHeader;
            column.ElementStyle = right ? moneyCell : textCell;
        }
    }
}
