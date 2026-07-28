using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;
using Microsoft.Win32;

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
        var list = new List<ReportRow>();
        switch (ReportType)
        {
            case "Daily Sales":
                list = _dashboard.GetDailySales(SelectedYear)
                    .Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                break;
            case "Weekly Sales":
                list = _dashboard.GetWeeklySales(SelectedYear)
                    .Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                break;
            case "Monthly Sales":
                list = _dashboard.GetMonthlySales(SelectedYear)
                    .Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                break;
            case "Yearly Sales":
                list = _dashboard.GetYearlySales()
                    .Select(p => new ReportRow { Col1 = p.Label, Col2 = p.Amount.ToString("N2") }).ToList();
                break;
            case "Stock In History":
                list = _inventory.GetHistory()
                    .Where(h => h.MovementType == "IN")
                    .Select(h => new ReportRow
                    {
                        Col1 = h.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        Col2 = h.ProductName,
                        Col3 = h.QtyChange.ToString("N3"),
                        Col4 = h.Remarks ?? ""
                    }).ToList();
                break;
            case "Stock Out History":
                list = _inventory.GetHistory()
                    .Where(h => h.MovementType == "OUT")
                    .Select(h => new ReportRow
                    {
                        Col1 = h.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        Col2 = h.ProductName,
                        Col3 = h.QtyChange.ToString("N3"),
                        Col4 = h.Remarks ?? ""
                    }).ToList();
                break;
            default:
                list = _sales.GetHistory(year: SelectedYear)
                    .Select(s => new ReportRow
                    {
                        Col1 = s.SaleDate.ToString("yyyy-MM-dd HH:mm"),
                        Col2 = s.InvoiceNo,
                        Col3 = s.CashierName,
                        Col4 = s.TotalDue.ToString("N2")
                    }).ToList();
                break;
        }

        Rows = new ObservableCollection<ReportRow>(list);
        HasRows = Rows.Count > 0;
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (Rows.Count == 0)
        {
            DialogService.ShowInfo("Nothing to export.", "Reports");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"4KV_{ReportType.Replace(' ', '_')}_{SelectedYear}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Col1,Col2,Col3,Col4");
        foreach (var r in Rows)
            sb.AppendLine($"\"{r.Col1}\",\"{r.Col2}\",\"{r.Col3}\",\"{r.Col4}\"");
        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        DialogService.ShowInfo("CSV exported.", "Reports");
    }
}

public class ReportRow
{
    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
}
