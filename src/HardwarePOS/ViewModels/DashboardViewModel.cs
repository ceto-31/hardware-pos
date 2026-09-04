using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HardwarePOS.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DashboardRepository _dashboard = new();
    private readonly System.Windows.Threading.DispatcherTimer _clock;

    [ObservableProperty] private string _welcomeMessage = string.Empty;
    [ObservableProperty] private string _userDisplay = string.Empty;
    [ObservableProperty] private string _currentDateTime = string.Empty;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private int _bottomPanelColumns = 3;

    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private decimal _todayItemsSold;
    [ObservableProperty] private decimal _todayGrossProfit;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _totalSuppliers;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _outOfStockCount;
    [ObservableProperty] private int _expiringSoonCount;

    [ObservableProperty] private ObservableCollection<string> _chartRangeOptions = new() { "Daily", "Weekly", "Monthly", "Yearly" };
    [ObservableProperty] private string _selectedChartRange = "Daily";

    [ObservableProperty] private ISeries[] _overviewSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _overviewXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes =
    [
        new Axis { Labeler = v => $"₱{v:N0}", SeparatorsPaint = new SolidColorPaint(new SKColor(226, 232, 240)), TextSize = 11 }
    ];
    [ObservableProperty] private ISeries[] _paymentSeries = Array.Empty<ISeries>();

    [ObservableProperty] private ObservableCollection<TopProductRow> _topProducts = new();
    [ObservableProperty] private ObservableCollection<Product> _inventoryAlerts = new();
    [ObservableProperty] private ObservableCollection<Product> _expirationAlerts = new();
    [ObservableProperty] private ObservableCollection<RecentSaleRow> _recentSales = new();
    [ObservableProperty] private ObservableCollection<Discount> _scheduledDiscounts = new();

    public DashboardViewModel()
    {
        _clock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        _clock.Start();
        CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    }

    partial void OnSelectedChartRangeChanged(string value) => ReloadCharts();

    [RelayCommand]
    public void Load()
    {
        var user = SessionManager.CurrentUser;
        WelcomeMessage = user?.FullName ?? "User";
        UserDisplay = user?.RoleName ?? string.Empty;
        IsAdmin = SessionManager.IsAdmin;
        BottomPanelColumns = IsAdmin ? 5 : 2;

        var summary = _dashboard.GetSummary();
        TodaySales = summary.TodaySales;
        TodayTransactions = summary.TodayTransactions;
        TodayItemsSold = summary.TodayItemsSold;
        TodayGrossProfit = summary.TodayGrossProfit;
        TotalRevenue = summary.TotalRevenue;
        TotalProducts = summary.TotalProducts;
        TotalSuppliers = summary.TotalSuppliers;
        LowStockCount = summary.LowStockCount;
        OutOfStockCount = summary.OutOfStockCount;
        ExpiringSoonCount = summary.ExpiringSoonCount;

        ReloadCharts();
        BuildPaymentDonut();
        TopProducts = new ObservableCollection<TopProductRow>(_dashboard.GetTopProducts(6));
        InventoryAlerts = new ObservableCollection<Product>(_dashboard.GetInventoryAlerts().Take(6));
        ExpirationAlerts = new ObservableCollection<Product>(_dashboard.GetExpirationAlerts().Take(6));
        RecentSales = new ObservableCollection<RecentSaleRow>(_dashboard.GetRecentSales(8));
        ScheduledDiscounts = IsAdmin
            ? new ObservableCollection<Discount>(_dashboard.GetScheduledDiscounts(6))
            : new ObservableCollection<Discount>();
    }

    private void ReloadCharts()
    {
        var year = DateTime.Now.Year;
        var range = string.IsNullOrWhiteSpace(SelectedChartRange) ? "Daily" : SelectedChartRange;
        var points = range switch
        {
            "Weekly" => _dashboard.GetWeeklySales(year),
            "Monthly" => _dashboard.GetMonthlySales(year),
            "Yearly" => _dashboard.GetYearlySales(),
            _ => _dashboard.GetDailySales(year)
        };
        (OverviewSeries, OverviewXAxes) = BuildLine(points, "Sales");
    }

    private void BuildPaymentDonut()
    {
        var cash = Math.Max((double)TodaySales, 0);
        PaymentSeries =
        [
            new PieSeries<double>
            {
                Name = "Cash",
                Values = [cash > 0 ? cash : 0.0001],
                InnerRadius = 70,
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB")),
                Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 3 },
                DataLabelsPaint = null
            }
        ];
    }

    private static (ISeries[] series, Axis[] axes) BuildLine(List<SalesPoint> points, string title)
    {
        var values = points.Select(p => (double)p.Amount).ToArray();
        var labels = points.Select(p => p.Label).ToArray();
        var line = SKColor.Parse("#2563EB");
        ISeries[] series =
        [
            new LineSeries<double>
            {
                Name = title,
                Values = values,
                Fill = new SolidColorPaint(line.WithAlpha(40)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(line) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                Stroke = new SolidColorPaint(line) { StrokeThickness = 2.5f },
                LineSmoothness = 0.35
            }
        ];
        Axis[] axes = [new Axis { Labels = labels, LabelsRotation = 15, TextSize = 11, Padding = new LiveChartsCore.Drawing.Padding(4) }];
        return (series, axes);
    }
}
