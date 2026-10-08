using System.Globalization;
using HardwarePOS.Models;

namespace HardwarePOS.Services;

public static class SalesReportBuilder
{
    public const int MaxDailySpanDays = 366;

    private static readonly CultureInfo Culture = SalesReportText.Culture;

    public static string? ValidateDailyRange(DateTime from, DateTime to, DateTime today)
    {
        from = from.Date;
        to = to.Date;
        today = today.Date;
        if (from > to)
            return "From must be on or before To.";
        if (to > today)
            return "To cannot be after today.";
        var days = (to - from).Days + 1;
        if (days > MaxDailySpanDays)
            return $"Daily reports cover at most {MaxDailySpanDays} days.";
        return null;
    }

    public static string? ValidateYearRange(int fromYear, int toYear)
    {
        if (fromYear > toYear)
            return "From year must be on or before To year.";
        return null;
    }

    public static SalesReportResult BuildDaily(
        IReadOnlyList<DailyTotal> totals,
        DateTime from,
        DateTime to,
        DateTime? recordsStart,
        DateTime today)
    {
        from = from.Date;
        to = to.Date;
        var periods = new List<SalesPeriodRow>();
        var byDate = Index(totals);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            periods.Add(Make(
                day,
                day,
                day.ToString("MMM d", Culture),
                day.ToString("MMM d, yyyy", Culture),
                clippedWeek: false,
                byDate,
                recordsStart,
                today));
        }

