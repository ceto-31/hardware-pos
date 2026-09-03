using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HardwarePOS.Models;

namespace HardwarePOS.Services;

public class ReceiptService
{
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
        var doc = BuildDocument(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            discountAmount, totalDue, cashTendered, changeAmount, footer);

        var viewer = new FlowDocumentScrollViewer
        {
            Document = doc,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var printBtn = new Button
        {
            Content = "Print",
            Margin = new Thickness(8),
            Padding = new Thickness(16, 6, 16, 6)
        };
        printBtn.Click += (_, _) =>
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.PageWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Receipt {invoiceNo}");
            }
        };

        var panel = new DockPanel();
        DockPanel.SetDock(printBtn, Dock.Bottom);
        panel.Children.Add(printBtn);
        panel.Children.Add(viewer);

        var owner = DialogService.GetVisibleOwner();
        var window = new Window
        {
            Title = $"Receipt Preview — {invoiceNo}",
            Width = 480,
            Height = 640,
            Content = panel,
            WindowStartupLocation = owner is not null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen
        };
        if (owner is not null)
            window.Owner = owner;
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
        var doc = BuildDocument(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            discountAmount, totalDue, cashTendered, changeAmount, footer);
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            doc.PageHeight = printDialog.PrintableAreaHeight;
            doc.PageWidth = printDialog.PrintableAreaWidth;
            printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Receipt {invoiceNo}");
        }
    }

    private static FlowDocument BuildDocument(
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
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            ColumnWidth = 400
        };

        doc.Blocks.Add(new Paragraph(new Run(storeName)) { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        doc.Blocks.Add(new Paragraph(new Run("Sales Receipt")) { TextAlignment = TextAlignment.Center });
        doc.Blocks.Add(new Paragraph(new Run($"Invoice: {invoiceNo}")));
        doc.Blocks.Add(new Paragraph(new Run($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}")));
        doc.Blocks.Add(new Paragraph(new Run($"Cashier: {cashierName}")));
        doc.Blocks.Add(new Paragraph(new Run(new string('-', 40))));
        doc.Blocks.Add(BuildLineItemsTable(items));
        doc.Blocks.Add(new Paragraph(new Run(new string('-', 40))));
        doc.Blocks.Add(new Paragraph(new Run($"Subtotal:   ₱{subtotal:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"VAT:        ₱{taxAmount:N2}")));
        if (discountAmount > 0)
            doc.Blocks.Add(new Paragraph(new Run($"Discount:   ₱{discountAmount:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"TOTAL DUE:  ₱{totalDue:N2}")) { FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Cash:       ₱{cashTendered:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"Change:     ₱{changeAmount:N2}")));
        var footerText = string.IsNullOrWhiteSpace(footer) ? "Thank you for shopping with us!" : footer;
        doc.Blocks.Add(new Paragraph(new Run(footerText)) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
        return doc;
    }

    private static Table BuildLineItemsTable(IReadOnlyList<CartItem> items)
    {
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 4)
        };

        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(45) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        var headerRow = new TableRow();
        AddTableCell(headerRow, "Item", TextAlignment.Left, bold: true);
        AddTableCell(headerRow, "Qty", TextAlignment.Right, bold: true);
        AddTableCell(headerRow, "Price", TextAlignment.Right, bold: true);
        AddTableCell(headerRow, "Total", TextAlignment.Right, bold: true);
        rowGroup.Rows.Add(headerRow);

        foreach (var item in items)
        {
            var row = new TableRow();
            AddTableCell(row, item.ProductName, TextAlignment.Left);
            AddTableCell(row, FormatQuantity(item.Quantity), TextAlignment.Right);
            AddTableCell(row, $"₱{item.UnitPrice:N2}", TextAlignment.Right);
            AddTableCell(row, $"₱{item.LineTotal:N2}", TextAlignment.Right);
            rowGroup.Rows.Add(row);
        }

        return table;
    }

    private static void AddTableCell(TableRow row, string text, TextAlignment align, bool bold = false)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            TextAlignment = align,
            Margin = new Thickness(0),
            Padding = new Thickness(2, 1, 2, 1)
        };
        if (bold)
            paragraph.FontWeight = FontWeights.Bold;

        row.Cells.Add(new TableCell(paragraph));
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity) ? quantity.ToString("N0") : quantity.ToString("N2");
}
