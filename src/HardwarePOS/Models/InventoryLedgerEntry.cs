namespace HardwarePOS.Models;

public class InventoryLedgerEntry
{
    public long LedgerId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal QtyChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public int? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
