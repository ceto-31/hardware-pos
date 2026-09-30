using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.ViewModels;

namespace HardwarePOS.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void HistoryActions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;
        if (button.DataContext is not SaleHistoryRow sale)
            return;

        button.ContextMenu.DataContext = sale;
        var adminVisibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        foreach (var child in button.ContextMenu.Items)
        {
            switch (child)
            {
                case MenuItem { Name: "VoidMenuItem" } item:
                    item.Visibility = adminVisibility;
                    item.IsEnabled = !sale.IsVoided;
                    item.ToolTip = sale.IsVoided ? "This sale is already voided." : null;
                    break;
                case Separator { Name: "VoidDivider" } separator:
                    separator.Visibility = adminVisibility;
                    break;
            }
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void ViewTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (SaleFromMenu(sender) is not SaleHistoryRow sale)
            return;
        if (HistoryGrid.DataContext is PosViewModel vm && vm.ViewTransactionCommand.CanExecute(sale))
            vm.ViewTransactionCommand.Execute(sale);
    }

    private void ReprintReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (SaleFromMenu(sender) is not SaleHistoryRow sale)
            return;
        if (HistoryGrid.DataContext is PosViewModel vm && vm.PreviewSelectedReceiptCommand.CanExecute(sale))
            vm.PreviewSelectedReceiptCommand.Execute(sale);
    }

    private void VoidTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (SaleFromMenu(sender) is not SaleHistoryRow sale)
            return;
        if (HistoryGrid.DataContext is PosViewModel vm && vm.VoidSelectedSaleCommand.CanExecute(sale))
            vm.VoidSelectedSaleCommand.Execute(sale);
    }

    private void CopyInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (SaleFromMenu(sender) is SaleHistoryRow sale && !string.IsNullOrWhiteSpace(sale.InvoiceNo))
            Clipboard.SetText(sale.InvoiceNo);
    }

    private static SaleHistoryRow? SaleFromMenu(object sender)
    {
        if (sender is not MenuItem item || item.Parent is not ContextMenu menu)
            return null;
        return menu.PlacementTarget is FrameworkElement target ? target.DataContext as SaleHistoryRow : null;
    }

    private bool _formattingTendered;

    private void CashTendered_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            ShowTenderedDecimals(box);
    }

    private void CashTendered_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is TextBox box && !box.IsKeyboardFocused)
            ShowTenderedDecimals(box);
    }

    private void ShowTenderedDecimals(TextBox box)
    {
        if (_formattingTendered || box.DataContext is not PosViewModel vm)
            return;

        var formatted = vm.CashTendered.ToString("0.00");
        if (box.Text == formatted)
            return;

        _formattingTendered = true;
        box.Text = formatted;
        box.CaretIndex = box.Text.Length;
        _formattingTendered = false;
    }

    private void ProductGrid_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var row = FindAncestor<DataGridRow>(source);
        if (row?.Item is null) return;

        if (DataContext is PosViewModel vm && vm.AddSelectedCommand.CanExecute(null))
            vm.AddSelectedCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
