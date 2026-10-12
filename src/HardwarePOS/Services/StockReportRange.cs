using HardwarePOS.Models;

namespace HardwarePOS.Services;

public static class StockReportRange
{
    public static StockReportWindow Resolve(StockReportPeriod period, DateTime anchor) => period switch
    {
        StockReportPeriod.Daily => Day(anchor),
        StockReportPeriod.Weekly => Week(anchor),
        StockReportPeriod.Monthly => Month(anchor.Year, anchor.Month),
        StockReportPeriod.Yearly => Year(anchor.Year),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    public static StockReportWindow Day(DateTime day)
    {
        var start = day.Date;
        return new StockReportWindow(start, start.AddDays(1), start.ToString("MMMM d, yyyy", SalesReportText.Culture));
    }

    public static StockReportWindow Week(DateTime day)
    {
        var date = day.Date;
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = date.AddDays(-daysFromMonday);
        var end = start.AddDays(7);
        var last = end.AddDays(-1);
        var culture = SalesReportText.Culture;
        var label = start.Year == last.Year && start.Month == last.Month
            ? $"Week of {start.ToString("MMMM d", culture)}\u2013{last.Day}, {start.Year}"
            : start.Year == last.Year
                ? $"Week of {start.ToString("MMMM d", culture)}\u2013{last.ToString("MMMM d", culture)}, {start.Year}"
                : $"Week of {start.ToString("MMMM d, yyyy", culture)}\u2013{last.ToString("MMMM d, yyyy", culture)}";
        return new StockReportWindow(start, end, label);
    }

    public static StockReportWindow Month(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        return new StockReportWindow(start, start.AddMonths(1), start.ToString("MMMM yyyy", SalesReportText.Culture));
    }

    public static StockReportWindow Year(int year)
    {
        var start = new DateTime(year, 1, 1);
        return new StockReportWindow(start, start.AddYears(1), $"Year {year}");
    }
}
