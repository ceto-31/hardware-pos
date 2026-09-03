using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class PosViewModel : ObservableObject
{
    private readonly ProductRepository _products = new();
    private readonly SalesRepository _sales = new();
    private readonly SettingsRepository _settings = new();
    private readonly ReceiptService _receipts = new();

    [ObservableProperty] private ObservableCollection<Product> _productList = new();
    [ObservableProperty] private ObservableCollection<CartItem> _cart = new();
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private CartItem? _selectedCartItem;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _taxRate = 0.12m;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _totalDue;
    [ObservableProperty] private decimal _cashTendered;
    [ObservableProperty] private decimal _changeAmount;
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private ObservableCollection<SaleHistoryRow> _saleHistory = new();
    [ObservableProperty] private SaleHistoryRow? _selectedSale;
    [ObservableProperty] private string _historySearch = string.Empty;

    private readonly ActivityRepository _activity = new();

    [RelayCommand]
    public void Load()
    {
        TaxRate = _settings.GetTaxRate();
        SearchProducts();
        LoadHistory();
        Recalculate();
    }

    [RelayCommand]
    private void LoadHistory()
    {
        SaleHistory = new ObservableCollection<SaleHistoryRow>(_sales.GetHistory(HistorySearch));
    }

    [RelayCommand]
    private void SearchProducts()
    {
        ProductList = new ObservableCollection<Product>(_products.GetAll(SearchText));
    }

    partial void OnSearchTextChanged(string value) => SearchProducts();
    partial void OnHistorySearchChanged(string value) => LoadHistory();

    [RelayCommand]
    private void AddSelected()
    {
        if (SelectedProduct is null) return;
        AddProductToCart(SelectedProduct);
    }

    private void AddProductToCart(Product product)
    {
        if (product.StockQty <= 0)
        {
            DialogService.ShowWarning("Product is out of stock.", "POS");
            return;
        }

        CartItem target;
        var existing = Cart.FirstOrDefault(c => c.ProductId == product.ProductId);
        if (existing is not null)
        {
            if (existing.Quantity + 1 > product.StockQty)
            {
                DialogService.ShowWarning("Not enough stock.", "POS");
                return;
            }
            existing.Quantity += 1;
            Cart = new ObservableCollection<CartItem>(Cart);
            target = existing;
        }
        else
        {
            target = new CartItem
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Barcode = product.Barcode,
                UnitOfMeasure = product.UnitOfMeasure,
                UnitPrice = product.EffectivePrice,
                Quantity = 1,
                AvailableStock = product.StockQty
            };
            Cart.Add(target);
        }

        SelectedCartItem = target;
        Recalculate();
    }

    private CartItem? GetTargetCartItem() => SelectedCartItem ?? Cart.LastOrDefault();

    [RelayCommand]
    private void RemoveCartItem()
    {
        var item = GetTargetCartItem();
        if (item is null) return;
        Cart.Remove(item);
        SelectedCartItem = Cart.LastOrDefault();
        Recalculate();
    }

    [RelayCommand]
    private void ClearCart()
    {
        Cart.Clear();
        CashTendered = 0;
        Recalculate();
    }

    partial void OnCashTenderedChanged(decimal value) => Recalculate();
    partial void OnTaxRateChanged(decimal value) => Recalculate();

    private void Recalculate()
    {
        Subtotal = Cart.Sum(c => c.LineTotal);
        TaxAmount = Math.Round(Subtotal * TaxRate, 2);
        TotalDue = Math.Round(Subtotal + TaxAmount, 2);
        ChangeAmount = Math.Max(0, CashTendered - TotalDue);
    }

    [RelayCommand]
    private void IncreaseQty()
    {
        var item = GetTargetCartItem();
        if (item is null) return;
        SelectedCartItem = item;
        if (item.Quantity + 1 > item.AvailableStock)
        {
            DialogService.ShowWarning("Not enough stock.", "POS");
            return;
        }
        item.Quantity += 1;
        Cart = new ObservableCollection<CartItem>(Cart);
        Recalculate();
    }

    [RelayCommand]
    private void DecreaseQty()
    {
        var item = GetTargetCartItem();
        if (item is null) return;
        SelectedCartItem = item;
        if (item.Quantity <= 1)
        {
            Cart.Remove(item);
            SelectedCartItem = Cart.LastOrDefault();
        }
        else
        {
            item.Quantity -= 1;
            Cart = new ObservableCollection<CartItem>(Cart);
        }
        Recalculate();
    }

    [RelayCommand]
    private void CancelTransaction()
    {
        if (Cart.Count == 0) return;
        if (!DialogService.Confirm("Cancel this transaction and clear the cart?", "POS")) return;
        ClearCart();
    }

    [RelayCommand]
    private void PreviewSelectedReceipt()
    {
        if (SelectedSale is null)
        {
            DialogService.ShowInfo("Select a transaction first.", "POS");
            return;
        }

        var items = _sales.GetSaleItems(SelectedSale.SaleId)
            .Select(i => new CartItem
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList();

        _receipts.PreviewReceipt(
            _settings.GetStoreName(),
            SelectedSale.InvoiceNo,
            SelectedSale.CashierName,
            items,
            SelectedSale.Subtotal,
            SelectedSale.TaxAmount,
            SelectedSale.DiscountAmount,
            SelectedSale.TotalDue,
            SelectedSale.CashTendered,
            SelectedSale.ChangeAmount,
            _settings.GetReceiptFooter());
    }

    [RelayCommand]
    private void Checkout()
    {
        if (Cart.Count == 0)
        {
            DialogService.ShowWarning("Cart is empty.", "POS");
            return;
        }

        if (CashTendered < TotalDue)
        {
            DialogService.ShowWarning("Cash tendered is less than total due.", "POS");
            return;
        }

        var user = SessionManager.CurrentUser;
        if (user is null)
        {
            DialogService.ShowError("Session expired. Please log in again.", "POS");
            return;
        }

        try
        {
            var cartSnapshot = Cart.ToList();
            var invoiceNo = _sales.CompleteSale(
                user.UserId,
                cartSnapshot,
                Subtotal,
                TaxAmount,
                0,
                TotalDue,
                CashTendered,
                ChangeAmount);

            _activity.Log("Sale", $"Completed sale {invoiceNo} totaling ₱{TotalDue:N2}", user.UserId);

            try
            {
                _receipts.PreviewReceipt(
                    _settings.GetStoreName(),
                    invoiceNo,
                    user.FullName,
                    cartSnapshot,
                    Subtotal,
                    TaxAmount,
                    0,
                    TotalDue,
                    CashTendered,
                    ChangeAmount,
                    _settings.GetReceiptFooter());
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message, "POS");
            }

            ClearCart();
            SearchProducts();
            LoadHistory();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "POS");
        }
    }
}
