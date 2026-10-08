using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;

namespace HardwarePOS.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private const string SalesColor = "#2563EB";
    private const string ToDateColor = "#93C5FD";

    private readonly DashboardRepository _dashboard = new();
    private readonly SalesReportRepository _salesReports = new();
    private readonly InventoryRepository _inventory = new();
    private readonly SalesRepository _salesRepo = new();

    public bool IsAdmin => SessionManager.IsAdmin;
    [ObservableProperty] private bool _singleDayDetail;
    [ObservableProperty] private bool _rankByQuantity;
    [ObservableProperty] private ObservableCollection<ProductSalesRow> _topProducts = new();
    [ObservableProperty] private ObservableCollection<SalesDetailRow> _dailyDetails = new();
    private List<ProductSalesRow> _productSource = [];
    public bool ShowDailyDetails => IsToday || (IsDaily && SingleDayDetail);
    public bool ShowDailyTo => IsDaily && !SingleDayDetail;
    public bool ShowChartSection => !ShowDailyDetails;
    public string ProductRankingNote => RankByQuantity
        ? "Ranked by net quantity; quantities use each product's own unit and are not summed across units."
        : "Ranked by net sales amount, including allocated tax and discounts. Refunds reduce the period in which they occur.";
    partial void OnRankByQuantityChanged(bool value) { SortProducts(); OnPropertyChanged(nameof(ProductRankingNote)); }
    partial void OnSingleDayDetailChanged(bool value)
    {
        if(value) DailyTo=DailyFrom;
        OnPropertyChanged(nameof(ShowDailyDetails)); OnPropertyChanged(nameof(ShowDailyTo)); RequestRefresh();
        OnPropertyChanged(nameof(ShowChartSection));
    }
    private void SortProducts() => TopProducts=new ObservableCollection<ProductSalesRow>(
        (RankByQuantity?_productSource.OrderByDescending(p=>p.NetQuantity).ThenByDescending(p=>p.NetSales):_productSource.OrderByDescending(p=>p.NetSales)).Take(10));
    private int _requestId;
    private bool _suppress;
    private SalesReportResult? _sales;
    private List<ReportRow> _otherSource = [];

    [ObservableProperty] private ReportView _selectedView = ReportView.Daily;
    [ObservableProperty] private ObservableCollection<int> _yearOptions = new();
    [ObservableProperty] private DateTime? _dailyFrom;
    [ObservableProperty] private DateTime? _dailyTo;
    [ObservableProperty] private int _weeklyYear;
    [ObservableProperty] private int _monthlyYear;
    [ObservableProperty] private int _yearFrom;
    [ObservableProperty] private int _yearTo;
    [ObservableProperty] private int _otherYear;
    [ObservableProperty] private bool _hideZeroRows;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private string _reportTitle = "Daily sales";
    [ObservableProperty] private string _periodDescription = string.Empty;
    [ObservableProperty] private string _tableTitle = "Daily sales by date";
    [ObservableProperty] private string _periodColumnHeader = "Date";
    [ObservableProperty] private string _footerText = string.Empty;
    [ObservableProperty] private string _chartTitle = "Sales";
    [ObservableProperty] private string _chartCaption = string.Empty;
    [ObservableProperty] private string _chartEmptyMessage = string.Empty;
    [ObservableProperty] private bool _hasChart;
    [ObservableProperty] private LegendPosition _chartLegendPosition = LegendPosition.Hidden;

    [ObservableProperty] private ObservableCollection<ReportSummaryItem> _summaryItems = new();
    [ObservableProperty] private ObservableCollection<SalesPeriodRow> _visibleSalesRows = new();
    [ObservableProperty] private ObservableCollection<ReportRow> _otherRows = new();

    [ObservableProperty] private string _col1Header = "Date";
    [ObservableProperty] private string _col2Header = "Net Sales";
    [ObservableProperty] private string _col3Header = string.Empty;
    [ObservableProperty] private string _col4Header = string.Empty;
    [ObservableProperty] private bool _showCol3;
    [ObservableProperty] private bool _showCol4;

    [ObservableProperty] private ISeries[] _chartSeries = [];
    [ObservableProperty] private Axis[] _chartXAxes = [];
    [ObservableProperty] private Axis[] _chartYAxes = [];

    public bool IsSalesView => SelectedView is ReportView.Today or ReportView.Daily or ReportView.Weekly or ReportView.Monthly or ReportView.Yearly;
    public bool IsToday => SelectedView == ReportView.Today;
    public bool IsDaily => SelectedView == ReportView.Daily;
    public bool IsWeekly => SelectedView == ReportView.Weekly;
    public bool IsMonthly => SelectedView == ReportView.Monthly;
    public bool IsYearly => SelectedView == ReportView.Yearly;
    public bool IsOther => !IsSalesView;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasValidation => !string.IsNullOrEmpty(ValidationMessage);
    public bool ShowResults => !HasError && !HasValidation;
    public bool ShowChartEmpty => ShowResults && !HasChart && ChartEmptyMessage.Length > 0;
    public bool HasChartCaption => ChartCaption.Length > 0;
    public bool CanExport => !IsLoading && !HasValidation && !HasError && HasExportRows;

    public bool IsRightAligned(int column) => SelectedView switch
    {
        ReportView.StockIn => column == 3,
        ReportView.StockOut => column == 2,
        ReportView.Transactions => column == 3,
        _ => false
    };

    private bool HasExportRows => IsSalesView
        ? _sales is { Periods.Count: > 0 }
        : _otherSource.Count > 0;

    public void Load()
    {
        _suppress = true;
        var today = DateTime.Today;
        var available = _dashboard.GetAvailableYears();
        var earliest = available.Count == 0 ? today.Year : available.Min();
        var first = Math.Min(earliest, today.Year - 4);
        YearOptions = new ObservableCollection<int>(Enumerable.Range(first, today.Year - first + 1).Reverse());
        SelectedView = ReportView.Daily;
        DailyFrom = today.AddDays(-29);
        DailyTo = today;
        WeeklyYear = today.Year;
        MonthlyYear = today.Year;
        YearFrom = today.Year - 4;
        YearTo = today.Year;
        OtherYear = today.Year;
        HideZeroRows = false;
        _suppress = false;
        _ = RefreshAsync();
    }

    partial void OnSelectedViewChanged(ReportView value)
    {
        NotifyViewFlags();
        RequestRefresh();
    }

    partial void OnDailyFromChanged(DateTime? value) { if(SingleDayDetail) DailyTo=value; RequestRefresh(); }
    partial void OnDailyToChanged(DateTime? value) => RequestRefresh();
    partial void OnWeeklyYearChanged(int value) => RequestRefresh();
    partial void OnMonthlyYearChanged(int value) => RequestRefresh();
    partial void OnYearFromChanged(int value) => RequestRefresh();
    partial void OnYearToChanged(int value) => RequestRefresh();
    partial void OnOtherYearChanged(int value) => RequestRefresh();

    partial void OnHideZeroRowsChanged(bool value)
    {
        if (_suppress || _sales is null) return;
        ApplySalesRows();
        NotifyChrome();
    }

    partial void OnHasChartChanged(bool value) => OnPropertyChanged(nameof(ShowChartEmpty));
    partial void OnChartEmptyMessageChanged(string value) => OnPropertyChanged(nameof(ShowChartEmpty));
    partial void OnChartCaptionChanged(string value) => OnPropertyChanged(nameof(HasChartCaption));

    [RelayCommand]
    private Task Retry() => RefreshAsync();

    private bool CanExportPdf() => CanExport;

    [RelayCommand(CanExecute = nameof(CanExportPdf))]
    private async Task ExportPdf()
    {
        if(!SessionManager.IsLoggedIn || !CanExport) return;
        var document=BuildDocument();
        var fileName=BuildFileName();
        try { await ReportPreview.ShowAsync(document,fileName); }
        catch(Exception ex) { DialogService.ShowError(ex.Message,"Report preview"); }
    }
    private void RequestRefresh()
    {
        if (_suppress) return;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var id = ++_requestId;
        var today = DateTime.Today;
        var view = SelectedView;
        ValidationMessage = view switch
        {
            ReportView.Daily when DailyFrom is null || DailyTo is null => "Choose a From date and a To date.",
            ReportView.Daily => SalesReportBuilder.ValidateDailyRange(DailyFrom!.Value, DailyTo!.Value, today),
            ReportView.Yearly => SalesReportBuilder.ValidateYearRange(YearFrom, YearTo),
            _ => null
        };

        if (ValidationMessage is not null)
        {
            ClearReport();
            IsLoading = false;
            ErrorMessage = null;
            NotifyChrome();
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        NotifyChrome();

        var dailyFrom = DailyFrom ?? today;
        var dailyTo = DailyTo ?? today;
        var weeklyYear = WeeklyYear;
        var monthlyYear = MonthlyYear;
        var yearFrom = YearFrom;
        var yearTo = YearTo;
        var otherYear = OtherYear;

        try
        {
            var loaded = await Task.Run(() => LoadReport(view, today, dailyFrom, dailyTo, weeklyYear, monthlyYear, yearFrom, yearTo, otherYear));
            if (id != _requestId) return;
            Apply(view, loaded);
        }
        catch (Exception ex)
        {
            if (id != _requestId) return;
            ErrorMessage = ex.Message;
            ClearReport();
        }
        finally
        {
            if (id == _requestId)
            {
                IsLoading = false;
                NotifyChrome();
            }
        }
    }

    private LoadedReport LoadReport(
        ReportView view,
        DateTime today,
        DateTime dailyFrom,
        DateTime dailyTo,
        int weeklyYear,
        int monthlyYear,
        int yearFrom,
        int yearTo,
        int otherYear)
    {
        if (view is ReportView.Today or ReportView.Daily or ReportView.Weekly or ReportView.Monthly or ReportView.Yearly)
        {
            var (from, to) = view switch
            {
                ReportView.Today => (today, today),
                ReportView.Daily => (dailyFrom.Date, dailyTo.Date),
                ReportView.Weekly => (new DateTime(weeklyYear, 1, 1), new DateTime(weeklyYear, 12, 31)),
                ReportView.Monthly => (new DateTime(monthlyYear, 1, 1), new DateTime(monthlyYear, 12, 31)),
                _ => (new DateTime(yearFrom, 1, 1), new DateTime(yearTo, 12, 31))
            };
            if(to>today) to=today;
            var snapshot = _salesReports.Capture(from,to,view is ReportView.Daily or ReportView.Today);
            var totals = snapshot.Totals;
            var recordsStart = snapshot.RecordsStart;
            var result = view switch
            {
                ReportView.Daily or ReportView.Today => SalesReportBuilder.BuildDaily(totals, from, to, recordsStart, today),
                ReportView.Weekly => SalesReportBuilder.BuildWeekly(totals, weeklyYear, recordsStart, today),
                ReportView.Monthly => SalesReportBuilder.BuildMonthly(totals, monthlyYear, recordsStart, today),
                _ => SalesReportBuilder.BuildYearly(totals, yearFrom, yearTo, recordsStart, today)
            };
            return new LoadedReport { Sales=result, Products=snapshot.Products, Details=snapshot.Details };
        }

        if(!SessionManager.IsAdmin && view is ReportView.StockIn or ReportView.StockOut) throw new InvalidOperationException("Admin access required.");
        return view switch
        {
            ReportView.StockIn => LoadedReport.FromStockIn(_inventory.GetStockInReport(otherYear), otherYear),
            ReportView.StockOut => LoadedReport.FromStockOut(_inventory.GetStockOutReport(otherYear), otherYear),
            _ => LoadedReport.FromTransactions(_salesRepo.GetHistory(year: otherYear), otherYear, _salesReports.GetDetails(new DateTime(otherYear,1,1),new DateTime(otherYear,12,31)))
        };
    }

    private void Apply(ReportView view, LoadedReport loaded)
    {
        _productSource=loaded.Products; SortProducts(); DailyDetails=new ObservableCollection<SalesDetailRow>(loaded.Details);
        ReportTitle = Title(view);
        if (loaded.Sales is not null)
        {
            _sales = loaded.Sales;
            _otherSource = [];
            OtherRows = new ObservableCollection<ReportRow>();
            PeriodDescription = loaded.Sales.PeriodDescription;
            TableTitle = TableTitleFor(view);
            PeriodColumnHeader = PeriodHeader(view);
            SummaryItems = new ObservableCollection<ReportSummaryItem>(SalesSummary(loaded.Sales, view));
            ApplySalesRows();
            BuildSalesChart(loaded.Sales, view);
            ApplyOtherColumns(view);
            return;
        }

        _sales = null;
        _productSource=[]; TopProducts=new(); DailyDetails=new();
        VisibleSalesRows = new ObservableCollection<SalesPeriodRow>();
        _otherSource = loaded.Rows;
        OtherRows = new ObservableCollection<ReportRow>(loaded.Rows);
        PeriodDescription = $"Year {loaded.Year} · {loaded.Rows.Count} {(loaded.Rows.Count == 1 ? "record" : "records")}";
        TableTitle = Title(view);
        SummaryItems = new ObservableCollection<ReportSummaryItem>(loaded.Summary);
        FooterText = loaded.Footer;
        ApplyOtherColumns(view);
        BuildOtherChart(loaded);
    }

    private void ApplySalesRows()
    {
        if (_sales is null) return;
        var all = _sales.Periods;
        var visible = HideZeroRows
            ? all.Where(row => row.Amount != 0m || row.RefundAmount != 0m).ToList()
            : all.ToList();
        VisibleSalesRows = new ObservableCollection<SalesPeriodRow>(visible);
        var noun = all.Count == 1 ? _sales.PeriodSingular : _sales.PeriodSingular + "s";
        var hidden = HideZeroRows ? " (zero-sales hidden)" : string.Empty;
        var totalLabel = _sales.IncludesToDate ? "Total to date" : "Total";
        FooterText = $"Showing {visible.Count} of {all.Count} {noun}{hidden} · {totalLabel} {SalesReportText.Money(_sales.TotalSales)}";
    }

    private void ApplyOtherColumns(ReportView view)
    {
        switch (view)
        {
            case ReportView.StockIn:
                Col1Header = "Date";
                Col2Header = "Product";
                Col3Header = "Supplier";
                Col4Header = "Quantity Received";
                ShowCol3 = ShowCol4 = true;
                break;
            case ReportView.StockOut:
                Col1Header = "Date";
                Col2Header = "Product";
                Col3Header = "Quantity";
                Col4Header = "Reason";
                ShowCol3 = ShowCol4 = true;
                break;
            case ReportView.Transactions:
                Col1Header = "Date";
                Col2Header = "Invoice No.";
                Col3Header = "Cashier";
                Col4Header = "Amount";
                ShowCol3 = ShowCol4 = true;
                break;
            default:
                ShowCol3 = ShowCol4 = false;
                break;
        }
    }

    private void BuildSalesChart(SalesReportResult result, ReportView view)
    {
        ChartTitle = "Sales";
        var confirmed = result.Periods.Any(period => period.Amount is not null);
        if (!result.Periods.Any(p=>p.Amount is not null && p.Amount!=0))
        {
            ClearChart();
            ChartEmptyMessage = confirmed
                ? $"Net sales are zero for {result.RangeText}. See sales and refunds below."
                : $"No records for {result.RangeText}.";
            return;
        }

        var fullLabels = result.Periods.Select(period => period.FullLabel).ToArray();
        var shortLabels = result.Periods.Select(period => period.ShortLabel).ToArray();
        ChartCaption = ChartNote(result, view);

        ISeries[] series;
        LegendPosition legend;
        if (view == ReportView.Daily && result.Periods.Count>1)
        {
            series = [Line(result.Periods.Select(period => period.Amount).ToArray(), fullLabels, "Net Sales", SalesColor)];
            legend = LegendPosition.Hidden;
        }
        else
        {
            var columns = new List<ISeries>
            {
                Columns(
                    result.Periods.Select(period => period.Status == SalesPeriodStatus.ToDate ? (decimal?)null : period.Amount).ToArray(),
                    fullLabels,
                    "Net Sales",
                    SalesColor,
                    money: true)
            };
            if (result.IncludesToDate)
            {
                columns.Add(Columns(
                    result.Periods.Select(period => period.Status == SalesPeriodStatus.ToDate ? period.Amount : null).ToArray(),
                    fullLabels,
                    "To date",
                    ToDateColor,
                    money: true));
            }
            series = columns.ToArray();
            legend = result.IncludesToDate ? LegendPosition.Bottom : LegendPosition.Hidden;
        }

        PublishChart(series, [CategoryAxis(shortLabels)], [MoneyAxis((double)Math.Min(0,result.Periods.Min(p=>p.Amount??0)))], legend);
    }

    private void BuildOtherChart(LoadedReport loaded)
    {
        ChartTitle = loaded.ChartTitle;
        ChartCaption = string.Empty;
        if (loaded.ChartLabels.Count == 0)
        {
            ClearChart();
            ChartEmptyMessage = loaded.ChartEmptyMessage;
            return;
        }

        var labels = loaded.ChartLabels.ToArray();
        var full = loaded.ChartFullLabels.ToArray();
        var values = loaded.ChartValues.Select(value => (decimal?)value).ToArray();
        PublishChart(
            [Columns(values, full, loaded.MoneyChart ? "Net Sales" : "Quantity", SalesColor, loaded.MoneyChart)],
            [CategoryAxis(labels)],
            [loaded.MoneyChart ? MoneyAxis((double)Math.Min(0,loaded.ChartValues.Min())) : QuantityAxis()],
            LegendPosition.Hidden);
    }

    private void PublishChart(ISeries[] series, Axis[] xAxes, Axis[] yAxes, LegendPosition legend)
    {
        ChartXAxes = xAxes;
        ChartYAxes = yAxes;
        ChartLegendPosition = legend;
        ChartEmptyMessage = string.Empty;
        ChartSeries = series;
        HasChart = true;
    }

    private static LineSeries<double?> Line(decimal?[] amounts, string[] labels, string name, string color) => new()
    {
        Name = name,
        Values = amounts.Select(amount => amount is null ? (double?)null : (double)amount.Value).ToArray(),
        GeometrySize = 0,
        LineSmoothness = 0,
        Fill = null,
        AnimationsSpeed = TimeSpan.Zero,
        Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2.5f },
        YToolTipLabelFormatter = point => SalesReportText.Money((decimal)point.Coordinate.PrimaryValue),
        XToolTipLabelFormatter = point => LabelAt(labels, point.Index)
    };

    private static ColumnSeries<double?> Columns(decimal?[] amounts, string[] labels, string name, string color, bool money) => new()
    {
        Name = name,
        Values = amounts.Select(amount => amount is null ? (double?)null : (double)amount.Value).ToArray(),
        Fill = new SolidColorPaint(SKColor.Parse(color)),
        MaxBarWidth = 28,
        IgnoresBarPosition = true,
        AnimationsSpeed = TimeSpan.Zero,
        YToolTipLabelFormatter = point => money
            ? SalesReportText.Money((decimal)point.Coordinate.PrimaryValue)
            : SalesReportText.Quantity((decimal)point.Coordinate.PrimaryValue),
        XToolTipLabelFormatter = point => LabelAt(labels, point.Index)
    };

    private static Axis CategoryAxis(string[] labels)
    {
        var step = Math.Max(1, (int)Math.Ceiling(labels.Length / 12d));
        var shown = new string[labels.Length];
        for (var i = 0; i < labels.Length; i++)
            shown[i] = i % step == 0 || i == labels.Length - 1 ? labels[i] : string.Empty;

        return new Axis
        {
            Labels = shown,
            LabelsRotation = labels.Length > 16 ? 30 : 0,
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
            SeparatorsPaint = null
        };
    }

    private static Axis MoneyAxis(double minimum = 0) => new()
    {
        MinLimit = minimum,
        Labeler = value => value.ToString("₱#,##0", SalesReportText.Culture),
        TextSize = 12,
        LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
        SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
    };

    private static Axis QuantityAxis() => new()
    {
        MinLimit = 0,
        Labeler = value => value.ToString("N0", SalesReportText.Culture),
        TextSize = 12,
        LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
        SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
    };

    private static string LabelAt(string[] labels, int index) =>
        index >= 0 && index < labels.Length ? labels[index] : string.Empty;

    private static string ChartNote(SalesReportResult result, ReportView view)
    {
        var parts = new List<string>();
        if (result.Periods.Any(period => period.Amount is null or 0m))
            parts.Add("A period with no bar is upcoming, before the first record, or had no sales. The table shows which.");
        if (result.IncludesToDate && view != ReportView.Daily)
            parts.Add("The lighter bar is the current period, to date.");
        else if (result.IncludesToDate)
            parts.Add("Today is included to date.");
        return string.Join(" ", parts);
    }

    private static IEnumerable<ReportSummaryItem> SalesSummary(SalesReportResult result, ReportView view)
    {
        yield return new ReportSummaryItem {Label="Sales before refunds",Value=SalesReportText.Money(result.SalesAmount)};
        yield return new ReportSummaryItem {Label="Refunds",Value=SalesReportText.Money(result.RefundAmount)};
        var bestName = view switch
        {
            ReportView.Weekly => "Best week",
            ReportView.Monthly => "Best month",
            ReportView.Yearly => "Best year",
            _ => "Best day"
        };
        yield return new ReportSummaryItem
        {
            Label = "Net sales",
            Value = SalesReportText.Money(result.TotalSales),
            Detail = result.IncludesToDate ? "To date" : string.Empty
        };
        yield return new ReportSummaryItem
        {
            Label = "Transactions",
            Value = result.TransactionCount.ToString("N0", SalesReportText.Culture)
        };
        yield return new ReportSummaryItem
        {
            Label = "Average sale before refunds",
            Value = result.AverageTransaction is decimal average ? SalesReportText.Money(average) : "—"
        };
        yield return new ReportSummaryItem
        {
            Label = bestName,
            Value = result.BestPeriod is null ? "—" : SalesReportText.Money(result.BestPeriod.Amount ?? 0),
            Detail = result.BestPeriod?.FullLabel ?? "No sales in this period"
        };
    }

    private ReportDocument BuildDocument()
    {
        if (_sales is not null) return ShowDailyDetails ? DailyDetailDocument(_sales) : SalesDocument(_sales);
        return SelectedView switch
        {
            ReportView.StockIn => OtherDocument("Stock in history", ["Date", "Product", "Supplier", "Quantity / unit"], [3], QuantityTotal()),
            ReportView.StockOut => OtherDocument("Stock out history", ["Date", "Product", "Quantity / unit", "Reason"], [2], QuantityTotal()),
            _ => TransactionDocument()
        };
    }

    private ReportSection ProductSection() => new()
    {
        Title = "Top products · " + (RankByQuantity ? "by net quantity" : "by net sales amount"),
        Headers = ["Product", "Unit", "Sold", "Returned", "Net qty", "Net sales"],
        RightAlignedColumns = [2,3,4,5],
        Rows = TopProducts.Select(p=>new[] {p.ProductName,p.Unit,SalesReportText.Quantity(p.Sold),SalesReportText.Quantity(p.Returned),SalesReportText.Quantity(p.NetQuantity),SalesReportText.Money(p.NetSales)}).ToList()
    };

    private List<string> SalesNotes() =>
    [
        "Excludes voided sales. Refunds are deducted on the date the return was processed.",
        "Sales amounts include original discounts and tax. Quantities retain their own units.",
        ProductRankingNote
    ];

    private ReportDocument DailyDetailDocument(SalesReportResult result) => new()
    {
        Title=IsToday ? "Today's sales — transaction and product detail" : "Daily sales — transaction and product detail",
        PeriodValue=result.RangeText,
        SummaryLines=SummaryItems.Select(i=>$"{i.Label}: {i.Value}").ToList(),
        Notes=SalesNotes(),
        Headers=["Invoice", "Product", "Qty / unit", "Type", "Amount"],
        RightAlignedColumns=[2,4],
        Rows=DailyDetails.Select(d=>new[] {d.Invoice,d.ProductName,$"{d.Quantity:0.###} {d.Unit}",d.Event,SalesReportText.Money(d.Amount)}).ToList(),
        TotalRow=["Net sales", "", "", "",SalesReportText.Money(result.TotalSales)],
        Sections=[ProductSection()]
    };

    private ReportDocument SalesDocument(SalesReportResult result)
    {
        var hidden = HideZeroRows ? result.Periods.Count(period => period.Amount == 0m && period.RefundAmount==0) : 0;
        var rows = (HideZeroRows ? result.Periods.Where(period => period.Amount != 0m || period.RefundAmount!=0) : result.Periods)
            .Select(period => new[] { period.PdfPeriod, period.SalesDisplay,period.RefundDisplay,period.AmountDisplay }).ToList();
        var notes=SalesNotes();
        if(SelectedView==ReportView.Weekly) notes.Add("Weeks run Monday–Sunday and are clipped to the selected calendar year.");
        if(hidden>0) notes.Add($"{hidden} zero-activity rows hidden. Totals cover the complete selected period.");
        var totalLabel=result.IncludesToDate?"Total to date":"Total";
        return new ReportDocument
        {
            Title=ReportTitle + (IsDaily?" — daily summary":""), PeriodValue=result.RangeText,
            SummaryLines=SummaryItems.Select(i=>$"{i.Label}: {i.Value} {i.Detail}".Trim()).ToList(),
            Notes=notes, Headers=[PeriodColumnHeader,"Sales before refunds","Refunds","Net sales"], RightAlignedColumns=[1,2,3],
            Rows=rows, TotalRow=[totalLabel,SalesReportText.Money(result.SalesAmount),SalesReportText.Money(result.RefundAmount),SalesReportText.Money(result.TotalSales)],
            Sections=[ProductSection()]
        };
    }
    private ReportDocument OtherDocument(string title, string[] headers, int[] rightAligned, string totalLine)
    {
        return new ReportDocument
        {
            Title = title,
            PeriodLabel = "Year",
            PeriodValue = OtherYear.ToString(),
            SummaryLines = SummaryItems.Select(item => string.IsNullOrEmpty(item.Detail) ? $"{item.Label}: {item.Value}" : $"{item.Label}: {item.Value}").ToList(),
            Notes = [],
            Headers = headers,
            RightAlignedColumns = rightAligned,
            Rows = _otherSource.Select(row => new[] { row.Col1, row.Col2, row.Col3, row.Col4 }).ToList(),
            TotalRow = SelectedView == ReportView.StockIn
                ? ["Total", string.Empty, string.Empty, totalLine]
                : ["Total", string.Empty, totalLine, string.Empty]
        };
    }

    private string QuantityTotal()
    {
        var item = SummaryItems.FirstOrDefault(summary => summary.Label.Contains("Quantity", StringComparison.OrdinalIgnoreCase));
        return item?.Value ?? SalesReportText.Quantity(0);
    }

    private ReportDocument TransactionDocument()
    {
        var due = SummaryItems.FirstOrDefault(item => item.Label == "Net sales")?.Value ?? SalesReportText.Money(0);
        return new ReportDocument
        {
            Title = "All sales transactions",
            PeriodLabel = "Year",
            PeriodValue = OtherYear.ToString(),
            SummaryLines = SummaryItems.Select(item => $"{item.Label}: {item.Value}").ToList(),
            Notes = ["Voided sales are listed and excluded. Refund rows reduce totals on their processing date."],
            Headers = ["Date", "Invoice No.", "Cashier", "Total Due"],
            RightAlignedColumns = [3],
            Rows = _otherSource.Select(row => new[] { row.Col1, row.Col2, row.Col3, row.Col4 }).ToList(),
            TotalRow = ["Total", string.Empty, string.Empty, due]
        };
    }

    private string BuildFileName()
    {
        return SelectedView switch
        {
            ReportView.Today => $"4KV_Todays_Sales_{_sales?.RangeStart:yyyy-MM-dd}.pdf",
            ReportView.Daily => $"4KV_Daily_Sales_{DailyFrom:yyyy-MM-dd}_to_{DailyTo:yyyy-MM-dd}.pdf",
            ReportView.Weekly => $"4KV_Weekly_Sales_{WeeklyYear}.pdf",
            ReportView.Monthly => $"4KV_Monthly_Sales_{MonthlyYear}.pdf",
            ReportView.Yearly => $"4KV_Yearly_Sales_{YearFrom}-{YearTo}.pdf",
            ReportView.StockIn => $"4KV_Stock_In_History_{OtherYear}.pdf",
            ReportView.StockOut => $"4KV_Stock_Out_History_{OtherYear}.pdf",
            _ => $"4KV_All_Sales_Transactions_{OtherYear}.pdf"
        };
    }

    private void ClearReport()
    {
        _sales = null;
        _productSource=[]; TopProducts=new(); DailyDetails=new();
        _otherSource = [];
        VisibleSalesRows = new ObservableCollection<SalesPeriodRow>();
        OtherRows = new ObservableCollection<ReportRow>();
        SummaryItems = new ObservableCollection<ReportSummaryItem>();
        PeriodDescription = string.Empty;
        FooterText = string.Empty;
        ReportTitle = Title(SelectedView);
        TableTitle = IsSalesView ? TableTitleFor(SelectedView) : Title(SelectedView);
        ClearChart();
        ChartEmptyMessage = string.Empty;
    }

    private void ClearChart()
    {
        ChartSeries = [];
        ChartXAxes = [];
        ChartYAxes = [];
        HasChart = false;
        ChartCaption = string.Empty;
        ChartLegendPosition = LegendPosition.Hidden;
    }

    private void NotifyViewFlags()
    {
        OnPropertyChanged(nameof(ShowChartSection));
        OnPropertyChanged(nameof(ShowDailyDetails)); OnPropertyChanged(nameof(ShowDailyTo));
        OnPropertyChanged(nameof(IsSalesView));
        OnPropertyChanged(nameof(IsDaily));
        OnPropertyChanged(nameof(IsToday));
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(IsYearly));
        OnPropertyChanged(nameof(IsOther));
    }

    private void NotifyChrome()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasValidation));
        OnPropertyChanged(nameof(ShowResults));
        OnPropertyChanged(nameof(ShowChartEmpty));
        OnPropertyChanged(nameof(CanExport));
        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    private static string Title(ReportView view) => view switch
    {
        ReportView.Today => "Today's sales",
        ReportView.Daily => "Daily sales",
        ReportView.Weekly => "Weekly sales",
        ReportView.Monthly => "Monthly sales",
        ReportView.Yearly => "Yearly sales",
        ReportView.StockIn => "Stock in history",
        ReportView.StockOut => "Stock out history",
        _ => "All sales transactions"
    };

    private static string TableTitleFor(ReportView view) => view switch
    {
        ReportView.Today => "Today's sales summary",
        ReportView.Weekly => "Weekly sales by week",
        ReportView.Monthly => "Monthly sales by month",
        ReportView.Yearly => "Yearly sales by year",
        _ => "Daily sales by date"
    };

    private static string PeriodHeader(ReportView view) => view switch
    {
        ReportView.Weekly => "Week",
        ReportView.Monthly => "Month",
        ReportView.Yearly => "Year",
        _ => "Date"
    };

    private static string BestName(ReportView view) => view switch
    {
        ReportView.Weekly => "Best week",
        ReportView.Monthly => "Best month",
        ReportView.Yearly => "Best year",
        _ => "Best day"
    };

    private static string FormatReason(string? reason) => reason switch
    {
        "Damaged" => "Damaged",
        "ReturnedToSupplier" => "Returned to supplier",
        "InternalUse" => "Internal use",
        "Expired" => "Expired",
        null or "" => "—",
        _ => reason
    };

    private sealed class LoadedReport
    {
        public List<ProductSalesRow> Products { get; init; } = [];
        public List<SalesDetailRow> Details { get; init; } = [];
        public SalesReportResult? Sales { get; init; }
        public List<ReportRow> Rows { get; init; } = [];
        public List<ReportSummaryItem> Summary { get; init; } = [];
        public string Footer { get; init; } = string.Empty;
        public int Year { get; init; }
        public string ChartTitle { get; init; } = string.Empty;
        public string ChartEmptyMessage { get; init; } = string.Empty;
        public bool MoneyChart { get; init; }
        public List<string> ChartLabels { get; init; } = [];
        public List<string> ChartFullLabels { get; init; } = [];
        public List<decimal> ChartValues { get; init; } = [];

        public static LoadedReport FromSales(SalesReportResult sales) => new() { Sales = sales };

        public static LoadedReport FromStockIn(List<StockInReportRow> rows, int year)
        {
            var quantity = string.Join("; ",rows.GroupBy(r=>r.Unit).Select(g=>$"{SalesReportText.Quantity(g.Sum(r=>r.Quantity))} {g.Key}"));
            var products = rows.Select(row => row.ProductName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var grouped = rows
                .GroupBy(row => row.ProductName)
                .Select(group => (group.Key, group.Sum(row => row.Quantity)))
                .OrderByDescending(group => group.Item2)
                .Take(12)
                .ToList();
            return new LoadedReport
            {
                Year = year,
                Rows = rows.Select(row => new ReportRow
                {
                    Col1 = row.MovementDate.ToString("MMM d, yyyy", SalesReportText.Culture),
                    Col2 = row.ProductName,
                    Col3 = string.IsNullOrWhiteSpace(row.SupplierName) ? "—" : row.SupplierName,
                    Col4 = $"{SalesReportText.Quantity(row.Quantity)} {row.Unit}"
                }).ToList(),
                Summary =
                [
                    new ReportSummaryItem { Label = "Entries", Value = rows.Count.ToString("N0", SalesReportText.Culture) },
                    new ReportSummaryItem { Label = "Quantity received", Value = quantity },
                    new ReportSummaryItem { Label = "Products", Value = products.ToString("N0", SalesReportText.Culture) }
                ],
                Footer = $"Showing {rows.Count} {(rows.Count == 1 ? "record" : "records")} · Total quantity {quantity}",
                ChartTitle = "Stock in by product",
                ChartEmptyMessage = $"No stock in records for {year}.",
                ChartLabels = grouped.Select(group => group.Key).ToList(),
                ChartFullLabels = grouped.Select(group => group.Key).ToList(),
                ChartValues = grouped.Select(group => group.Item2).ToList()
            };
        }

        public static LoadedReport FromStockOut(List<StockOutReportRow> rows, int year)
        {
            var quantity = string.Join("; ",rows.GroupBy(r=>r.Unit).Select(g=>$"{SalesReportText.Quantity(g.Sum(r=>r.Quantity))} {g.Key}"));
            var grouped = rows
                .GroupBy(row => row.ProductName)
                .Select(group => (group.Key, group.Sum(row => row.Quantity)))
                .OrderByDescending(group => group.Item2)
                .Take(12)
                .ToList();
            return new LoadedReport
            {
                Year = year,
                Rows = rows.Select(row => new ReportRow
                {
                    Col1 = row.MovementDate.ToString("MMM d, yyyy", SalesReportText.Culture),
                    Col2 = row.ProductName,
                    Col3 = $"{SalesReportText.Quantity(row.Quantity)} {row.Unit}",
                    Col4 = FormatReason(row.Reason)
                }).ToList(),
                Summary =
                [
                    new ReportSummaryItem { Label = "Entries", Value = rows.Count.ToString("N0", SalesReportText.Culture) },
                    new ReportSummaryItem { Label = "Quantity out", Value = quantity }
                ],
                Footer = $"Showing {rows.Count} {(rows.Count == 1 ? "record" : "records")} · Total quantity {quantity}",
                ChartTitle = "Stock out by product",
                ChartEmptyMessage = $"No stock out records for {year}.",
                ChartLabels = grouped.Select(group => group.Key).ToList(),
                ChartFullLabels = grouped.Select(group => group.Key).ToList(),
                ChartValues = grouped.Select(group => group.Item2).ToList()
            };
        }

        public static LoadedReport FromTransactions(List<SaleHistoryRow> rows, int year, List<SalesDetailRow> details)
        {
            var completed = rows.Where(row => !row.IsVoided).ToList();
            var refundRows=details.Where(d=>d.Event=="Refund").GroupBy(d=>new {d.Invoice,d.EventDate}).Select(g=>new ReportRow {Col1=g.Key.EventDate.ToString("MMM d, yyyy h:mm tt"),Col2=g.Key.Invoice+" (REFUND)",Col3="Customer return",Col4=SalesReportText.Money(g.Sum(d=>d.Amount))}).ToList();
            var refunds=-details.Where(d=>d.Event=="Refund").Sum(d=>d.Amount);
            var due = completed.Sum(row => row.TotalDue)-refunds;
            var voided = rows.Count - completed.Count;
            var grouped = details
                .GroupBy(row => row.EventDate.Date)
                .OrderBy(group => group.Key)
                .Select(group => (group.Key, group.Sum(row => row.Amount)))
                .ToList();
            return new LoadedReport
            {
                Year = year,
                MoneyChart = true,
                Rows = rows.Select(row => new ReportRow
                {
                    Col1 = row.SaleDate.ToString("MMM d, yyyy h:mm tt", SalesReportText.Culture),
                    Col2 = row.IsVoided ? $"{row.InvoiceNo} (VOIDED)" : row.InvoiceNo,
                    Col3 = row.CashierName,
                    Col4 = SalesReportText.Money(row.TotalDue),
                    ExcludeFromTotal = row.IsVoided
                }).Concat(refundRows).ToList(),
                Summary =
                [
                    new ReportSummaryItem { Label = "Completed sales", Value = completed.Count.ToString("N0", SalesReportText.Culture) },
                    new ReportSummaryItem {Label="Refunds in period",Value=SalesReportText.Money(refunds)},
                    new ReportSummaryItem { Label = "Net sales", Value = SalesReportText.Money(due) },
                    new ReportSummaryItem { Label = "Voided sales", Value = voided.ToString("N0", SalesReportText.Culture) }
                ],
                Footer = $"Showing {rows.Count+refundRows.Count} records · Net sales {SalesReportText.Money(due)} · {voided} voided excluded",
                ChartTitle = "Net sales by day",
                ChartEmptyMessage = $"No completed sales for {year}.",
                ChartLabels = grouped.Select(group => group.Key.ToString("MMM d", SalesReportText.Culture)).ToList(),
                ChartFullLabels = grouped.Select(group => group.Key.ToString("MMM d, yyyy", SalesReportText.Culture)).ToList(),
                ChartValues = grouped.Select(group => group.Item2).ToList()
            };
        }
    }
}
