namespace HardwarePOS.Models;

public enum ReportView
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
    StockIn,
    StockOut,
    Transactions,
    Today
}

public enum StockReportPeriod
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public readonly record struct StockReportWindow(DateTime Start, DateTime End, string Label)
{
    public bool Contains(DateTime localTimestamp) => localTimestamp >= Start && localTimestamp < End;

    public bool ContainsUtc(DateTime utcTimestamp, TimeZoneInfo zone)
    {
        var utc = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        return Contains(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
    }
}

public enum SalesPeriodStatus
{
    Complete,
    ToDate,
    Partial,
    Upcoming,
    NoRecords
}

public class DailyTotal
{
    public decimal RefundAmount { get; init; }
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
    public int TransactionCount { get; init; }
}

public class SalesPeriodRow
{
    public decimal RefundAmount { get; init; }
    public decimal? SalesAmount => Amount + RefundAmount;
    public string SalesDisplay => SalesAmount is decimal amount ? SalesReportText.Money(amount) : "—";
    public string RefundDisplay => Amount is null ? "—" : SalesReportText.Money(RefundAmount);
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string ShortLabel { get; init; } = string.Empty;
    public string FullLabel { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public int TransactionCount { get; init; }
    public SalesPeriodStatus Status { get; init; }

    public bool HasStatus => Status != SalesPeriodStatus.Complete;

    public string StatusLabel => Status switch
    {
        SalesPeriodStatus.ToDate => "To date",
        SalesPeriodStatus.Partial => "Partial week",
        SalesPeriodStatus.Upcoming => "Upcoming",
        SalesPeriodStatus.NoRecords => "No records",
        _ => string.Empty
    };

    public string AmountDisplay =>
        Amount is null ? "—" : Amount.Value.ToString("₱#,##0.00", SalesReportText.Culture);

    public string PdfPeriod =>
        HasStatus ? $"{FullLabel} ({StatusLabel})" : FullLabel;
}

public class SalesReportResult
{
    public decimal RefundAmount => Periods.Sum(p=>p.RefundAmount);
    public decimal SalesAmount => TotalSales + RefundAmount;
    public DateTime RangeStart { get; init; }
    public DateTime RangeEnd { get; init; }
    public string RangeText { get; init; } = string.Empty;
    public string PeriodDescription { get; init; } = string.Empty;
    public string PeriodSingular { get; init; } = "day";
    public IReadOnlyList<SalesPeriodRow> Periods { get; init; } = [];
    public decimal TotalSales { get; init; }
    public int TransactionCount { get; init; }
    public decimal? AverageTransaction { get; init; }
    public SalesPeriodRow? BestPeriod { get; init; }
    public bool IncludesToDate { get; init; }
}

public class ReportSummaryItem
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

public class MonthOption
{
    public int Month { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

public class ReportDocument
{
    public IReadOnlyList<(string Label, string Value)> HeaderFields { get; init; } = [];
    public IReadOnlyList<ReportSection> Sections { get; init; } = [];
    public string Title { get; init; } = string.Empty;
    public string PeriodLabel { get; init; } = "Period";
    public string PeriodValue { get; init; } = string.Empty;
    public bool FlexibleColumns { get; init; }
    public IReadOnlyList<string> SummaryLines { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<int> RightAlignedColumns { get; init; } = [];
    public IReadOnlyList<string[]> Rows { get; init; } = [];
    public string[]? TotalRow { get; init; }
}

public class ReportSection
{
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<string[]> Rows { get; init; } = [];
    public IReadOnlyList<int> RightAlignedColumns { get; init; } = [];
}

public class ProductSalesRow
{
    public string? ImagePath { get; set; }
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Sold { get; set; }
    public decimal Returned { get; set; }
    public decimal NetQuantity => Sold - Returned;
    public decimal NetSales { get; set; }
}

public class SalesDetailRow
{
    public string Invoice { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Event { get; set; } = "Sale";
    public DateTime EventDate { get; set; }
}

public static class SalesReportText
{
    public static readonly System.Globalization.CultureInfo Culture =
        System.Globalization.CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal amount) => amount.ToString("₱#,##0.00", Culture);

    public static string Quantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity)
            ? quantity.ToString("N0", Culture)
            : quantity.ToString("0.###", Culture);
}
