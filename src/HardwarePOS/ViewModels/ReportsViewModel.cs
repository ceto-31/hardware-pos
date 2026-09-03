using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;

namespace HardwarePOS.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly SalesRepository _sales = new();
    private readonly InventoryRepository _inventory = new();
    private readonly DashboardRepository _dashboard = new();

    [ObservableProperty] private ObservableCollection<int> _yearOptions = new();
    [ObservableProperty] private int _selectedYear = DateTime.Now.Year;
    [ObservableProperty] private string _reportType = "Daily Sales";
    [ObservableProperty] private ObservableCollection<string> _reportTypes = new()
    {
        "Daily Sales", "Weekly Sales", "Monthly Sales", "Yearly Sales",
        "Stock In History", "Stock Out History", "All Sales Transactions"
    };
    [ObservableProperty] private ObservableCollection<ReportRow> _rows = new();
    [ObservableProperty] private bool _hasRows;
    [ObservableProperty] private string _col1Header = "Date";
    [ObservableProperty] private string _col2Header = "Total Sales";
    [ObservableProperty] private string _col3Header = string.Empty;
    [ObservableProperty] private string _col4Header = string.Empty;
    [ObservableProperty] private bool _showCol3;
    [ObservableProperty] private bool _showCol4;
    [ObservableProperty] private ISeries[] _chartSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _chartXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _chartYAxes =
    [
        new Axis { Labeler = v => $"₱{v:N0}", SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) }
    ];
    [ObservableProperty] private bool _hasChart;
    [ObservableProperty] private string _chartTitle = string.Empty;

    [RelayCommand]
    public void Load()
    {
        YearOptions = new ObservableCollection<int>(_dashboard.GetAvailableYears());
        if (YearOptions.Count > 0 && !YearOptions.Contains(SelectedYear))
            SelectedYear = YearOptions[0];
        Generate();
    }

    [RelayCommand]
    private void Generate()
    {
        ApplyColumnLayout();

        var list = new List<ReportRow>();
        switch (ReportType)
        {
            case "Daily Sales":
                var daily = _dashboard.GetDailySales(SelectedYear);
                list = daily.Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                BuildSalesLineChart(daily, ReportType, "#2563EB");
                break;
            case "Weekly Sales":
                var weekly = _dashboard.GetWeeklySales(SelectedYear);
                list = weekly.Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                BuildSalesLineChart(weekly, ReportType, "#2E7D32");
                break;
            case "Monthly Sales":
                var monthly = _dashboard.GetMonthlySales(SelectedYear);
                list = monthly.Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                BuildSalesLineChart(monthly, ReportType, "#E65100");
                break;
            case "Yearly Sales":
                var yearly = _dashboard.GetYearlySales();
                list = yearly.Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                BuildSalesLineChart(yearly, ReportType, "#6A1B9A");
                break;
            case "Stock In History":
                var stockIn = _inventory.GetHistory()
                    .Where(h => h.MovementType == "IN")
                    .ToList();
                list = stockIn.Select(h => new ReportRow
                {
                    Col1 = h.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Col2 = h.ProductName,
                    Col3 = h.QtyChange.ToString("N0"),
                    Col4 = h.Remarks ?? ""
                }).ToList();
                BuildStockBarChart(stockIn, "Stock In by Product", "#2563EB");
                break;
            case "Stock Out History":
                var stockOut = _inventory.GetHistory()
                    .Where(h => h.MovementType == "OUT")
                    .ToList();
                list = stockOut.Select(h => new ReportRow
                {
                    Col1 = h.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Col2 = h.ProductName,
                    Col3 = h.QtyChange.ToString("N0"),
                    Col4 = h.Remarks ?? ""
                }).ToList();
                BuildStockBarChart(stockOut, "Stock Out by Product", "#DC2626");
                break;
            default:
                var sales = _sales.GetHistory(year: SelectedYear).ToList();
                list = sales.Select(s => new ReportRow
                {
                    Col1 = s.SaleDate.ToString("yyyy-MM-dd HH:mm"),
                    Col2 = s.InvoiceNo,
                    Col3 = s.CashierName,
                    Col4 = s.TotalDue.ToString("N2")
                }).ToList();
                BuildTransactionBarChart(sales);
                break;
        }

        Rows = new ObservableCollection<ReportRow>(list);
        HasRows = Rows.Count > 0;
        if (!HasRows) ClearChart();
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (Rows.Count == 0)
        {
            DialogService.ShowInfo("Nothing to export.", "Reports");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = $"4KV_{ReportType.Replace(' ', '_')}_{SelectedYear}.pdf"
        };
        if (dialog.ShowDialog() != true) return;

        var headers = new List<string> { Col1Header, Col2Header };
        if (ShowCol3) headers.Add(Col3Header);
        if (ShowCol4) headers.Add(Col4Header);

        try
        {
            ReportPdfExporter.Export(
                dialog.FileName,
                ReportType,
                SelectedYear,
                headers,
                Rows.ToList(),
                BuildSummaryLine());
            DialogService.ShowInfo("PDF exported.", "Reports");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Reports");
        }
    }

    private string? BuildSummaryLine()
    {
        return ReportType switch
        {
            "Daily Sales" or "Weekly Sales" or "Monthly Sales" or "Yearly Sales" =>
                $"Total: ₱{Rows.Sum(r => ParseDecimal(r.Col2)):N2}",
            "Stock In History" or "Stock Out History" =>
                $"Total quantity: {Rows.Sum(r => ParseDecimal(r.Col3)):N0}",
            "All Sales Transactions" =>
                $"Total due: ₱{Rows.Sum(r => ParseDecimal(r.Col4)):N2}",
            _ => null
        };
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) ? result : 0m;

    private void ApplyColumnLayout()
    {
        switch (ReportType)
        {
            case "Daily Sales":
                Col1Header = "Date";
                Col2Header = "Total Sales";
                ShowCol3 = ShowCol4 = false;
                break;
            case "Weekly Sales":
                Col1Header = "Week";
                Col2Header = "Total Sales";
                ShowCol3 = ShowCol4 = false;
                break;
            case "Monthly Sales":
                Col1Header = "Month";
                Col2Header = "Total Sales";
                ShowCol3 = ShowCol4 = false;
                break;
            case "Yearly Sales":
                Col1Header = "Year";
                Col2Header = "Total Sales";
                ShowCol3 = ShowCol4 = false;
                break;
            case "Stock In History":
                Col1Header = "Date";
                Col2Header = "Product";
                Col3Header = "Quantity";
                Col4Header = "Remarks";
                ShowCol3 = ShowCol4 = true;
                break;
            case "Stock Out History":
                Col1Header = "Date";
                Col2Header = "Product";
                Col3Header = "Quantity";
                Col4Header = "Remarks";
                ShowCol3 = ShowCol4 = true;
                break;
            default:
                Col1Header = "Date";
                Col2Header = "Invoice No.";
                Col3Header = "Cashier";
                Col4Header = "Total Due";
                ShowCol3 = ShowCol4 = true;
                break;
        }
    }

    private void BuildSalesLineChart(List<SalesPoint> points, string title, string color)
    {
        if (points.Count == 0)
        {
            ClearChart();
            return;
        }

        ChartTitle = title;
        ChartSeries =
        [
            new LineSeries<double>
            {
                Name = "Total Sales",
                Values = points.Select(p => (double)p.Amount).ToArray(),
                LineSmoothness = 0,
                Fill = null,
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(SKColor.Parse(color)),
                GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2.5f }
            }
        ];
        ChartXAxes =
        [
            new Axis
            {
                Labels = points.Select(p => p.Label).ToArray(),
                LabelsRotation = 15,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B"))
            }
        ];
        ChartYAxes =
        [
            new Axis
            {
                Labeler = v => $"₱{v:N0}",
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
            }
        ];
        HasChart = true;
    }

    private void BuildStockBarChart(List<InventoryLedgerEntry> entries, string title, string color)
    {
        var grouped = entries
            .GroupBy(e => e.ProductName)
            .Select(g => new { Product = g.Key, Qty = (double)g.Sum(x => Math.Abs(x.QtyChange)) })
            .OrderByDescending(x => x.Qty)
            .Take(12)
            .ToList();

        if (grouped.Count == 0)
        {
            ClearChart();
            return;
        }

        ChartTitle = title;
        ChartSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Quantity",
                Values = grouped.Select(g => g.Qty).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse(color)),
                MaxBarWidth = 36
            }
        ];
        ChartXAxes =
        [
            new Axis
            {
                Labels = grouped.Select(g => g.Product).ToArray(),
                LabelsRotation = 20,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B"))
            }
        ];
        ChartYAxes =
        [
            new Axis
            {
                Labeler = v => v.ToString("N0"),
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
            }
        ];
        HasChart = true;
    }

    private void BuildTransactionBarChart(List<SaleHistoryRow> sales)
    {
        var grouped = sales
            .GroupBy(s => s.SaleDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new SalesPoint
            {
                Label = g.Key.ToString("MMM dd"),
                Amount = g.Sum(s => s.TotalDue)
            })
            .ToList();

        if (grouped.Count == 0)
        {
            ClearChart();
            return;
        }

        ChartTitle = "Daily Sales Totals";
        ChartSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Total Sales",
                Values = grouped.Select(p => (double)p.Amount).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB")),
                MaxBarWidth = 36
            }
        ];
        ChartXAxes =
        [
            new Axis
            {
                Labels = grouped.Select(p => p.Label).ToArray(),
                LabelsRotation = 15,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B"))
            }
        ];
        ChartYAxes =
        [
            new Axis
            {
                Labeler = v => $"₱{v:N0}",
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
            }
        ];
        HasChart = true;
    }

    private void ClearChart()
    {
        ChartSeries = Array.Empty<ISeries>();
        ChartXAxes = Array.Empty<Axis>();
        HasChart = false;
        ChartTitle = string.Empty;
    }
}
