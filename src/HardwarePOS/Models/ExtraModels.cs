namespace HardwarePOS.Models;

public class UnitOfMeasureItem
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class SelectableItem
{
    public bool IsSelected { get; set; }
}

public class ActivityItem
{
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TopProductRow
{
    public int Rank { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = "Piece";
    public string? ImagePath { get; set; }
    public decimal QtySold { get; set; }
    public decimal Revenue { get; set; }
    public string QtyLabel => $"{QtySold:N0} {UnitOfMeasure}";
}

public class RecentSaleRow
{
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public decimal TotalDue { get; set; }
}

public class CategorySalesRow
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class SaleHistoryRow
{
    public int SaleId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalDue { get; set; }
    public decimal CashTendered { get; set; }
    public decimal ChangeAmount { get; set; }
}

public class SaleHistoryItemRow
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
