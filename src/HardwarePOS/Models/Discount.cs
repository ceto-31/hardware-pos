namespace HardwarePOS.Models;

public class Discount
{
    public int DiscountId { get; set; }
    public string DiscountName { get; set; } = string.Empty;
    public string ApplyScope { get; set; } = "Store";
    public string DiscountType { get; set; } = "PercentOff";
    public decimal DiscountValue { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProductCount { get; set; }
    public List<int> ProductIds { get; set; } = new();

    public string StatusLabel
    {
        get
        {
            if (IsArchived) return "Archived";
            var today = DateTime.Today;
            if (EndDate.Date < today) return "Expired";
            if (StartDate.Date > today) return "Scheduled";
            return "Active";
        }
    }

    public string ScopeDisplay => ApplyScope switch
    {
        "Store" => "Store-wide",
        "Category" => CategoryName ?? "Category",
        "Product" => ProductCount == 1 ? "1 product" : $"{ProductCount} products",
        _ => ApplyScope
    };

    public string ValueDisplay => DiscountType switch
    {
        "PercentOff" => $"{DiscountValue:N0}% off",
        "SalePrice" => $"₱{DiscountValue:N2} sale",
        "FixedAmount" => $"₱{DiscountValue:N2} off",
        _ => DiscountValue.ToString("N2")
    };

    public string ScheduleDisplay =>
        $"{StartDate:MMM dd, yyyy} – {EndDate:MMM dd, yyyy}";

    public string SummaryDisplay => $"{ScopeDisplay} · {ValueDisplay}";
}

public class ProductPickerRow : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayLabel => string.IsNullOrWhiteSpace(ProductCode)
        ? ProductName
        : $"{ProductCode} — {ProductName}";
}
