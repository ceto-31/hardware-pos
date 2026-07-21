using System.Collections.ObjectModel;
using System.Windows;
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
    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _taxRate = 0.12m;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _totalDue;
    [ObservableProperty] private decimal _cashTendered;
    [ObservableProperty] private decimal _changeAmount;

    [RelayCommand]
    public void Load()
    {
        TaxRate = _settings.GetTaxRate();
        SearchProducts();
        Recalculate();
    }

    [RelayCommand]
    private void SearchProducts()
    {
        ProductList = new ObservableCollection<Product>(_products.GetAll(SearchText));
    }

    [RelayCommand]
    private void ScanBarcode()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
        var product = _products.GetByBarcode(BarcodeInput.Trim());
        if (product is null)
        {
            MessageBox.Show("Barcode not found.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
            BarcodeInput = string.Empty;
            return;
        }

        AddProductToCart(product);
        BarcodeInput = string.Empty;
    }

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
            MessageBox.Show("Product is out of stock.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = Cart.FirstOrDefault(c => c.ProductId == product.ProductId);
        if (existing is not null)
        {
            if (existing.Quantity + 1 > product.StockQty)
            {
                MessageBox.Show("Not enough stock.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            existing.Quantity += 1;
            Cart = new ObservableCollection<CartItem>(Cart);
        }
        else
        {
            Cart.Add(new CartItem
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Barcode = product.Barcode,
                UnitOfMeasure = product.UnitOfMeasure,
                UnitPrice = product.SellingPrice,
                Quantity = 1,
                AvailableStock = product.StockQty
            });
        }

        Recalculate();
    }

    [RelayCommand]
    private void RemoveCartItem()
    {
        if (SelectedCartItem is null) return;
        Cart.Remove(SelectedCartItem);
        Recalculate();
    }

    [RelayCommand]
    private void ClearCart()
    {
        Cart.Clear();
        DiscountAmount = 0;
        CashTendered = 0;
        Recalculate();
    }

    partial void OnDiscountAmountChanged(decimal value) => Recalculate();
    partial void OnCashTenderedChanged(decimal value) => Recalculate();
    partial void OnTaxRateChanged(decimal value) => Recalculate();

    private void Recalculate()
    {
        Subtotal = Cart.Sum(c => c.LineTotal);
        if (DiscountAmount < 0) DiscountAmount = 0;
        if (DiscountAmount > Subtotal) DiscountAmount = Subtotal;
        var taxable = Math.Max(0, Subtotal - DiscountAmount);
        TaxAmount = Math.Round(taxable * TaxRate, 2);
        TotalDue = Math.Round(taxable + TaxAmount, 2);
        ChangeAmount = Math.Max(0, CashTendered - TotalDue);
    }

    [RelayCommand]
    private void IncreaseQty()
    {
        if (SelectedCartItem is null) return;
        if (SelectedCartItem.Quantity + 1 > SelectedCartItem.AvailableStock)
        {
            MessageBox.Show("Not enough stock.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SelectedCartItem.Quantity += 1;
        Cart = new ObservableCollection<CartItem>(Cart);
        Recalculate();
    }

    [RelayCommand]
    private void DecreaseQty()
    {
        if (SelectedCartItem is null) return;
        if (SelectedCartItem.Quantity <= 1)
        {
            Cart.Remove(SelectedCartItem);
        }
        else
        {
            SelectedCartItem.Quantity -= 1;
            Cart = new ObservableCollection<CartItem>(Cart);
        }
        Recalculate();
    }

    [RelayCommand]
    private void Checkout()
    {
        if (Cart.Count == 0)
        {
            MessageBox.Show("Cart is empty.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CashTendered < TotalDue)
        {
            MessageBox.Show("Cash tendered is less than total due.", "POS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = SessionManager.CurrentUser;
        if (user is null)
        {
            MessageBox.Show("Session expired. Please log in again.", "POS", MessageBoxButton.OK, MessageBoxImage.Error);
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
                DiscountAmount,
                TotalDue,
                CashTendered,
                ChangeAmount);

            var print = MessageBox.Show($"Sale completed.\nInvoice: {invoiceNo}\n\nPrint receipt?", "POS",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (print == MessageBoxResult.Yes)
            {
                _receipts.PrintReceipt(
                    _settings.GetStoreName(),
                    invoiceNo,
                    user.FullName,
                    cartSnapshot,
                    Subtotal,
                    TaxAmount,
                    DiscountAmount,
                    TotalDue,
                    CashTendered,
                    ChangeAmount,
                    _settings.GetReceiptFooter());
            }

            ClearCart();
            SearchProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "POS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
