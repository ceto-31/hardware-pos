namespace HardwarePOS.Models;

public class DashboardSummary
{
    public decimal TodaySales { get; set; }
    public int TodayTransactions { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalProducts { get; set; }
    public int TotalSuppliers { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal YesterdaySales { get; set; }
    public int YesterdayTransactions { get; set; }
    public decimal TodayItemsSold { get; set; }
    public decimal YesterdayItemsSold { get; set; }
    public decimal TodayGrossProfit { get; set; }
    public decimal YesterdayGrossProfit { get; set; }
}

public class SalesPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
