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
    public PagedList<InventoryLedgerEntry> HistoryPage { get; } = new();
    public PagedList<InventoryBatch> DeliveriesPage { get; } = new();
    public PagedList<BadStockRow> BadStockPage { get; } = new();
    public InventoryViewModel() => BadStockPage.PageChanged += () => SelectedBadStock = null;
    partial void OnHistoryChanged(ObservableCollection<InventoryLedgerEntry> value) => HistoryPage.SetSource(value);
    partial void OnDeliveriesChanged(ObservableCollection<InventoryBatch> value) => DeliveriesPage.SetSource(value);
    partial void OnBadStockChanged(ObservableCollection<BadStockRow> value) => BadStockPage.SetSource(value);
    private readonly ProductRepository _products = new();
    private readonly SupplierRepository _suppliers = new();
    private readonly InventoryRepository _inventory = new();
    private List<Product> _monitoringSource = [];
    [ObservableProperty] private ObservableCollection<InventoryBatch> _inBatches = new();
    [ObservableProperty] private ObservableCollection<InventoryBatch> _outBatches = new();
    [ObservableProperty] private ObservableCollection<InventoryBatch> _deliveries = new();
    [ObservableProperty] private ObservableCollection<Supplier> _historySuppliers = new();
    [ObservableProperty] private ObservableCollection<BadStockRow> _badStock = new();
    [ObservableProperty] private BadStockRow? _selectedBadStock;
    [ObservableProperty] private int _deliverySupplierFilter;
    [ObservableProperty] private int? _outBatchId;
    [ObservableProperty] private decimal _boQuantity = 1;
    [ObservableProperty] private string _inQuantityText = "1";
    [ObservableProperty] private string _outQuantityText = "1";
    [ObservableProperty] private string _boQuantityText = "1";
    partial void OnInQuantityTextChanged(string value) { if(decimal.TryParse(value,out var q)) InQuantity=q; }
    [ObservableProperty] private string _boReason = string.Empty;
    [ObservableProperty] private decimal _currentInStock;
    [ObservableProperty] private string _inUnit = string.Empty;
    public decimal AfterReceiving => CurrentInStock + InQuantity;
    partial void OnInQuantityChanged(decimal value) => OnPropertyChanged(nameof(AfterReceiving));
    partial void OnCurrentInStockChanged(decimal value) => OnPropertyChanged(nameof(AfterReceiving));
    partial void OnInProductIdChanged(int? value) => RefreshInBalance();
    partial void OnOutProductIdChanged(int? value)
    {
        OutBatches = new ObservableCollection<InventoryBatch>(value is null ? [] : BatchStock.GetBatches(value).Where(b=>b.RemainingQuantity>0));
        OutBatchId=null;
    }
    partial void OnDeliverySupplierFilterChanged(int value) => RefreshDeliveries();
    [RelayCommand]
    private void ClearOutBatch() => OutBatchId = null;
    private void RefreshInBalance()
    {
        var product=InProductId is int id?_products.GetById(id):null;
        CurrentInStock=product?.StockQty??0; InUnit=product?.UnitOfMeasure??"";
        InBatches=new ObservableCollection<InventoryBatch>(product is null?[]:BatchStock.GetBatches(product.ProductId));
    }
    [RelayCommand]
    private void RefreshDeliveries()
    {
        Deliveries=new ObservableCollection<InventoryBatch>(BatchStock.GetBatches(supplierId:DeliverySupplierFilter));
        BadStock=new ObservableCollection<BadStockRow>(new ReturnsRepository().GetBadStock(DeliverySupplierFilter));
    }
    [RelayCommand]
    private void DisposeBo(string action)
    {
        if(!decimal.TryParse(BoQuantityText,out var q)) {DialogService.ShowWarning("Enter a valid BO quantity.","BO");return;}
        BoQuantity=q;
        if(SelectedBadStock is null) { DialogService.ShowWarning("Select a BO entry.","BO"); return; }
        if(!DialogService.Confirm($"{(action=="Disposed"?"Dispose":"Return to supplier")} {BoQuantity:0.###} {SelectedBadStock.Unit} from BO?", "BO")) return;
        try
        {
            new ReturnsRepository().DisposeBadStock(SelectedBadStock.BadStockId,BoQuantity,action,BoReason);
            BoReason=""; BoQuantity=1; BoQuantityText="1"; RefreshDeliveries();
        }
        catch(Exception ex) { DialogService.ShowError(ex.Message,"BO"); }
    }

    [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
    [ObservableProperty] private bool _filterLowStock;
    [ObservableProperty] private bool _filterOutOfStock;
    [ObservableProperty] private bool _filterExpiringSoon;
    [ObservableProperty] private ObservableCollection<InventoryLedgerEntry> _history = new();
    [ObservableProperty] private ObservableCollection<Category> _categoryOptions = new();
    [ObservableProperty] private ObservableCollection<Product> _inProductOptions = new();
    [ObservableProperty] private ObservableCollection<Product> _outProductOptions = new();
    [ObservableProperty] private ObservableCollection<Supplier> _supplierOptions = new();
    [ObservableProperty] private ObservableCollection<string> _stockOutReasons = new()
    {
        "Damaged", "ReturnedToSupplier", "InternalUse", "Expired"
    };

    // Stock In
    [ObservableProperty] private int? _inCategoryId;
    [ObservableProperty] private int? _inSupplierId;
    [ObservableProperty] private int? _inProductId;
    [ObservableProperty] private decimal _inQuantity = 1;
    [ObservableProperty] private DateTime _inDate = DateTime.Today;
    [ObservableProperty] private DateTime? _inExpirationDate;
    [ObservableProperty] private string _inRemarks = string.Empty;

    // Stock Out
    [ObservableProperty] private int? _outCategoryId;
    [ObservableProperty] private int? _outProductId;
    [ObservableProperty] private decimal _outQuantity = 1;
    [ObservableProperty] private string? _outReason;
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
        HistorySuppliers = new ObservableCollection<Supplier>(new[] {new Supplier {SupplierId=0,CompanyName="All suppliers"}}.Concat(_suppliers.GetAll()));
        ApplyInProductFilter();
        ApplyOutProductFilter();
        RefreshMonitoring();
        RefreshHistory();
        RefreshInBalance();
        RefreshDeliveries();
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

        if (FilterLowStock)
            items = _monitoringSource.Where(p => p.StockStatus == "LowStock");
        else if (FilterOutOfStock)
            items = _monitoringSource.Where(p => p.StockStatus == "OutOfStock");
        else if (FilterExpiringSoon)
            items = _monitoringSource.Where(p => p.ExpirationAlertStatus is "Expired" or "ExpiringSoon");

        InventoryItems = new ObservableCollection<Product>(items);
    }

    partial void OnFilterLowStockChanged(bool value)
    {
        if (value)
        {
            if (FilterOutOfStock) FilterOutOfStock = false;
            if (FilterExpiringSoon) FilterExpiringSoon = false;
        }
        ApplyMonitoringFilter();
    }

    partial void OnFilterOutOfStockChanged(bool value)
    {
        if (value)
        {
            if (FilterLowStock) FilterLowStock = false;
            if (FilterExpiringSoon) FilterExpiringSoon = false;
        }
        ApplyMonitoringFilter();
    }

    partial void OnFilterExpiringSoonChanged(bool value)
    {
        if (value)
        {
            if (FilterLowStock) FilterLowStock = false;
            if (FilterOutOfStock) FilterOutOfStock = false;
        }
        ApplyMonitoringFilter();
    }

    [RelayCommand]
    private void RefreshHistory()
    {
        History = new ObservableCollection<InventoryLedgerEntry>(_inventory.GetHistory(take: int.MaxValue));
    }

    [RelayCommand]
    private void SaveStockIn()
    {
        if(!decimal.TryParse(InQuantityText,out var quantity)) {DialogService.ShowWarning("Enter a valid incoming quantity.","Stock In");return;}
        InQuantity=quantity;
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
                InExpirationDate?.Date,
                string.IsNullOrWhiteSpace(InRemarks) ? null : InRemarks.Trim(),
                SessionManager.CurrentUser?.UserId);

            InQuantity = 1;
            InQuantityText="1";
            InRemarks = string.Empty;
            InExpirationDate = null;
            InDate = DateTime.Today;
            ApplyInProductFilter();
            RefreshMonitoring();
            RefreshHistory();
            RefreshInBalance(); RefreshDeliveries();
            DialogService.ShowInfo("Stock in saved.", "Stock In");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Stock In");
        }
    }

    [RelayCommand]
    private void ClearInExpirationDate() => InExpirationDate = null;

    [RelayCommand]
    private void SaveStockOut()
    {
        if(!decimal.TryParse(OutQuantityText,out var quantity)) {DialogService.ShowWarning("Enter a valid stock-out quantity.","Stock Out");return;}
        OutQuantity=quantity;
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
                SessionManager.CurrentUser?.UserId, OutBatchId);

            OutQuantity = 1;
            OutQuantityText="1";
            OutRemarks = string.Empty;
            OutDate = DateTime.Today;
            ApplyOutProductFilter();
            RefreshMonitoring();
            RefreshHistory();
            RefreshInBalance(); RefreshDeliveries(); OnOutProductIdChanged(OutProductId);
            DialogService.ShowInfo("Stock out saved.", "Stock Out");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Stock Out");
        }
    }
}
