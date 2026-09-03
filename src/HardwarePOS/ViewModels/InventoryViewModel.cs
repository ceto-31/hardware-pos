using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();
    private readonly InventoryRepository _inventory = new();
    private List<Product> _monitoringSource = [];

    [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
    [ObservableProperty] private bool _filterLowStock;
    [ObservableProperty] private bool _filterOutOfStock;
    [ObservableProperty] private ObservableCollection<InventoryLedgerEntry> _history = new();
    [ObservableProperty] private ObservableCollection<Category> _categoryOptions = new();
    [ObservableProperty] private ObservableCollection<Product> _inProductOptions = new();
    [ObservableProperty] private ObservableCollection<Product> _outProductOptions = new();
    [ObservableProperty] private ObservableCollection<Supplier> _supplierOptions = new();
    [ObservableProperty] private ObservableCollection<string> _stockOutReasons = new()
    {
        "Damaged", "ReturnedToSupplier", "InternalUse"
    };

    // Stock In
    [ObservableProperty] private int? _inCategoryId;
    [ObservableProperty] private int? _inSupplierId;
    [ObservableProperty] private int? _inProductId;
    [ObservableProperty] private decimal _inQuantity = 1;
    [ObservableProperty] private DateTime _inDate = DateTime.Today;
    [ObservableProperty] private string _inRemarks = string.Empty;

    // Stock Out
    [ObservableProperty] private int? _outCategoryId;
    [ObservableProperty] private int? _outProductId;
    [ObservableProperty] private decimal _outQuantity = 1;
    [ObservableProperty] private string _outReason = "Damaged";
    [ObservableProperty] private DateTime _outDate = DateTime.Today;
    [ObservableProperty] private string _outRemarks = string.Empty;

    [ObservableProperty] private int _selectedTabIndex;

    [RelayCommand]
    public void Load()
    {
        var cats = _products.GetCategories();
        CategoryOptions = new ObservableCollection<Category>(
            new[] { new Category { CategoryId = 0, CategoryName = "Select category…" } }.Concat(cats));
        SupplierOptions = new ObservableCollection<Supplier>(_suppliers.GetAll(activeOnly: true));
        ApplyInProductFilter();
        ApplyOutProductFilter();
        RefreshMonitoring();
        RefreshHistory();
    }

    partial void OnInCategoryIdChanged(int? value) => ApplyInProductFilter();
    partial void OnOutCategoryIdChanged(int? value) => ApplyOutProductFilter();

    private void ApplyInProductFilter()
    {
        if (InCategoryId is null or 0)
        {
            InProductOptions = new ObservableCollection<Product>();
            InProductId = null;
            return;
        }

        var products = _products.GetAll(categoryId: InCategoryId);
        InProductOptions = new ObservableCollection<Product>(products);
        if (InProductId is not null && products.All(p => p.ProductId != InProductId))
            InProductId = null;
    }

    private void ApplyOutProductFilter()
    {
        if (OutCategoryId is null or 0)
        {
            OutProductOptions = new ObservableCollection<Product>();
            OutProductId = null;
            return;
        }

        var products = _products.GetAll(categoryId: OutCategoryId);
        OutProductOptions = new ObservableCollection<Product>(products);
        if (OutProductId is not null && products.All(p => p.ProductId != OutProductId))
            OutProductId = null;
    }

    [RelayCommand]
    private void RefreshMonitoring()
    {
        _monitoringSource = _products.GetAll();
        ApplyMonitoringFilter();
    }

    private void ApplyMonitoringFilter()
    {
        IEnumerable<Product> items = _monitoringSource;

        if (FilterLowStock || FilterOutOfStock)
        {
            items = _monitoringSource.Where(p =>
                (FilterLowStock && p.StockStatus == "LowStock") ||
                (FilterOutOfStock && p.StockStatus == "OutOfStock"));
        }

        InventoryItems = new ObservableCollection<Product>(items);
    }

    partial void OnFilterLowStockChanged(bool value) => ApplyMonitoringFilter();
    partial void OnFilterOutOfStockChanged(bool value) => ApplyMonitoringFilter();

    [RelayCommand]
    private void RefreshHistory()
    {
        History = new ObservableCollection<InventoryLedgerEntry>(_inventory.GetHistory());
    }

    [RelayCommand]
    private void SaveStockIn()
    {
        if (InCategoryId is null or 0 || InSupplierId is null || InProductId is null || InQuantity <= 0)
        {
            DialogService.ShowWarning("Select category, supplier, product, and a quantity greater than zero.", "Stock In");
            return;
        }

        try
        {
            _inventory.StockIn(
                InSupplierId.Value,
                InProductId.Value,
                InQuantity,
                InDate,
                string.IsNullOrWhiteSpace(InRemarks) ? null : InRemarks.Trim(),
                SessionManager.CurrentUser?.UserId);

            InQuantity = 1;
            InRemarks = string.Empty;
            InDate = DateTime.Today;
            ApplyInProductFilter();
            RefreshMonitoring();
            RefreshHistory();
            DialogService.ShowInfo("Stock in saved.", "Stock In");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Stock In");
        }
    }

    [RelayCommand]
    private void SaveStockOut()
    {
        if (OutCategoryId is null or 0 || OutProductId is null || OutQuantity <= 0 || string.IsNullOrWhiteSpace(OutReason))
        {
            DialogService.ShowWarning("Select category, product, reason, and a quantity greater than zero.", "Stock Out");
            return;
        }

        try
        {
            _inventory.StockOut(
                OutProductId.Value,
                OutQuantity,
                OutReason,
                OutDate,
                string.IsNullOrWhiteSpace(OutRemarks) ? null : OutRemarks.Trim(),
                SessionManager.CurrentUser?.UserId);

            OutQuantity = 1;
            OutRemarks = string.Empty;
            OutDate = DateTime.Today;
            ApplyOutProductFilter();
            RefreshMonitoring();
            RefreshHistory();
            DialogService.ShowInfo("Stock out saved.", "Stock Out");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Stock Out");
        }
    }
}