        return Finish(periods, from, to, "day", Describe(from, to, periods.Count, "day"));
    }

    public static SalesReportResult BuildWeekly(
        IReadOnlyList<DailyTotal> totals,
        int year,
        DateTime? recordsStart,
        DateTime today)
    {
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);
        var byDate = Index(totals);
        var periods = new List<SalesPeriodRow>();
        for (var weekStart = MondayOf(yearStart); weekStart <= yearEnd; weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(6);
            var clampedStart = weekStart < yearStart ? yearStart : weekStart;
            var clampedEnd = weekEnd > yearEnd ? yearEnd : weekEnd;
            periods.Add(Make(
                clampedStart,
                clampedEnd,
                clampedStart.ToString("MMM d", Culture),
                FormatSpan(clampedStart, clampedEnd),
                clippedWeek: clampedStart != weekStart || clampedEnd != weekEnd,
                byDate,
                recordsStart,
                today));
        }

        var description = $"{FormatSpan(yearStart, yearEnd)} · {CountPhrase(periods.Count, "week")} · Monday to Sunday · Excludes voided sales";
        return Finish(periods, yearStart, yearEnd, "week", description);
    }

    public static SalesReportResult BuildMonthly(
        IReadOnlyList<DailyTotal> totals,
        int year,
        DateTime? recordsStart,
        DateTime today)
    {
        var byDate = Index(totals);
        var periods = new List<SalesPeriodRow>();
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            periods.Add(Make(
                start,
                end,
                start.ToString("MMM", Culture),
                start.ToString("MMMM yyyy", Culture),
                clippedWeek: false,
                byDate,
                recordsStart,
                today));
        }

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);
        return Finish(periods, yearStart, yearEnd, "month", Describe(yearStart, yearEnd, periods.Count, "month"));
    }

    public static SalesReportResult BuildYearly(
        IReadOnlyList<DailyTotal> totals,
        int fromYear,
        int toYear,
        DateTime? recordsStart,
        DateTime today)
    {
        var byDate = Index(totals);
        var periods = new List<SalesPeriodRow>();
        for (var year = fromYear; year <= toYear; year++)
        {
            var start = new DateTime(year, 1, 1);
            var end = new DateTime(year, 12, 31);
            var label = year.ToString(Culture);
            periods.Add(Make(
                start,
                end,
                label,
                label,
                clippedWeek: false,
                byDate,
                recordsStart,
                today));
        }

        var rangeStart = new DateTime(fromYear, 1, 1);
        var rangeEnd = new DateTime(toYear, 12, 31);
        return Finish(periods, rangeStart, rangeEnd, "year", Describe(rangeStart, rangeEnd, periods.Count, "year"));
    }

    private static SalesReportResult Finish(
        List<SalesPeriodRow> periods,
        DateTime rangeStart,
        DateTime rangeEnd,
        string singular,
        string description)
    {
        var total = periods.Sum(p => p.Amount ?? 0m);
        var count = periods.Sum(p => p.TransactionCount);
        return new SalesReportResult
        {
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            RangeText = FormatSpan(rangeStart, rangeEnd),
            PeriodDescription = description,
            PeriodSingular = singular,
            Periods = periods,
            TotalSales = total,
            TransactionCount = count,
            AverageTransaction = count > 0 ? Math.Round((total + periods.Sum(p=>p.RefundAmount)) / count, 2) : null,
            BestPeriod = periods
                .Where(p => p.Amount > 0)
                .OrderByDescending(p => p.Amount)
                .ThenBy(p => p.Start)
                .FirstOrDefault(),
            IncludesToDate = periods.Any(p => p.Status == SalesPeriodStatus.ToDate)
        };
    }

    private static SalesPeriodRow Make(
        DateTime start,
        DateTime end,
        string shortLabel,
        string fullLabel,
        bool clippedWeek,
        IReadOnlyDictionary<DateTime, DailyTotal> byDate,
        DateTime? recordsStart,
        DateTime today)
    {
        today = today.Date;
        start = start.Date;
        end = end.Date;
        recordsStart = recordsStart?.Date;

        if (start > today)
            return Row(start, end, shortLabel, fullLabel, null, 0, SalesPeriodStatus.Upcoming);

        if (recordsStart is null || end < recordsStart.Value)
            return Row(start, end, shortLabel, fullLabel, null, 0, SalesPeriodStatus.NoRecords);

        decimal amount = 0, refunds = 0;
        var transactions = 0;
        var last = end < today ? end : today;
        for (var day = start; day <= last; day = day.AddDays(1))
        {
            if (day < recordsStart.Value)
                continue;
            if (!byDate.TryGetValue(day, out var total))
                continue;
            amount += total.Amount; refunds += total.RefundAmount;
            transactions += total.TransactionCount;
        }

        var status = start <= today && end >= today
            ? SalesPeriodStatus.ToDate
            : clippedWeek
                ? SalesPeriodStatus.Partial
                : SalesPeriodStatus.Complete;

        return Row(start, end, shortLabel, fullLabel, amount, transactions, status, refunds);
    }

    private static SalesPeriodRow Row(
        DateTime start,
        DateTime end,
        string shortLabel,
        string fullLabel,
        decimal? amount,
        int transactions,
        SalesPeriodStatus status, decimal refunds = 0) => new()
    {
        Start = start,
        End = end,
        ShortLabel = shortLabel,
        FullLabel = fullLabel,
        Amount = amount,
        TransactionCount = transactions,
        Status = status, RefundAmount = refunds
    };

    private static Dictionary<DateTime, DailyTotal> Index(IReadOnlyList<DailyTotal> totals)
    {
        var map = new Dictionary<DateTime, DailyTotal>();
        foreach (var total in totals)
        {
            var date = total.Date.Date;
            if (map.TryGetValue(date, out var existing))
            {
                map[date] = new DailyTotal
                {
                    Date = date,
                    Amount = existing.Amount + total.Amount, RefundAmount = existing.RefundAmount + total.RefundAmount,
                    TransactionCount = existing.TransactionCount + total.TransactionCount
                };
            }
            else
            {
                map[date] = total;
            }
        }
        return map;
    }

    public static DateTime MondayOf(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-offset);
    }

    public static string FormatSpan(DateTime start, DateTime end)
    {
        start = start.Date;
        end = end.Date;
        if (start == end)
            return start.ToString("MMM d, yyyy", Culture);
        if (start.Year == end.Year && start.Month == end.Month)
            return $"{start.ToString("MMM d", Culture)}–{end.ToString("d, yyyy", Culture)}";
        if (start.Year == end.Year)
            return $"{start.ToString("MMM d", Culture)} – {end.ToString("MMM d, yyyy", Culture)}";
        return $"{start.ToString("MMM d, yyyy", Culture)} – {end.ToString("MMM d, yyyy", Culture)}";
    }

    private static string Describe(DateTime start, DateTime end, int count, string singular) =>
        $"{FormatSpan(start, end)} · {CountPhrase(count, singular)} · Excludes voided sales";

    private static string CountPhrase(int count, string singular) =>
        $"{count} {(count == 1 ? singular : singular + "s")}";
}
