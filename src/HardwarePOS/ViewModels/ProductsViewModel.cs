using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;
using Microsoft.Win32;

namespace HardwarePOS.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();
    private readonly UnitRepository _units = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<Product> _items = new();
    [ObservableProperty] private ObservableCollection<Supplier> _supplierOptions = new();
    [ObservableProperty] private ObservableCollection<Category> _categoryOptions = new();
    [ObservableProperty] private ObservableCollection<Category> _filterCategories = new();
    [ObservableProperty] private ObservableCollection<UnitOfMeasureItem> _unitOptions = new();
    [ObservableProperty] private Product? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showArchived;
    [ObservableProperty] private int? _filterCategoryId = 0;
    [ObservableProperty] private bool _hasRows;

    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _productDetails = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private int? _unitId;
    [ObservableProperty] private decimal _costPrice;
    [ObservableProperty] private decimal _sellingPrice;
    [ObservableProperty] private decimal _stockQty;
    [ObservableProperty] private decimal _reorderLevel = 10;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private int? _supplierId;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private string _formTitle = "Add Product";
    public bool IsNewProduct => EditingId == 0;
    public bool HasPhoto => PreviewImage is not null;

    private string? _pendingImageFile;
    private bool _clearImage;

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(IsNewProduct));
    partial void OnPreviewImageChanged(ImageSource? value) => OnPropertyChanged(nameof(HasPhoto));
    partial void OnShowArchivedChanged(bool value) => Search();
    partial void OnFilterCategoryIdChanged(int? value) => Search();
    partial void OnSearchTextChanged(string value) => Search();

    [RelayCommand]
    public void Load()
    {
        var suppliers = _suppliers.GetAll(activeOnly: true);
        SupplierOptions = new ObservableCollection<Supplier>(
            new[] { new Supplier { SupplierId = 0, CompanyName = "Select supplier…" } }.Concat(suppliers));
        var cats = _products.GetCategories();
        CategoryOptions = new ObservableCollection<Category>(
            new[] { new Category { CategoryId = 0, CategoryName = "Select category…" } }.Concat(cats));
        FilterCategories = new ObservableCollection<Category>(
            new[] { new Category { CategoryId = 0, CategoryName = "All Categories" } }.Concat(cats));
        UnitOptions = new ObservableCollection<UnitOfMeasureItem>(_units.GetAll(activeOnly: true));
        if (UnitId is null && UnitOptions.Count > 0) UnitId = UnitOptions[0].UnitId;
        Search();
    }

    [RelayCommand]
    private void Search()
    {
        int? cat = FilterCategoryId is null or 0 ? null : FilterCategoryId;
        Items = new ObservableCollection<Product>(_products.GetAll(SearchText, ShowArchived, cat));
        HasRows = Items.Count > 0;
    }

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
        ProductCode = SelectedItem.ProductCode;
        ProductName = SelectedItem.ProductName;
        ProductDetails = SelectedItem.ProductDetails ?? string.Empty;
        Barcode = SelectedItem.Barcode ?? string.Empty;
        UnitId = SelectedItem.UnitId;
        CostPrice = SelectedItem.CostPrice;
        SellingPrice = SelectedItem.SellingPrice;
        StockQty = SelectedItem.StockQty;
        ReorderLevel = SelectedItem.ReorderLevel;
        CategoryId = SelectedItem.CategoryId;
        SupplierId = SelectedItem.SupplierId;
        ImagePath = SelectedItem.ImagePath;
        PreviewImage = ProductImageStore.Load(SelectedItem.ImagePath);
        _pendingImageFile = null;
        _clearImage = false;
        FormTitle = "Edit Product";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            DialogService.ShowWarning("Product name is required.", "Products");
            return;
        }

        try
        {
            var unitName = UnitOptions.FirstOrDefault(u => u.UnitId == UnitId)?.UnitName ?? "Piece";
            var product = new Product
            {
                ProductId = EditingId,
                ProductCode = string.IsNullOrWhiteSpace(ProductCode) ? $"PRD-{DateTime.Now:HHmmss}" : ProductCode.Trim(),
                ProductName = ProductName.Trim(),
                ProductDetails = string.IsNullOrWhiteSpace(ProductDetails) ? null : ProductDetails.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                UnitId = UnitId,
                UnitOfMeasure = unitName,
                CostPrice = CostPrice,
                SellingPrice = SellingPrice,
                StockQty = StockQty,
                ReorderLevel = ReorderLevel,
                CategoryId = CategoryId is null or 0 ? null : CategoryId,
                SupplierId = SupplierId is null or 0 ? null : SupplierId,
                ImagePath = ImagePath
            };

            int productId;
            if (EditingId == 0)
            {
                productId = _products.Insert(product);
                _activity.Log("Product", $"Added product '{product.ProductName}'");
            }
            else
            {
                productId = EditingId;
                _products.Update(product);
                _activity.Log("Product", $"Updated product '{product.ProductName}'");
            }

            if (_clearImage)
            {
                ProductImageStore.Delete(productId);
                _products.UpdateImagePath(productId, null);
            }
            else if (!string.IsNullOrWhiteSpace(_pendingImageFile))
            {
                var fileName = ProductImageStore.Save(productId, _pendingImageFile);
                _products.UpdateImagePath(productId, fileName);
            }

            ClearForm();
            Search();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Products");
        }
    }

    [RelayCommand]
    private void Archive()
    {
        if (SelectedItem is null || SelectedItem.IsArchived) return;
        if (!DialogService.Confirm($"Archive '{SelectedItem.ProductName}'?", "Products")) return;
        _products.Archive(SelectedItem.ProductId, true);
        _activity.Log("Product", $"Archived product '{SelectedItem.ProductName}'");
        Search();
    }

    [RelayCommand]
    private void Restore()
    {
        if (SelectedItem is null || !SelectedItem.IsArchived) return;
        _products.Archive(SelectedItem.ProductId, false);
        _activity.Log("Product", $"Restored product '{SelectedItem.ProductName}'");
        Search();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (!DialogService.Confirm($"Delete '{SelectedItem.ProductName}'?", "Products")) return;
        _products.Delete(SelectedItem.ProductId);
        Search();
    }

    [RelayCommand]
    private void BrowsePhoto()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.jpg;*.jpeg;*.png|JPEG|*.jpg;*.jpeg|PNG|*.png",
            Title = "Select product photo"
        };
        if (dialog.ShowDialog() != true) return;

        _pendingImageFile = dialog.FileName;
        _clearImage = false;
        PreviewImage = ProductImageStore.LoadFromFile(dialog.FileName);
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        _pendingImageFile = null;
        _clearImage = true;
        ImagePath = null;
        PreviewImage = null;
    }

    private void ClearForm()
    {
        EditingId = 0;
        ProductCode = ProductName = ProductDetails = Barcode = string.Empty;
        UnitId = UnitOptions.FirstOrDefault()?.UnitId;
        CostPrice = SellingPrice = StockQty = 0;
        ReorderLevel = 10;
        CategoryId = null;
        SupplierId = null;
        ImagePath = null;
        PreviewImage = null;
        _pendingImageFile = null;
        _clearImage = false;
        FormTitle = "Add Product";
    }
}
