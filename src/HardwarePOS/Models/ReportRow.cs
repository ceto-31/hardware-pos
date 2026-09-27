namespace HardwarePOS.Models;

public class ReportRow
{
    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
    public bool ExcludeFromTotal { get; set; }
}

public class StockInReportRow
{
    public DateTime MovementDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal Quantity { get; set; }
}

public class StockOutReportRow
{
    public DateTime MovementDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
}
