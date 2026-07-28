using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class ArchivesViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<Product> _archivedProducts = new();
    [ObservableProperty] private ObservableCollection<Supplier> _archivedSuppliers = new();
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private string _productSearch = string.Empty;
    [ObservableProperty] private string _supplierSearch = string.Empty;

    [RelayCommand]
    public void Load()
    {
        RefreshProducts();
        RefreshSuppliers();
    }

    partial void OnProductSearchChanged(string value) => RefreshProducts();
    partial void OnSupplierSearchChanged(string value) => RefreshSuppliers();

    [RelayCommand]
    private void RefreshProducts()
    {
        ArchivedProducts = new ObservableCollection<Product>(
            _products.GetAll(ProductSearch, includeArchived: true)
                .Where(p => p.IsArchived));
    }

    [RelayCommand]
    private void RefreshSuppliers()
    {
        ArchivedSuppliers = new ObservableCollection<Supplier>(
            _suppliers.GetAll(SupplierSearch, includeArchived: true)
                .Where(s => s.IsArchived));
    }

    [RelayCommand]
    private void RestoreProduct(Product? product)
    {
        var item = product ?? SelectedProduct;
        if (item is null) return;
        _products.Archive(item.ProductId, false);
        _activity.Log("Archive", $"Restored product '{item.ProductName}'");
        RefreshProducts();
    }

    [RelayCommand]
    private void PermanentDeleteProduct(Product? product)
    {
        var item = product ?? SelectedProduct;
        if (item is null) return;
        if (!DialogService.Confirm("Permanently delete this product if unused?", "Archives")) return;
        try
        {
            _products.Delete(item.ProductId);
            RefreshProducts();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Archives");
        }
    }

    [RelayCommand]
    private void RestoreSupplier(Supplier? supplier)
    {
        var item = supplier ?? SelectedSupplier;
        if (item is null) return;
        _suppliers.Archive(item.SupplierId, false);
        _activity.Log("Archive", $"Restored supplier '{item.CompanyName}'");
        RefreshSuppliers();
    }

    [RelayCommand]
    private void PermanentDeleteSupplier(Supplier? supplier)
    {
        var item = supplier ?? SelectedSupplier;
        if (item is null) return;
        if (!DialogService.Confirm("Permanently delete this supplier if unused?", "Archives")) return;
        try
        {
            _suppliers.PermanentDelete(item.SupplierId);
            RefreshSuppliers();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Archives");
        }
    }
}
