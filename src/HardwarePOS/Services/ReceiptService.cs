using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HardwarePOS.Models;

namespace HardwarePOS.Services;

public class ReceiptService
{
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

        foreach (var item in items)
        {
            doc.Blocks.Add(new Paragraph(new Run($"{item.ProductName}")));
            doc.Blocks.Add(new Paragraph(new Run($"  {item.Quantity} x {item.UnitPrice:N2} = {item.LineTotal:N2}")));
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 40))));
        doc.Blocks.Add(new Paragraph(new Run($"Subtotal:   ₱{subtotal:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"VAT:        ₱{taxAmount:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"Discount:   ₱{discountAmount:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"TOTAL DUE:  ₱{totalDue:N2}")) { FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Cash:       ₱{cashTendered:N2}")));
        doc.Blocks.Add(new Paragraph(new Run($"Change:     ₱{changeAmount:N2}")));
        var footerText = string.IsNullOrWhiteSpace(footer) ? "Thank you for shopping with us!" : footer;
        doc.Blocks.Add(new Paragraph(new Run(footerText)) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            doc.PageHeight = printDialog.PrintableAreaHeight;
            doc.PageWidth = printDialog.PrintableAreaWidth;
            printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Receipt {invoiceNo}");
        }
    }
}
