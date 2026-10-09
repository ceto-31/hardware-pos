namespace HardwarePOS.Models;

public class InventoryBatch
{
    public int BatchId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "Supplier/delivery unknown";
    public DateTime ReceivedAt { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public string Unit { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public string Display => $"#{BatchId} · {ReceivedAt:MMM d, yyyy} · {SupplierName} · {RemainingQuantity:0.###} {Unit} left";
}

public class ReturnableItem
{
    public int SaleItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Returned { get; set; }
    public decimal Remaining => Quantity - Returned;
    public decimal Paid { get; set; }
    public decimal Refunded { get; set; }
    public string Display => $"{ProductName} · {Remaining:0.###} {Unit} returnable";
}

public class BadStockRow
{
    public int BadStockId { get; set; }
    public string ProductName { get; set; } = "";
    public int? BatchId { get; set; }
    public string SupplierName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public string Unit { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal SaleableQty { get; set; }
    public decimal ProductBoQty { get; set; }
    public decimal TotalPhysicalQty => SaleableQty + ProductBoQty;
}

public class BadStockDisposition
{
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = "";
    public string ActionLabel => Action == "ReturnedToSupplier" ? "Returned to supplier" : "Disposed";
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string CreatedByName { get; set; } = "";
}

public class StockOutReasonOption
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}
