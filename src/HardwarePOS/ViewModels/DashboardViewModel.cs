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

    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private decimal _todayItemsSold;
    [ObservableProperty] private decimal _todayGrossProfit;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _totalSuppliers;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StockAttentionCount))] private int _lowStockCount;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StockAttentionCount))] private int _outOfStockCount;
    [ObservableProperty] private int _expiringSoonCount;
    [ObservableProperty] private string _lastUpdated = string.Empty;
    [ObservableProperty] private string _chartPeriodLabel = string.Empty;
    [ObservableProperty] private decimal _chartTotal;
    [ObservableProperty] private bool _hasChartActivity;
    public int StockAttentionCount => LowStockCount + OutOfStockCount;
    public decimal TopProductMaximum => Math.Max(1m, TopProducts.Select(p => p.Revenue).DefaultIfEmpty(0m).Max());
    partial void OnTopProductsChanged(ObservableCollection<TopProductRow> value) => OnPropertyChanged(nameof(TopProductMaximum));

    [ObservableProperty] private ObservableCollection<string> _chartRangeOptions = new() { "Daily", "Weekly", "Monthly", "Yearly" };
    [ObservableProperty] private string _selectedChartRange = "Daily";

    [ObservableProperty] private ISeries[] _overviewSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _overviewXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes =
    [
        new Axis { Labeler = v => $"₱{v:N0}", MinLimit = 0, SeparatorsPaint = new SolidColorPaint(new SKColor(226, 232, 240)), TextSize = 11 }
    ];

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
        TopProducts = new ObservableCollection<TopProductRow>(_dashboard.GetTopProducts(6));
        InventoryAlerts = new ObservableCollection<Product>(_dashboard.GetInventoryAlerts().Take(6));
        ExpirationAlerts = new ObservableCollection<Product>(_dashboard.GetExpirationAlerts().Take(6));
        RecentSales = new ObservableCollection<RecentSaleRow>(_dashboard.GetRecentSales(8));
        ScheduledDiscounts = IsAdmin
            ? new ObservableCollection<Discount>(_dashboard.GetScheduledDiscounts(6))
            : new ObservableCollection<Discount>();
        LastUpdated = $"Updated {DateTime.Now:h:mm tt}";
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
        ChartPeriodLabel = range switch
        {
            "Weekly" => $"Last 12 weeks · {year}",
            "Monthly" => $"January–December {year}",
            "Yearly" => $"{year - 4}–{year}",
            _ => $"Last 14 days · {year}"
        };
        ChartTotal = points.Sum(p => p.Amount);
        HasChartActivity = points.Any(p => p.Amount != 0);
        (OverviewSeries, OverviewXAxes) = BuildLine(points, "Net sales");
        YAxes[0].MinLimit = (double)Math.Min(0m, points.Select(p => p.Amount).DefaultIfEmpty(0m).Min());
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
                GeometrySize = 5,
                GeometryStroke = new SolidColorPaint(line) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                Stroke = new SolidColorPaint(line) { StrokeThickness = 2.5f },
                LineSmoothness = 0
            }
        ];
        Axis[] axes = [new Axis { Labels = labels, LabelsRotation = 0, TextSize = 11, Padding = new LiveChartsCore.Drawing.Padding(4) }];
        return (series, axes);
    }
}
