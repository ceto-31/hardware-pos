using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;

namespace HardwarePOS.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();

    [ObservableProperty] private ObservableCollection<Product> _items = new();
    [ObservableProperty] private ObservableCollection<Supplier> _supplierOptions = new();
    [ObservableProperty] private ObservableCollection<Category> _categoryOptions = new();
    [ObservableProperty] private ObservableCollection<string> _unitOptions = new() { "Piece", "Box", "Meter", "Kilogram", "Liter", "Pack" };
    [ObservableProperty] private Product? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showArchived;

    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _productDetails = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _unitOfMeasure = "Piece";
    [ObservableProperty] private decimal _costPrice;
    [ObservableProperty] private decimal _sellingPrice;
    [ObservableProperty] private decimal _stockQty;
    [ObservableProperty] private decimal _reorderLevel = 10;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private int? _supplierId;
    [ObservableProperty] private string _formTitle = "Add Product";
    public bool IsNewProduct => EditingId == 0;

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(IsNewProduct));

    [RelayCommand]
    public void Load()
    {
        SupplierOptions = new ObservableCollection<Supplier>(_suppliers.GetAll(activeOnly: true));
        CategoryOptions = new ObservableCollection<Category>(_products.GetCategories());
        Search();
    }

    [RelayCommand]
    private void Search()
    {
        Items = new ObservableCollection<Product>(_products.GetAll(SearchText, ShowArchived));
    }

    partial void OnShowArchivedChanged(bool value) => Search();

    [RelayCommand]
    private void New()
    {
        ClearForm();
        FormTitle = "Add Product";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedItem is null) return;
        EditingId = SelectedItem.ProductId;
        ProductName = SelectedItem.ProductName;
        ProductDetails = SelectedItem.ProductDetails ?? string.Empty;
        Barcode = SelectedItem.Barcode ?? string.Empty;
        UnitOfMeasure = SelectedItem.UnitOfMeasure;
        CostPrice = SelectedItem.CostPrice;
        SellingPrice = SelectedItem.SellingPrice;
        StockQty = SelectedItem.StockQty;
        ReorderLevel = SelectedItem.ReorderLevel;
        CategoryId = SelectedItem.CategoryId;
        SupplierId = SelectedItem.SupplierId;
        FormTitle = "Edit Product";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            MessageBox.Show("Product name is required.", "Products", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var product = new Product
            {
                ProductId = EditingId,
                ProductName = ProductName.Trim(),
                ProductDetails = string.IsNullOrWhiteSpace(ProductDetails) ? null : ProductDetails.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                UnitOfMeasure = UnitOfMeasure,
                CostPrice = CostPrice,
                SellingPrice = SellingPrice,
                StockQty = StockQty,
                ReorderLevel = ReorderLevel,
                CategoryId = CategoryId,
                SupplierId = SupplierId
            };

            if (EditingId == 0)
                _products.Insert(product);
            else
                _products.Update(product);

            ClearForm();
            Search();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Products", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Archive()
    {
        if (SelectedItem is null) return;
        if (SelectedItem.IsArchived)
        {
            MessageBox.Show("Product is already archived. Use Restore.", "Products",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Archive '{SelectedItem.ProductName}'?", "Products",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _products.Archive(SelectedItem.ProductId, true);
        Search();
    }

    [RelayCommand]
    private void Restore()
    {
        if (SelectedItem is null) return;
        if (!SelectedItem.IsArchived)
        {
            MessageBox.Show("Select an archived product to restore. Enable 'Show archived' first.", "Products",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Restore '{SelectedItem.ProductName}'?", "Products",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _products.Archive(SelectedItem.ProductId, false);
        Search();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Delete '{SelectedItem.ProductName}'? Related history will archive instead.", "Products",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _products.Delete(SelectedItem.ProductId);
        Search();
    }

    private void ClearForm()
    {
        EditingId = 0;
        ProductName = string.Empty;
        ProductDetails = string.Empty;
        Barcode = string.Empty;
        UnitOfMeasure = "Piece";
        CostPrice = 0;
        SellingPrice = 0;
        StockQty = 0;
        ReorderLevel = 10;
        CategoryId = null;
        SupplierId = null;
        FormTitle = "Add Product";
    }
}
