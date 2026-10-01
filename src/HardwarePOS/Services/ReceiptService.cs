using System.Globalization;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HardwarePOS.Models;

namespace HardwarePOS.Services;

public class ReceiptService
{
    // POS58 paper is 58mm. About 5mm on each side is outside the printable area.
    private const double PaperWidthMm = 58;
    private const double SideMarginMm = 5;
    private const double BottomMarginMm = 3;
    private const double MmToDip = 96.0 / 25.4;
    private const double BodyFontSize = 12;
    private const double TitleFontSize = 15;
    private const double LineSpacing = 16;
    private const double ColumnGap = 6;
    private const double ColumnSlack = 2;
    private const double MinimumItemColumn = 64;
    private const string ReceiptFontFamily = "Consolas";
    private const string PosPrinterName = "POS58 Printer";
    // 58mm print heads are 384 dots wide. The preview is scaled onto that width.
    private const int ThermalDotsWide = 384;

    private static readonly double PaperWidthDip = PaperWidthMm * MmToDip;
    private static readonly double SideMarginDip = SideMarginMm * MmToDip;
    private static readonly double BottomMarginDip = BottomMarginMm * MmToDip;
    private static readonly double PrintableWidthDip = PaperWidthDip - (SideMarginDip * 2);

    private static readonly FontFamily ReceiptFont = new(ReceiptFontFamily);
    private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(71, 85, 105)));
    private static readonly Brush RuleBrush = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
    private static readonly Brush PrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(37, 99, 235)));
    private static readonly Brush VoidBrush = Freeze(new SolidColorBrush(Color.FromRgb(185, 28, 28)));
    private static readonly Brush PageBgBrush = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));

    public void PreviewReceipt(
        string storeName,
        string invoiceNo,
        string cashierName,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal itemDiscountAmount,
        decimal storeDiscountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer = null,
        bool isVoided = false,
        string orderType = "Pickup",
        string? customerName = null,
        string? contactNumber = null,
        string? deliveryAddress = null)
    {
        var receipt = BuildReceiptVisual(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            itemDiscountAmount, storeDiscountAmount, totalDue, cashTendered, changeAmount, footer, isVoided,
            orderType, customerName, contactNumber, deliveryAddress);
        MeasureReceipt(receipt);

        var scroll = new ScrollViewer
        {
            Content = receipt,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = PageBgBrush,
            Padding = new Thickness(12, 12, 12, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Top,
            Focusable = false
        };
        scroll.SetValue(FrameworkElement.FocusVisualStyleProperty, null);

        var owner = DialogService.GetVisibleOwner();
        var window = new Window
        {
            Title = $"Receipt Preview — {invoiceNo}",
            Width = PaperWidthDip + 48,
            Height = Math.Min(receipt.Height + 110, SystemParameters.WorkArea.Height - 48),
            MinHeight = 220,
            Background = PageBgBrush,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is not null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen
        };
        if (owner is not null)
            window.Owner = owner;

        var buttonBar = CreateActionButtons(invoiceNo, receipt, window.Close);
        var panel = new DockPanel { Background = PageBgBrush };
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        panel.Children.Add(buttonBar);
        panel.Children.Add(scroll);
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
        decimal itemDiscountAmount,
        decimal storeDiscountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer = null,
        bool isVoided = false,
        string orderType = "Pickup",
        string? customerName = null,
        string? contactNumber = null,
        string? deliveryAddress = null)
    {
        var receipt = BuildReceiptVisual(storeName, invoiceNo, cashierName, items, subtotal, taxAmount,
            itemDiscountAmount, storeDiscountAmount, totalDue, cashTendered, changeAmount, footer, isVoided,
            orderType, customerName, contactNumber, deliveryAddress);
        Print(receipt, invoiceNo);
    }

    private static UIElement CreateActionButtons(string invoiceNo, FrameworkElement receipt, Action close)
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
        printBtn.Click += (_, _) => Print(receipt, invoiceNo);

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

    private static void Print(FrameworkElement receipt, string invoiceNo)
    {
        MeasureReceipt(receipt);
        if (TryPrintThermal(receipt, invoiceNo))
            return;

        var dialog = new PrintDialog();
        ApplyReceiptPageSize(dialog, receipt.Width, receipt.Height);
        if (dialog.ShowDialog() != true)
            return;

        ApplyReceiptPageSize(dialog, receipt.Width, receipt.Height);
        dialog.PrintVisual(receipt, $"Receipt {invoiceNo}");
    }

    private static bool TryPrintThermal(FrameworkElement receipt, string invoiceNo)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(PosPrinterName);
            queue.Refresh();
            var blocked = queue.QueueStatus.HasFlag(PrintQueueStatus.Offline)
                || queue.QueueStatus.HasFlag(PrintQueueStatus.Error)
                || queue.QueueStatus.HasFlag(PrintQueueStatus.PaperOut)
                || queue.QueueStatus.HasFlag(PrintQueueStatus.UserIntervention);
            if (blocked)
            {
                DialogService.ShowWarning("The POS58 printer is not ready. Check that it is turned on and connected, then print again.");
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            RawPrint.Send(PosPrinterName, $"Receipt {invoiceNo}", BuildThermalJob(receipt));
            return true;
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"The receipt could not be printed.\n\n{ex.Message}");
            return true;
        }
    }

    private static byte[] BuildThermalJob(FrameworkElement receipt)
    {
        const int supersample = 2;
        var pixelWidth = ThermalDotsWide * supersample;
        var pixelHeight = Math.Max(supersample, (int)Math.Ceiling(receipt.ActualHeight * pixelWidth / Math.Max(1, receipt.ActualWidth)));
        if (pixelHeight % supersample != 0)
            pixelHeight += supersample - (pixelHeight % supersample);

        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var bounds = new Rect(0, 0, pixelWidth, pixelHeight);
            context.DrawRectangle(Brushes.White, null, bounds);
            context.DrawRectangle(new VisualBrush(receipt)
            {
                Stretch = Stretch.Fill
            }, null, bounds);
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);

        var stride = pixelWidth * 4;
        var pixels = new byte[stride * pixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var widthBytes = ThermalDotsWide / 8;
        var heightDots = pixelHeight / supersample;
        var packed = new byte[widthBytes * heightDots];
        for (var y = 0; y < heightDots; y++)
        {
            for (var x = 0; x < ThermalDotsWide; x++)
            {
                var dark = 0;
                for (var oy = 0; oy < supersample; oy++)
                {
                    for (var ox = 0; ox < supersample; ox++)
                    {
                        var i = ((y * supersample) + oy) * stride + ((x * supersample) + ox) * 4;
                        if (pixels[i + 3] < 32)
                            continue;
                        var luminance = (pixels[i + 2] * 299 + pixels[i + 1] * 587 + pixels[i] * 114) / 1000;
                        if (luminance < 210)
                            dark++;
                    }
                }

                if (dark > 0)
                    packed[(y * widthBytes) + (x / 8)] |= (byte)(0x80 >> (x % 8));
            }
        }

        var job = new List<byte>(packed.Length + 32);
        job.Add(0x1B);
        job.Add(0x40);
        job.Add(0x1B);
        job.Add(0x61);
        job.Add(0x00);

        const int bandRows = 128;
        for (var y = 0; y < heightDots; y += bandRows)
        {
            var rows = Math.Min(bandRows, heightDots - y);
            job.Add(0x1D);
            job.Add(0x76);
            job.Add(0x30);
            job.Add(0x00);
            job.Add((byte)(widthBytes & 0xFF));
            job.Add((byte)((widthBytes >> 8) & 0xFF));
            job.Add((byte)(rows & 0xFF));
            job.Add((byte)((rows >> 8) & 0xFF));
            for (var row = 0; row < rows; row++)
            {
                var start = (y + row) * widthBytes;
                for (var column = 0; column < widthBytes; column++)
                    job.Add(packed[start + column]);
            }
        }

        // A few lines so the footer clears the tear bar, without a long blank page.
        job.Add(0x1B);
        job.Add(0x64);
        job.Add(0x03);
        return job.ToArray();
    }

    private static void ApplyReceiptPageSize(PrintDialog dialog, double width, double height)
    {
        var requested = new PrintTicket
        {
            PageMediaSize = new PageMediaSize(width, height),
            PageOrientation = PageOrientation.Portrait,
            PageScalingFactor = 100
        };

        var queue = dialog.PrintQueue;
        if (queue is null)
        {
            dialog.PrintTicket = requested;
            return;
        }

        try
        {
            var merged = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, requested);
            dialog.PrintTicket = merged.ValidatedPrintTicket ?? requested;
        }
        catch (Exception)
        {
            dialog.PrintTicket = requested;
        }
    }

    private static void MeasureReceipt(FrameworkElement receipt)
    {
        receipt.Width = PaperWidthDip;
        receipt.Measure(new Size(PaperWidthDip, double.PositiveInfinity));
        var height = Math.Ceiling(receipt.DesiredSize.Height);
        receipt.Arrange(new Rect(0, 0, PaperWidthDip, height));
        receipt.UpdateLayout();
        receipt.Height = height;
    }

    private static FrameworkElement BuildReceiptVisual(
        string storeName,
        string invoiceNo,
        string cashierName,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal itemDiscountAmount,
        decimal storeDiscountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount,
        string? footer,
        bool isVoided,
        string orderType,
        string? customerName,
        string? contactNumber,
        string? deliveryAddress)
    {
        var root = new StackPanel { Width = PrintableWidthDip };

        var card = new Border
        {
            Width = PaperWidthDip,
            Background = Brushes.White,
            Padding = new Thickness(SideMarginDip, SideMarginDip, SideMarginDip, BottomMarginDip),
            SnapsToDevicePixels = true,
            UseLayoutRounding = false,
            Child = root
        };
        TextOptions.SetTextFormattingMode(card, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(card, TextRenderingMode.Grayscale);
        card.SetValue(TextElement.FontFamilyProperty, ReceiptFont);
        card.SetValue(TextElement.FontSizeProperty, BodyFontSize);
        card.SetValue(TextElement.ForegroundProperty, Brushes.Black);

        root.Children.Add(MakeCentered(storeName, TitleFontSize, FontWeights.Bold, new Thickness(0, 0, 0, 2)));
        root.Children.Add(MakeCentered("Sales Receipt", BodyFontSize, FontWeights.Normal, new Thickness(0, 0, 0, 4)));
        if (isVoided)
            root.Children.Add(MakeCentered("VOIDED", BodyFontSize, FontWeights.Bold, new Thickness(0, 0, 0, 4), VoidBrush));
        root.Children.Add(MakeRule());
        root.Children.Add(MakeMeta("Invoice", invoiceNo));
        root.Children.Add(MakeMeta("Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
        root.Children.Add(MakeMeta("Cashier", cashierName));
        root.Children.Add(MakeMeta("Order", string.IsNullOrWhiteSpace(orderType) ? "Pickup" : orderType));
        if (string.Equals(orderType, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(customerName))
                root.Children.Add(MakeMeta("Customer", customerName));
            if (!string.IsNullOrWhiteSpace(contactNumber))
                root.Children.Add(MakeMeta("Contact", contactNumber));
            if (!string.IsNullOrWhiteSpace(deliveryAddress))
                root.Children.Add(MakeMeta("Address", deliveryAddress));
        }
        root.Children.Add(MakeRule());
        root.Children.Add(BuildItemsGrid(items));
        root.Children.Add(MakeRule());
        root.Children.Add(BuildTotalsStack(subtotal, taxAmount, itemDiscountAmount, storeDiscountAmount, totalDue, cashTendered, changeAmount));
        root.Children.Add(MakeRule());
        root.Children.Add(MakeCentered(
            string.IsNullOrWhiteSpace(footer) ? "Thank you for shopping with us!" : footer,
            BodyFontSize,
            FontWeights.Normal,
            new Thickness(0, 2, 0, 0)));

        return card;
    }

    private static TextBlock MakeCentered(string text, double fontSize, FontWeight weight, Thickness margin, Brush? foreground = null) => new()
    {
        Text = WrapCentered(text, PrintableWidthDip, fontSize, weight),
        FontFamily = ReceiptFont,
        FontSize = fontSize,
        FontWeight = weight,
        Foreground = foreground ?? Brushes.Black,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.NoWrap,
        LineHeight = fontSize + 4,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = margin
    };

    private static TextBlock MakeMeta(string label, string value) => new()
    {
        Text = WrapToWidth($"{label}: {value}", PrintableWidthDip, BodyFontSize, FontWeights.Normal),
        FontFamily = ReceiptFont,
        FontSize = BodyFontSize,
        TextWrapping = TextWrapping.NoWrap,
        TextAlignment = TextAlignment.Left,
        LineHeight = LineSpacing,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        Margin = new Thickness(0, 1, 0, 1)
    };

    private static Border MakeRule() => new()
    {
        Height = 2,
        Background = RuleBrush,
        Margin = new Thickness(0, 5, 0, 5),
        SnapsToDevicePixels = true
    };

    private static Grid BuildItemsGrid(IReadOnlyList<CartItem> items)
    {
        var qtyWidth = Math.Max(
            MeasureTextWidth("Qty", FontWeights.SemiBold),
            Widest(items.Select(item => FormatQuantity(item.Quantity)), FontWeights.Normal));
        var totalWidth = Math.Max(
            MeasureTextWidth("Total", FontWeights.SemiBold),
            Widest(items.Select(item => $"₱{item.LineTotal:N2}"), FontWeights.Normal));
        qtyWidth += ColumnSlack;
        totalWidth += ColumnSlack;

        var gap = ColumnGap;
        var itemWidth = PrintableWidthDip - qtyWidth - totalWidth - (gap * 2);
        if (itemWidth < MinimumItemColumn)
        {
            gap = 4;
            itemWidth = PrintableWidthDip - qtyWidth - totalWidth - (gap * 2);
        }

        // Currency keeps its measured width. The name uses whatever space remains.
        if (itemWidth < 1)
            itemWidth = 1;

        var grid = new Grid { Width = PrintableWidthDip };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(itemWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(qtyWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(totalWidth) });

        var row = 0;
            AddItemRow(grid, row++, "Item", "Qty", "Total", FontWeights.SemiBold, Math.Max(1, itemWidth - 1));
        foreach (var item in items)
        {
            AddItemRow(
                grid,
                row++,
                item.ProductName,
                FormatQuantity(item.Quantity),
                $"₱{item.LineTotal:N2}",
                FontWeights.Normal,
                Math.Max(1, itemWidth - 1));
        }

        return grid;
    }

    private static void AddItemRow(Grid grid, int rowIndex, string name, string qty, string total, FontWeight weight, double itemWidth)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddGridCell(grid, rowIndex, 0, WrapToWidth(name, itemWidth, BodyFontSize, weight), weight, TextAlignment.Left, TextWrapping.NoWrap);
        AddGridCell(grid, rowIndex, 2, qty, weight, TextAlignment.Right, TextWrapping.NoWrap);
        AddGridCell(grid, rowIndex, 4, total, weight, TextAlignment.Right, TextWrapping.NoWrap);
    }

    private static void AddGridCell(
        Grid grid,
        int row,
        int column,
        string text,
        FontWeight weight,
        TextAlignment align,
        TextWrapping wrapping)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = ReceiptFont,
            FontSize = BodyFontSize,
            FontWeight = weight,
            TextAlignment = align,
            TextWrapping = wrapping,
            LineHeight = LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 1)
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private static UIElement BuildTotalsStack(
        decimal subtotal,
        decimal taxAmount,
        decimal itemDiscountAmount,
        decimal storeDiscountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount)
    {
        var panel = new StackPanel { Width = PrintableWidthDip };
        var displaySubtotal = itemDiscountAmount > 0 ? subtotal + itemDiscountAmount : subtotal;
        panel.Children.Add(MakeSummaryRow("Subtotal", $"₱{displaySubtotal:N2}"));

        if (itemDiscountAmount > 0)
            panel.Children.Add(MakeSummaryRow("Item Discounts", $"−₱{itemDiscountAmount:N2}", MutedBrush));

        if (storeDiscountAmount > 0)
            panel.Children.Add(MakeSummaryRow("Store Discount", $"−₱{storeDiscountAmount:N2}", MutedBrush));

        panel.Children.Add(MakeSummaryRow("VAT", $"₱{taxAmount:N2}"));
        panel.Children.Add(MakeSummaryRow("TOTAL DUE", $"₱{totalDue:N2}", bold: true));
        panel.Children.Add(MakeSummaryRow("Cash", $"₱{cashTendered:N2}"));
        panel.Children.Add(MakeSummaryRow("Change", $"₱{changeAmount:N2}"));
        return panel;
    }

    private static Grid MakeSummaryRow(string label, string amount, Brush? foreground = null, bool bold = false)
    {
        var weight = bold ? FontWeights.Bold : FontWeights.Normal;
        var grid = new Grid { Width = PrintableWidthDip, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontFamily = ReceiptFont,
            FontSize = BodyFontSize,
            FontWeight = weight,
            Foreground = foreground ?? Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var amountBlock = new TextBlock
        {
            Text = amount,
            FontFamily = ReceiptFont,
            FontSize = BodyFontSize,
            FontWeight = weight,
            Foreground = foreground ?? Brushes.Black,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            LineHeight = LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        Grid.SetColumn(amountBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(amountBlock);
        return grid;
    }

    private static double Widest(IEnumerable<string> samples, FontWeight weight)
    {
        var widest = 0.0;
        foreach (var sample in samples)
            widest = Math.Max(widest, MeasureTextWidth(sample, weight));
        return widest;
    }

    /// <summary>
    /// Picks the line break that keeps both lines closest in width, then repeats for whatever remains.
    /// </summary>
    private static string WrapCentered(string text, double maxWidth, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrEmpty(text) || MeasureTextWidth(text, fontSize, weight) <= maxWidth)
            return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var best = -1;
        var bestGap = double.MaxValue;
        for (var i = 1; i < words.Length; i++)
        {
            var left = string.Join(' ', words[..i]);
            var right = string.Join(' ', words[i..]);
            var leftWidth = MeasureTextWidth(left, fontSize, weight);
            var rightWidth = MeasureTextWidth(right, fontSize, weight);
            if (leftWidth > maxWidth || rightWidth > maxWidth)
                continue;

            var gap = Math.Abs(leftWidth - rightWidth);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = i;
            }
        }

        if (best < 0)
            return WrapToWidth(text, maxWidth, fontSize, weight);

        var first = string.Join(' ', words[..best]);
        var rest = WrapCentered(string.Join(' ', words[best..]), maxWidth, fontSize, weight);
        return first + "\n" + rest;
    }

    /// <summary>
    /// Breaks text on spaces so a word stays intact unless that single word is wider than the column.
    /// </summary>
    private static string WrapToWidth(string text, double maxWidth, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrEmpty(text) || MeasureTextWidth(text, fontSize, weight) <= maxWidth)
            return text;

        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (MeasureTextWidth(candidate, fontSize, weight) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
                lines.Add(current);

            if (MeasureTextWidth(word, fontSize, weight) <= maxWidth)
            {
                current = word;
                continue;
            }

            current = "";
            var chunk = "";
            foreach (var character in word)
            {
                var next = chunk + character;
                if (chunk.Length > 0 && MeasureTextWidth(next, fontSize, weight) > maxWidth)
                {
                    lines.Add(chunk);
                    chunk = character.ToString();
                }
                else
                    chunk = next;
            }
            current = chunk;
        }

        if (current.Length > 0)
            lines.Add(current);

        return string.Join('\n', lines);
    }

    private static double MeasureTextWidth(string text, FontWeight weight) =>
        MeasureTextWidth(text, BodyFontSize, weight);

    private static double MeasureTextWidth(string text, double fontSize, FontWeight weight)
    {
        var typeface = new Typeface(ReceiptFont, FontStyles.Normal, weight, FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            1.0);
        return Math.Ceiling(formatted.Width);
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity) ? quantity.ToString("N0") : quantity.ToString("N2");

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static class RawPrint
    {
        public static void Send(string printerName, string documentName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var info = new DocInfo
                {
                    DocumentName = documentName,
                    DataType = "RAW"
                };
                if (!StartDocPrinter(printer, 1, ref info))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    if (!StartPagePrinter(printer))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var count = Math.Min(16384, bytes.Length - offset);
                        var chunk = new byte[count];
                        Buffer.BlockCopy(bytes, offset, chunk, 0, count);
                        if (!WritePrinter(printer, chunk, count, out var written) || written != count)
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        offset += count;
                    }

                    EndPagePrinter(printer);
                }
                finally
                {
                    EndDocPrinter(printer);
                }
            }
            finally
            {
                ClosePrinter(printer);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DocumentName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? OutputFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DataType;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool StartDocPrinter(IntPtr printer, int level, ref DocInfo info);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr printer, byte[] buffer, int count, out int written);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr printer);
    }
}
