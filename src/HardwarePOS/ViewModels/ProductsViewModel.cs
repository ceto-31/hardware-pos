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
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private DateTime? _saleStartDate;
    [ObservableProperty] private DateTime? _saleEndDate;
    [ObservableProperty] private decimal _stockQty;
    [ObservableProperty] private decimal _reorderLevel = 10;
    [ObservableProperty] private DateTime? _expirationDate;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private int? _supplierId;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private string _formTitle = "Add Product";
    [ObservableProperty] private bool _isFormOpen;
    public bool IsNewProduct => EditingId == 0;
    public bool HasPhoto => PreviewImage is not null;
    public bool HasExpirationDate => ExpirationDate.HasValue;
    public bool HasSaleFields => SalePrice > 0 || SaleStartDate.HasValue || SaleEndDate.HasValue;

    public decimal ProfitAmount => SellingPrice - CostPrice;
    public decimal MarkupPercent => CostPrice > 0 ? (SellingPrice - CostPrice) / CostPrice * 100 : 0;
    public string ProfitSummary => $"Profit {ProfitAmount:N2}  ·  Markup {MarkupPercent:N1}%";
    public string ProfitTone => ProfitAmount > 0 ? "Positive" : ProfitAmount < 0 ? "Negative" : "Neutral";

    private string? _pendingImageFile;
    private bool _clearImage;

    partial void OnEditingIdChanged(int value) => OnPropertyChanged(nameof(IsNewProduct));
    partial void OnPreviewImageChanged(ImageSource? value) => OnPropertyChanged(nameof(HasPhoto));
    partial void OnExpirationDateChanged(DateTime? value) => OnPropertyChanged(nameof(HasExpirationDate));
    partial void OnSalePriceChanged(decimal value) => OnPropertyChanged(nameof(HasSaleFields));
    partial void OnSaleStartDateChanged(DateTime? value) => OnPropertyChanged(nameof(HasSaleFields));
    partial void OnSaleEndDateChanged(DateTime? value) => OnPropertyChanged(nameof(HasSaleFields));
    partial void OnShowArchivedChanged(bool value) => Search();
    partial void OnFilterCategoryIdChanged(int? value) => Search();
    partial void OnSearchTextChanged(string value) => Search();
    partial void OnCostPriceChanged(decimal value) => NotifyPricing();
    partial void OnSellingPriceChanged(decimal value) => NotifyPricing();

    private void NotifyPricing()
    {
        OnPropertyChanged(nameof(ProfitAmount));
        OnPropertyChanged(nameof(MarkupPercent));
        OnPropertyChanged(nameof(ProfitSummary));
        OnPropertyChanged(nameof(ProfitTone));
    }

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
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        ClearForm();
        IsFormOpen = false;
    }

    [RelayCommand]
    private void Edit(Product? product)
    {
        var item = product ?? SelectedItem;
        if (item is null) return;
        SelectedItem = item;
        EditingId = item.ProductId;
        ProductCode = item.ProductCode;
        ProductName = item.ProductName;
        ProductDetails = item.ProductDetails ?? string.Empty;
        Barcode = item.Barcode ?? string.Empty;
        UnitId = item.UnitId;
        CostPrice = item.CostPrice;
        SellingPrice = item.SellingPrice;
        SalePrice = item.SalePrice ?? 0;
        SaleStartDate = item.SaleStartDate;
        SaleEndDate = item.SaleEndDate;
        StockQty = item.StockQty;
        ReorderLevel = item.ReorderLevel;
        ExpirationDate = item.ExpirationDate;
        CategoryId = item.CategoryId;
        SupplierId = item.SupplierId;
        ImagePath = item.ImagePath;
        PreviewImage = ProductImageStore.Load(item.ImagePath);
        _pendingImageFile = null;
        _clearImage = false;
        FormTitle = "Edit Product";
        IsFormOpen = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            DialogService.ShowWarning("Product name is required.", "Products");
            return;
        }

        decimal? salePrice = null;
        DateTime? saleStart = null;
        DateTime? saleEnd = null;
        if (SalePrice > 0)
        {
            if (SalePrice >= SellingPrice)
            {
                DialogService.ShowWarning("Sale price must be less than the selling price.", "Products");
                return;
            }
            if (SaleStartDate is null || SaleEndDate is null)
            {
                DialogService.ShowWarning("Sale start and end dates are required when a sale price is set.", "Products");
                return;
            }
            if (SaleEndDate.Value.Date < SaleStartDate.Value.Date)
            {
                DialogService.ShowWarning("Sale end date cannot be earlier than the start date.", "Products");
                return;
            }

            salePrice = SalePrice;
            saleStart = SaleStartDate.Value.Date;
            saleEnd = SaleEndDate.Value.Date;
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
                SalePrice = salePrice,
                SaleStartDate = saleStart,
                SaleEndDate = saleEnd,
                StockQty = StockQty,
                ReorderLevel = ReorderLevel,
                ExpirationDate = ExpirationDate?.Date,
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
            IsFormOpen = false;
            Search();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Products");
        }
    }

    [RelayCommand]
    private void Archive(Product? product)
    {
        var item = product ?? SelectedItem;
        if (item is null || item.IsArchived) return;
        if (!DialogService.Confirm($"Archive '{item.ProductName}'?", "Products")) return;
        _products.Archive(item.ProductId, true);
        _activity.Log("Product", $"Archived product '{item.ProductName}'");
        if (EditingId == item.ProductId)
        {
            ClearForm();
            IsFormOpen = false;
        }
        Search();
    }

    [RelayCommand]
    private void Restore(Product? product)
    {
        var item = product ?? SelectedItem;
        if (item is null || !item.IsArchived) return;
        _products.Archive(item.ProductId, false);
        _activity.Log("Product", $"Restored product '{item.ProductName}'");
        Search();
    }

    [RelayCommand]
    private void ClearExpirationDate() => ExpirationDate = null;

    [RelayCommand]
    private void ClearSale()
    {
        SalePrice = 0;
        SaleStartDate = null;
        SaleEndDate = null;
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
        CostPrice = SellingPrice = SalePrice = StockQty = 0;
        SaleStartDate = SaleEndDate = null;
        ReorderLevel = 10;
        ExpirationDate = null;
        CategoryId = null;
        SupplierId = null;
        ImagePath = null;
        PreviewImage = null;
        _pendingImageFile = null;
        _clearImage = false;
        FormTitle = "Add Product";
    }
}
