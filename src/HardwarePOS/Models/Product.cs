namespace HardwarePOS.Models;

public class Product
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDetails { get; set; }
    public string? Barcode { get; set; }
    public int? UnitId { get; set; }
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
    public bool IsSelected { get; set; }

    public string StockStatus =>
        StockQty <= 0 ? "OutOfStock" :
        StockQty <= ReorderLevel ? "LowStock" : "OK";

    public string StatusLabel => IsArchived ? "Archived" : StockStatus switch
    {
        "OutOfStock" => "Out of Stock",
        "LowStock" => "Low Stock",
        _ => "Active"
    };
}

public class Supplier
{
    public int SupplierId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public bool IsSelected { get; set; }
    public string StatusLabel => IsArchived ? "Archived" : (IsActive ? "Active" : "Inactive");
}
