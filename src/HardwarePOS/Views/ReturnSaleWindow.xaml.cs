using System.Windows;
using System.Windows.Controls;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.Views;

public partial class ReturnSaleWindow : Window
{
    private readonly SaleHistoryRow _sale;
    private readonly ReturnsRepository _returns=new();
    public ReturnSaleWindow(SaleHistoryRow sale)
    {
        _sale=sale; InitializeComponent();
        InvoiceText.Text=$"{sale.InvoiceNo} · Sold {sale.SaleDate:MMM d, yyyy h:mm tt}";
        DeadlineText.Text=$"Return deadline: {sale.SaleDate.AddDays(7):MMM d, yyyy h:mm tt} (7 elapsed days)";
        Reload();
    }
    public static void ShowFor(SaleHistoryRow sale)
    {
        try { new ReturnSaleWindow(sale) {Owner=Application.Current.MainWindow}.ShowDialog(); }
        catch(Exception ex) { DialogService.ShowError(ex.Message,"Returns"); }
    }
    private void Reload()
    {
        ItemsBox.ItemsSource=_returns.GetItems(_sale.SaleId); ItemsBox.SelectedIndex=0; Refresh();
    }
    private void Changed(object sender,RoutedEventArgs e) => Refresh();
    private void Refresh()
    {
        if(SaveButton is null || RefundText is null) return;
        SaveButton.IsEnabled=false;
        // SQL Server validates the deadline when saving; do not reject a just-created
        // receipt because datetime2(0) rounded its timestamp to the next second.
        if(DateTime.Now>_sale.SaleDate.AddDays(7)) { RefundText.Text="Return period expired"; return; }
        try
        {
            if(ItemsBox.SelectedItem is not ReturnableItem item) {RefundText.Text="Select an item"; return;}
            if(!decimal.TryParse(QuantityBox.Text,out var quantity)) {RefundText.Text="Enter a valid quantity";return;}
            var amount=InventoryRules.Refund(item.Paid,item.Quantity,item.Returned,quantity,item.Refunded);
            RefundText.Text=$"Refund: ₱{amount:N2}";
            SaveButton.IsEnabled=!string.IsNullOrWhiteSpace(ReasonBox.Text);
        }
        catch(Exception ex) {RefundText.Text=ex.Message;}
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(ItemsBox.SelectedItem is not ReturnableItem item || !decimal.TryParse(QuantityBox.Text,out var quantity)) return;
        var condition=((ComboBoxItem)ConditionBox.SelectedItem).Content.ToString()!;
        if(!DialogService.Confirm($"Record return of {quantity:0.###} {item.Unit} of {item.ProductName}?\n{RefundText.Text}","Customer return")) return;
        try
        {
            SaveButton.IsEnabled=false;
            var amount=_returns.Return(_sale.SaleId,item.SaleItemId,quantity,condition,ReasonBox.Text);
            DialogService.ShowInfo($"Return saved. Refund due to customer: ₱{amount:N2}.","Customer return");
            Reload();
        }
        catch(Exception ex) {DialogService.ShowError(ex.Message,"Customer return");Refresh();}
    }
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
}
