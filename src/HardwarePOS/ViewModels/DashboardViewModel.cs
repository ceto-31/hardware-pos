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
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private decimal _totalRevenue;

    [ObservableProperty] private ISeries[] _dailySeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _weeklySeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _monthlySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _dailyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _weeklyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _monthlyXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes =
    [
        new Axis
        {
            Labeler = value => $"₱{value:N0}",
            SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
        }
    ];

    public DashboardViewModel()
    {
        _clock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy  hh:mm:ss tt");
        _clock.Start();
        CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy  hh:mm:ss tt");
    }

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

        (DailySeries, DailyXAxes) = BuildChart(_dashboard.GetDailySales(7), "Daily Sales", "#1565C0");
        (WeeklySeries, WeeklyXAxes) = BuildChart(_dashboard.GetWeeklySales(8), "Weekly Sales", "#2E7D32");
        (MonthlySeries, MonthlyXAxes) = BuildChart(_dashboard.GetMonthlySales(6), "Monthly Sales", "#E65100");
    }

    private static (ISeries[] series, Axis[] axes) BuildChart(List<SalesPoint> points, string title, string color)
    {
        var values = points.Select(p => (double)p.Amount).ToArray();
        var labels = points.Select(p => p.Label).ToArray();

        ISeries[] series =
        [
            new ColumnSeries<double>
            {
                Name = title,
                Values = values,
                Fill = new SolidColorPaint(SKColor.Parse(color))
            }
        ];

        Axis[] axes =
        [
            new Axis
            {
                Labels = labels,
                LabelsRotation = 15,
                TextSize = 12
            }
        ];

        return (series, axes);
    }
}
