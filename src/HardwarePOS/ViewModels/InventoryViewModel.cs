using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;

namespace HardwarePOS.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();
    private readonly InventoryRepository _inventory = new();

    [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
    [ObservableProperty] private ObservableCollection<InventoryLedgerEntry> _history = new();
    [ObservableProperty] private ObservableCollection<Product> _productOptions = new();
    [ObservableProperty] private ObservableCollection<Supplier> _supplierOptions = new();
    [ObservableProperty] private ObservableCollection<string> _stockOutReasons = new()
    {
        "Damaged", "ReturnedToSupplier", "InternalUse"
    };

    // Stock In
    [ObservableProperty] private int? _inSupplierId;
    [ObservableProperty] private int? _inProductId;
    [ObservableProperty] private decimal _inQuantity = 1;
    [ObservableProperty] private decimal _inCost;
    [ObservableProperty] private DateTime _inDate = DateTime.Today;
    [ObservableProperty] private string _inRemarks = string.Empty;

    // Stock Out
    [ObservableProperty] private int? _outProductId;
    [ObservableProperty] private decimal _outQuantity = 1;
    [ObservableProperty] private string _outReason = "Damaged";
    [ObservableProperty] private DateTime _outDate = DateTime.Today;
    [ObservableProperty] private string _outRemarks = string.Empty;

    [ObservableProperty] private int _selectedTabIndex;

    [RelayCommand]
    public void Load()
    {
        ProductOptions = new ObservableCollection<Product>(_products.GetAll());
        SupplierOptions = new ObservableCollection<Supplier>(_suppliers.GetAll(activeOnly: true));
        RefreshMonitoring();
        RefreshHistory();
    }

    [RelayCommand]
    private void RefreshMonitoring()
    {
        InventoryItems = new ObservableCollection<Product>(_products.GetAll());
    }

    [RelayCommand]
    private void RefreshHistory()
    {
        History = new ObservableCollection<InventoryLedgerEntry>(_inventory.GetHistory());
    }

    [RelayCommand]
    private void SaveStockIn()
    {
        if (InSupplierId is null || InProductId is null || InQuantity <= 0)
        {
            MessageBox.Show("Select supplier, product, and a quantity greater than zero.", "Stock In",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _inventory.StockIn(
                InSupplierId.Value,
                InProductId.Value,
                InQuantity,
                InCost,
                InDate,
                string.IsNullOrWhiteSpace(InRemarks) ? null : InRemarks.Trim(),
                SessionManager.CurrentUser?.UserId);

            InQuantity = 1;
            InCost = 0;
            InRemarks = string.Empty;
            InDate = DateTime.Today;
            ProductOptions = new ObservableCollection<Product>(_products.GetAll());
            RefreshMonitoring();
            RefreshHistory();
            MessageBox.Show("Stock in saved.", "Stock In", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Stock In", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SaveStockOut()
    {
        if (OutProductId is null || OutQuantity <= 0 || string.IsNullOrWhiteSpace(OutReason))
        {
            MessageBox.Show("Select product, reason, and a quantity greater than zero.", "Stock Out",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            ProductOptions = new ObservableCollection<Product>(_products.GetAll());
            RefreshMonitoring();
            RefreshHistory();
            MessageBox.Show("Stock out saved.", "Stock Out", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Stock Out", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
