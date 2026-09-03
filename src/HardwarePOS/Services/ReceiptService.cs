using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HardwarePOS.Models;

namespace HardwarePOS.Services;

public class ReceiptService
{
    private const double ReceiptWidth = 320;
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    private static readonly Brush PrimaryBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
    private static readonly Brush PageBgBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));

    public void PreviewReceipt(
        string storeName,
        string invoiceNo,
        string cashierName,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal discountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer = null)
    {
        var receipt = BuildReceiptVisual(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            discountAmount, totalDue, cashTendered, changeAmount, footer);

        var scroll = new ScrollViewer
        {
            Content = receipt,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = PageBgBrush,
            Padding = new Thickness(16, 12, 16, 8),
            Focusable = false
        };
        scroll.SetValue(FrameworkElement.FocusVisualStyleProperty, null);

        var owner = DialogService.GetVisibleOwner();
        var window = new Window
        {
            Title = $"Receipt Preview — {invoiceNo}",
            Width = 400,
            Height = 640,
            Background = PageBgBrush,
            WindowStartupLocation = owner is not null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen
        };
        if (owner is not null)
            window.Owner = owner;

        var buttonBar = CreateActionButtons(invoiceNo, receipt, window.Close);
        var panel = new DockPanel { Background = PageBgBrush };
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        panel.Children.Add(scroll);
        panel.Children.Add(buttonBar);
        window.Content = panel;
        window.ShowDialog();
    }

    public void PrintReceipt(
        string storeName,
        string invoiceNo,
        string cashierName,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal discountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer = null)
    {
        var receipt = BuildReceiptVisual(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            discountAmount, totalDue, cashTendered, changeAmount, footer);

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
            printDialog.PrintVisual(receipt, $"Receipt {invoiceNo}");
    }

    private static UIElement CreateActionButtons(string invoiceNo, UIElement receipt, Action close)
    {
        var closeBtn = new Button
        {
            Content = "Close",
            Padding = new Thickness(16, 10, 16, 10),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (Application.Current.TryFindResource("SecondaryButton") is Style secondaryStyle)
            closeBtn.Style = secondaryStyle;
        closeBtn.Click += (_, _) => close();

        var printBtn = new Button
        {
            Content = "Print",
            Padding = new Thickness(16, 10, 16, 10),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (Application.Current.TryFindResource("PrimaryButton") is Style primaryStyle)
            printBtn.Style = primaryStyle;
        else
        {
            printBtn.Background = PrimaryBrush;
            printBtn.Foreground = Brushes.White;
            printBtn.BorderThickness = new Thickness(0);
        }
        printBtn.Click += (_, _) =>
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
                printDialog.PrintVisual(receipt, $"Receipt {invoiceNo}");
        };

        var grid = new Grid { Margin = new Thickness(16, 8, 16, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(closeBtn, 0);
        Grid.SetColumn(printBtn, 2);
        grid.Children.Add(closeBtn);
        grid.Children.Add(printBtn);
        return grid;
    }

    private static UIElement BuildReceiptVisual(
        string storeName,
        string invoiceNo,
        string cashierName,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal discountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer)
    {
        var root = new StackPanel();

        var card = new Border
        {
            Width = ReceiptWidth,
            Background = Brushes.White,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Child = root
        };

        root.Children.Add(MakeTitle(storeName));
        root.Children.Add(MakeSubtitle("Sales Receipt"));
        root.Children.Add(MakeMeta("Invoice", invoiceNo));
        root.Children.Add(MakeMeta("Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
        root.Children.Add(MakeMeta("Cashier", cashierName));
        root.Children.Add(MakeRule());

        root.Children.Add(BuildItemsGrid(items));
        root.Children.Add(MakeRule());

        root.Children.Add(BuildTotalsStack(subtotal, taxAmount, discountAmount, totalDue, cashTendered, changeAmount));
        root.Children.Add(MakeFooter(footer));

        return card;
    }

    private static TextBlock MakeTitle(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static TextBlock MakeSubtitle(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = MutedBrush,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 12)
    };

    private static TextBlock MakeMeta(string label, string value)
    {
        var block = new TextBlock { Margin = new Thickness(0, 0, 0, 2), LineHeight = 18 };
        block.Inlines.Add(new System.Windows.Documents.Run($"{label}: ") { Foreground = MutedBrush });
        block.Inlines.Add(new System.Windows.Documents.Run(value));
        return block;
    }

    private static Border MakeRule(Thickness? margin = null) => new()
    {
        Height = 1,
        Background = RuleBrush,
        Margin = margin ?? new Thickness(0, 8, 0, 8)
    };

    private static TextBlock MakeFooter(string? footer)
    {
        var footerText = string.IsNullOrWhiteSpace(footer) ? "Thank you for shopping with us!" : footer;
        return new TextBlock
        {
            Text = footerText,
            TextAlignment = TextAlignment.Center,
            Foreground = MutedBrush,
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0)
        };
    }

    private static Grid BuildItemsGrid(IReadOnlyList<CartItem> items)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var row = 0;
        AddItemHeaderRow(grid, row++, "Item", "Qty", "Total");

        foreach (var item in items)
        {
            AddItemRow(grid, row++, item.ProductName, FormatQuantity(item.Quantity), $"₱{item.LineTotal:N2}");
        }

        return grid;
    }

    private static void AddItemHeaderRow(Grid grid, int rowIndex, string col1, string col2, string col3)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bg = new Border { Background = HeaderBgBrush, Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(bg, rowIndex);
        Grid.SetColumnSpan(bg, 3);
        grid.Children.Add(bg);

        AddGridCell(grid, rowIndex, 0, col1, FontWeights.SemiBold, TextAlignment.Left, new Thickness(4, 4, 4, 4));
        AddGridCell(grid, rowIndex, 1, col2, FontWeights.SemiBold, TextAlignment.Right, new Thickness(4, 4, 4, 4));
        AddGridCell(grid, rowIndex, 2, col3, FontWeights.SemiBold, TextAlignment.Right, new Thickness(4, 4, 4, 4));
    }

    private static void AddItemRow(Grid grid, int rowIndex, string name, string qty, string total)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddGridCell(grid, rowIndex, 0, name, FontWeights.Normal, TextAlignment.Left, new Thickness(4, 2, 4, 2));
        AddGridCell(grid, rowIndex, 1, qty, FontWeights.Normal, TextAlignment.Right, new Thickness(4, 2, 4, 2));
        AddGridCell(grid, rowIndex, 2, total, FontWeights.Normal, TextAlignment.Right, new Thickness(4, 2, 4, 2));
    }

    private static void AddGridCell(
        Grid grid,
        int row,
        int column,
        string text,
        FontWeight weight,
        TextAlignment align,
        Thickness margin)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            TextAlignment = align,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private static UIElement BuildTotalsStack(
        decimal subtotal,
        decimal taxAmount,
        decimal discountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

        panel.Children.Add(MakeTotalRow("Subtotal", subtotal));
        panel.Children.Add(MakeTotalRow("VAT", taxAmount));
        if (discountAmount > 0)
            panel.Children.Add(MakeTotalRow("Discount", discountAmount));

        panel.Children.Add(MakeRule(margin: new Thickness(0, 6, 0, 6)));
        panel.Children.Add(MakeTotalRow("TOTAL DUE", totalDue, bold: true));
        panel.Children.Add(MakeTotalRow("Cash", cashTendered));
        panel.Children.Add(MakeTotalRow("Change", changeAmount));

        return panel;
    }

    private static Grid MakeTotalRow(string label, decimal amount, bool bold = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        var weight = bold ? FontWeights.Bold : FontWeights.Normal;

        var labelBlock = new TextBlock { Text = label, FontWeight = weight };
        var amountBlock = new TextBlock
        {
            Text = $"₱{amount:N2}",
            TextAlignment = TextAlignment.Right,
            FontWeight = weight
        };

        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(amountBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(amountBlock);
        return grid;
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity) ? quantity.ToString("N0") : quantity.ToString("N2");
}
