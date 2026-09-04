namespace HardwarePOS.Models;

public class CartItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string UnitOfMeasure { get; set; } = "Piece";
    public decimal RegularUnitPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal AvailableStock { get; set; }
    public decimal RegularLineTotal => Math.Round(RegularUnitPrice * Quantity, 2);
    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2);
    public decimal LineDiscount => Math.Round(Math.Max(0, RegularUnitPrice - UnitPrice) * Quantity, 2);
}
