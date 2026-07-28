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
    private readonly ActivityRepository _activity = new();
    private readonly System.Windows.Threading.DispatcherTimer _clock;

    [ObservableProperty] private string _welcomeMessage = string.Empty;
    [ObservableProperty] private string _userDisplay = string.Empty;
    [ObservableProperty] private string _currentDateTime = string.Empty;
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _totalSuppliers;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _outOfStockCount;

    [ObservableProperty] private ObservableCollection<int> _yearOptions = new();
    [ObservableProperty] private int _selectedYear = DateTime.Now.Year;

    [ObservableProperty] private ISeries[] _dailySeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _weeklySeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _monthlySeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _yearlySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _dailyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _weeklyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _monthlyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yearlyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes =
    [
        new Axis { Labeler = v => $"₱{v:N0}", SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) }
    ];

    [ObservableProperty] private ObservableCollection<TopProductRow> _topProducts = new();
    [ObservableProperty] private ObservableCollection<CategorySalesRow> _categorySales = new();
    [ObservableProperty] private ObservableCollection<InventoryLedgerEntry> _recentMoves = new();
    [ObservableProperty] private ObservableCollection<ActivityItem> _activities = new();
    [ObservableProperty] private ObservableCollection<Product> _inventoryAlerts = new();

    public DashboardViewModel()
    {
        _clock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy  hh:mm:ss tt");
        _clock.Start();
        CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy  hh:mm:ss tt");
    }

    partial void OnSelectedYearChanged(int value) => ReloadCharts();

    [RelayCommand]
    public void Load()
    {
        var user = SessionManager.CurrentUser;
        WelcomeMessage = $"Welcome back, {user?.FullName ?? "User"}!";
        UserDisplay = $"{user?.FullName} ({user?.RoleName})";

        var summary = _dashboard.GetSummary();
        TodaySales = summary.TodaySales;
        TodayTransactions = summary.TodayTransactions;
        TotalRevenue = summary.TotalRevenue;
        TotalProducts = summary.TotalProducts;
        TotalSuppliers = summary.TotalSuppliers;
        LowStockCount = summary.LowStockCount;
        OutOfStockCount = summary.OutOfStockCount;

        YearOptions = new ObservableCollection<int>(_dashboard.GetAvailableYears());
        if (!YearOptions.Contains(SelectedYear) && YearOptions.Count > 0)
            SelectedYear = YearOptions[0];

        ReloadCharts();
        TopProducts = new ObservableCollection<TopProductRow>(_dashboard.GetTopProducts());
        CategorySales = new ObservableCollection<CategorySalesRow>(_dashboard.GetSalesByCategory());
        RecentMoves = new ObservableCollection<InventoryLedgerEntry>(_dashboard.GetRecentStockMoves());
        InventoryAlerts = new ObservableCollection<Product>(_dashboard.GetInventoryAlerts());
        Activities = new ObservableCollection<ActivityItem>(
            _activity.GetRecent().Select(a => new ActivityItem
            {
                ActivityType = a.Type,
                Description = a.Description,
                UserName = a.User,
                CreatedAt = a.At
            }));
    }

    private void ReloadCharts()
    {
        (DailySeries, DailyXAxes) = BuildLine(_dashboard.GetDailySales(SelectedYear), "Daily", "#1565C0");
        (WeeklySeries, WeeklyXAxes) = BuildLine(_dashboard.GetWeeklySales(SelectedYear), "Weekly", "#2E7D32");
        (MonthlySeries, MonthlyXAxes) = BuildLine(_dashboard.GetMonthlySales(SelectedYear), "Monthly", "#E65100");
        (YearlySeries, YearlyXAxes) = BuildLine(_dashboard.GetYearlySales(), "Yearly", "#6A1B9A");
    }

    private static (ISeries[] series, Axis[] axes) BuildLine(List<SalesPoint> points, string title, string color)
    {
        var values = points.Select(p => (double)p.Amount).ToArray();
        var labels = points.Select(p => p.Label).ToArray();
        ISeries[] series =
        [
            new LineSeries<double>
            {
                Name = title,
                Values = values,
                Fill = null,
                GeometrySize = 6,
                Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 }
            }
        ];
        Axis[] axes = [new Axis { Labels = labels, LabelsRotation = 15, TextSize = 11 }];
        return (series, axes);
    }
}
