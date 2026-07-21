namespace HardwarePOS.Models;

public class Product
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDetails { get; set; }
    public string? Barcode { get; set; }
    public string UnitOfMeasure { get; set; } = "Piece";
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal StockQty { get; set; }
    public decimal ReorderLevel { get; set; } = 10;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public bool IsArchived { get; set; }

    public string StockStatus =>
        StockQty <= 0 ? "OutOfStock" :
        StockQty <= ReorderLevel ? "LowStock" : "OK";
}
