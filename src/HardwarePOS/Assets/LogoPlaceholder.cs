using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;

namespace HardwarePOS;

/// <summary>Creates a simple logo placeholder asset at runtime if needed.</summary>
public static class LogoPlaceholder
{
    public static ImageSource Create()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(13, 71, 161)), null, new Rect(0, 0, 128, 128));
            var text = new FormattedText(
                "HW",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                48,
                Brushes.White,
                1.25);
            dc.DrawText(text, new Point(28, 36));
        }

        var bmp = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }
}
