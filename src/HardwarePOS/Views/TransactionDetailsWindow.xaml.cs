using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.Views;

public partial class TransactionDetailsWindow : Window
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public TransactionDetailsWindow(SaleHistoryRow sale, IReadOnlyList<SaleHistoryItemRow> items)
    {
        InitializeComponent();
        InvoiceText.Text = sale.InvoiceNo;
        WhenText.Text = sale.SaleDate.ToString("MMM dd, yyyy  h:mm tt", Us);
        CashierText.Text = sale.CashierName;
        OrderTypeText.Text = string.IsNullOrWhiteSpace(sale.OrderType) ? "Pickup" : sale.OrderType;
        if (sale.IsDelivery)
        {
            DeliveryText.Text = $"{sale.CustomerName}\n{sale.ContactNumber}\n{sale.DeliveryAddress}";
            DeliveryText.Visibility = Visibility.Visible;
        }
        StatusBadge.Background = sale.IsVoided
            ? new SolidColorBrush(Color.FromRgb(254, 226, 226))
            : new SolidColorBrush(Color.FromRgb(220, 252, 231));
        StatusBadge.Child = new TextBlock
        {
            Text = sale.IsVoided ? "VOIDED" : "PAID",
            Foreground = sale.IsVoided
                ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                : new SolidColorBrush(Color.FromRgb(22, 101, 52)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        if (sale.IsVoided && !string.IsNullOrWhiteSpace(sale.VoidReason))
        {
            ReasonText.Text = $"Reason: {sale.VoidReason}";
            ReasonText.Visibility = Visibility.Visible;
        }

        foreach (var item in items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock
            {
                Text = $"{item.ProductName}  × {item.Quantity:N0}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var amount = new TextBlock
            {
                Text = item.LineTotal.ToString("₱#,##0.00", Us),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(amount, 1);
            row.Children.Add(name);
            row.Children.Add(amount);
            ItemsPanel.Children.Add(row);
        }

        SubtotalText.Text = $"Subtotal  {sale.Subtotal.ToString("₱#,##0.00", Us)}";
        TaxText.Text = $"Tax  {sale.TaxAmount.ToString("₱#,##0.00", Us)}";
        DiscountText.Text = $"Discount  {sale.DiscountAmount.ToString("₱#,##0.00", Us)}";
        TotalText.Text = sale.TotalDue.ToString("₱#,##0.00", Us);
        if (sale.IsVoided)
        {
            TotalText.TextDecorations = TextDecorations.Strikethrough;
            TotalText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
    }

    public static void Show(SaleHistoryRow sale, IReadOnlyList<SaleHistoryItemRow> items)
    {
        var owner = DialogService.GetVisibleOwner();
        var dialog = new TransactionDetailsWindow(sale, items);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.Width = owner.ActualWidth > 0 ? owner.ActualWidth : SystemParameters.PrimaryScreenWidth;
            dialog.Height = owner.ActualHeight > 0 ? owner.ActualHeight : SystemParameters.PrimaryScreenHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowState = WindowState.Maximized;
        }

        dialog.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
