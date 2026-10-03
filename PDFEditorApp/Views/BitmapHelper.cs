using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFEditorApp.Models;

namespace PDFEditorApp.Views;

/// <summary>Chuyển ảnh render (byte BGRA từ Services) thành ảnh WPF.</summary>
public static class BitmapHelper
{
    /// <summary>Tạo BitmapSource đã Freeze (dùng được từ luồng nền).</summary>
    public static BitmapSource ToBitmapSource(RenderedPage page, double dpi = 96)
    {
        ArgumentNullException.ThrowIfNull(page);
        var bitmap = BitmapSource.Create(page.Width, page.Height, dpi, dpi, PixelFormats.Bgr32, null, page.Pixels, page.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static Color ToColor(RgbColor c) => Color.FromRgb(c.R, c.G, c.B);

    public static RgbColor ToRgb(Color c) => new(c.R, c.G, c.B);
}
