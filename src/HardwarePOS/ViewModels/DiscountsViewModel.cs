using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class DiscountsViewModel : ObservableObject
{
    private readonly DiscountRepository _discounts = new();
    private readonly CategoryRepository _categories = new();
    private readonly ProductRepository _products = new();
    private readonly ActivityRepository _activity = new();

    public ObservableCollection<string> ScopeOptions { get; } = new() { "Store", "Category", "Product" };

    [ObservableProperty] private ObservableCollection<Discount> _items = new();
    [ObservableProperty] private ObservableCollection<Category> _categoryOptions = new();
    [ObservableProperty] private ObservableCollection<ProductPickerRow> _productOptions = new();
    [ObservableProperty] private ObservableCollection<string> _discountTypeOptions = new() { "PercentOff", "FixedAmount" };
    [ObservableProperty] private Discount? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showArchived;
    [ObservableProperty] private bool _hasRows;
    [ObservableProperty] private bool _isFormOpen;
    [ObservableProperty] private string _formTitle = "Add Discount";

    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _discountName = string.Empty;
    [ObservableProperty] private string _applyScope = "Store";
    [ObservableProperty] private string _discountType = "PercentOff";
    [ObservableProperty] private decimal _discountValue;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private DateTime? _startDate = DateTime.Today;
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;

    public bool IsCategoryScope => ApplyScope == "Category";
    public bool IsProductScope => ApplyScope == "Product";
    public bool IsStoreScope => ApplyScope == "Store";
    public bool IsPercentType => DiscountType == "PercentOff";
    public string ValueLabel => DiscountType switch
    {
        "PercentOff" => "Discount Percent (%)",
        "SalePrice" => "Sale Price (₱)",
        "FixedAmount" => "Fixed Amount Off (₱)",
        _ => "Value"
    };

    partial void OnSearchTextChanged(string value) => Load();
    partial void OnShowArchivedChanged(bool value) => Load();

    partial void OnApplyScopeChanged(string value)
    {
        UpdateDiscountTypeOptions();
        OnPropertyChanged(nameof(IsCategoryScope));
        OnPropertyChanged(nameof(IsProductScope));
        OnPropertyChanged(nameof(IsStoreScope));
    }

    partial void OnDiscountTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsPercentType));
        OnPropertyChanged(nameof(ValueLabel));
    }

    [RelayCommand]
    public void Load()
    {
        Items = new ObservableCollection<Discount>(_discounts.GetAll(SearchText, ShowArchived));
        HasRows = Items.Count > 0;
        CategoryOptions = new ObservableCollection<Category>(_categories.GetAll());
        ProductOptions = new ObservableCollection<ProductPickerRow>(
            _products.GetAll(includeArchived: false)
                .OrderBy(p => p.ProductName)
                .Select(p => new ProductPickerRow
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    ProductCode = p.ProductCode
                }));
    }

    [RelayCommand]
    private void New()
    {
        ClearForm();
        FormTitle = "Add Discount";
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        ClearForm();
        IsFormOpen = false;
    }

    [RelayCommand]
    private void Edit(Discount? discount)
    {
        var item = discount ?? SelectedItem;
        if (item is null) return;

        var full = _discounts.GetById(item.DiscountId) ?? item;
        SelectedItem = full;
        EditingId = full.DiscountId;
        DiscountName = full.DiscountName;
        ApplyScope = full.ApplyScope;
        DiscountType = full.DiscountType;
        DiscountValue = full.DiscountValue;
        CategoryId = full.CategoryId;
        StartDate = full.StartDate;
        EndDate = full.EndDate;
        UpdateDiscountTypeOptions();

        foreach (var row in ProductOptions)
            row.IsSelected = full.ProductIds.Contains(row.ProductId);

        FormTitle = "Edit Discount";
        IsFormOpen = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(DiscountName))
        {
            DialogService.ShowWarning("Discount name is required.", "Discounts");
            return;
        }

        if (!StartDate.HasValue || !EndDate.HasValue)
        {
            DialogService.ShowWarning("Start and end dates are required.", "Discounts");
            return;
        }

        if (EndDate.Value.Date < StartDate.Value.Date)
        {
            DialogService.ShowWarning("End date must be on or after start date.", "Discounts");
            return;
        }

        if (DiscountValue <= 0)
        {
            DialogService.ShowWarning("Discount value must be greater than zero.", "Discounts");
            return;
        }

        if (DiscountType == "PercentOff" && DiscountValue > 100)
        {
            DialogService.ShowWarning("Percent discount cannot exceed 100%.", "Discounts");
            return;
        }

        if (ApplyScope == "Category" && !CategoryId.HasValue)
        {
            DialogService.ShowWarning("Select a category for this discount.", "Discounts");
            return;
        }

        var selectedProductIds = ProductOptions.Where(p => p.IsSelected).Select(p => p.ProductId).ToList();
        if (ApplyScope == "Product" && selectedProductIds.Count == 0)
        {
            DialogService.ShowWarning("Select at least one product for this discount.", "Discounts");
            return;
        }

        var discount = new Discount
        {
            DiscountId = EditingId,
            DiscountName = DiscountName.Trim(),
            ApplyScope = ApplyScope,
            DiscountType = DiscountType,
            DiscountValue = DiscountValue,
            CategoryId = ApplyScope == "Category" ? CategoryId : null,
            StartDate = StartDate.Value.Date,
            EndDate = EndDate.Value.Date
        };

        try
        {
            IEnumerable<int> productIds = ApplyScope == "Product" ? selectedProductIds : [];
            if (EditingId == 0)
            {
                _discounts.Insert(discount, productIds);
                _activity.Log("Discount", $"Added discount '{discount.DiscountName}'");
            }
            else
            {
                _discounts.Update(discount, productIds);
                _activity.Log("Discount", $"Updated discount '{discount.DiscountName}'");
            }

            CloseForm();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Discounts");
        }
    }

    [RelayCommand]
    private void Archive(Discount? discount)
    {
        var item = discount ?? SelectedItem;
        if (item is null || item.IsArchived) return;
        if (!DialogService.Confirm($"Archive '{item.DiscountName}'?", "Discounts")) return;
        _discounts.Archive(item.DiscountId, true);
        _activity.Log("Discount", $"Archived discount '{item.DiscountName}'");
        if (EditingId == item.DiscountId) CloseForm();
        Load();
    }

    [RelayCommand]
    private void Restore(Discount? discount)
    {
        var item = discount ?? SelectedItem;
        if (item is null || !item.IsArchived) return;
        _discounts.Archive(item.DiscountId, false);
        _activity.Log("Discount", $"Restored discount '{item.DiscountName}'");
        Load();
    }

    [RelayCommand]
    private void Delete(Discount? discount)
    {
        var item = discount ?? SelectedItem;
        if (item is null) return;
        if (!DialogService.Confirm($"Permanently delete '{item.DiscountName}'?", "Discounts")) return;

        try
        {
            _discounts.Delete(item.DiscountId);
            _activity.Log("Discount", $"Deleted discount '{item.DiscountName}'");
            if (EditingId == item.DiscountId) CloseForm();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Discounts");
        }
    }

    private void UpdateDiscountTypeOptions()
    {
        DiscountTypeOptions = ApplyScope == "Store"
            ? new ObservableCollection<string> { "PercentOff", "FixedAmount" }
            : new ObservableCollection<string> { "PercentOff", "SalePrice" };

        if (!DiscountTypeOptions.Contains(DiscountType))
            DiscountType = DiscountTypeOptions[0];
    }

    private void ClearForm()
    {
        EditingId = 0;
        DiscountName = string.Empty;
        ApplyScope = "Store";
        DiscountType = "PercentOff";
        DiscountValue = 0;
        CategoryId = null;
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
        FormTitle = "Add Discount";
        UpdateDiscountTypeOptions();
        foreach (var row in ProductOptions)
            row.IsSelected = false;
    }
}
