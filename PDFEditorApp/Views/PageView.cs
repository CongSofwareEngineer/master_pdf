using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFEditorApp.Models;

namespace PDFEditorApp.Views;

/// <summary>
/// Một trang trong <see cref="DocumentView"/>: ảnh render + các lớp phủ. Toạ độ trong các lớp phủ là
/// DIP của trang = toạ độ view (point) × <see cref="Scale"/>. Xem docs/viewer.md.
/// </summary>
public sealed class PageView : Grid
{
    private readonly Image _image;

    public PageView(int pageIndex)
    {
        PageIndex = pageIndex;
        // Không dùng DropShadowEffect: hiệu ứng trên ảnh lớn làm cuộn / zoom bị giật.
        Background = Brushes.White;
        _image = new Image { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        Children.Add(_image);

        // Viền mảnh để thấy mép trang trên nền sáng (thay cho bóng đổ tốn hiệu năng).
        var frame = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false, Margin = new Thickness(-1) };
        frame.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        Children.Add(frame);
        Children.Add(HighlightLayer);
        Children.Add(FormLayer);
        Children.Add(ToolLayer);
        ClipToBounds = false;
    }

    public int PageIndex { get; }

    /// <summary>DIP trên mỗi point của trang.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>Pixel / point của ảnh đang hiển thị (0 = chưa có ảnh).</summary>
    public double BitmapPixelsPerPoint { get; private set; }

    public int BitmapVersion { get; private set; } = -1;

    /// <summary>Tô sáng: kết quả tìm, vùng chữ đang chọn, nhận xét đang chọn, vùng che chờ áp dụng.</summary>
    public Canvas HighlightLayer { get; } = new() { IsHitTestVisible = false };

    /// <summary>Control điền form đặt trùng vị trí ô.</summary>
    public Canvas FormLayer { get; } = new();

    /// <summary>Hình xem trước / ô sửa của công cụ đang dùng.</summary>
    public Canvas ToolLayer { get; } = new() { IsHitTestVisible = true };

    public void SetScale(double scale, Size pagePoints)
    {
        Scale = scale;
        Width = pagePoints.Width * scale;
        Height = pagePoints.Height * scale;
    }

    public void SetBitmap(BitmapSource bitmap, double pixelsPerPoint, int version)
    {
        _image.Source = bitmap;
        BitmapPixelsPerPoint = pixelsPerPoint;
        BitmapVersion = version;
    }

    public void ClearBitmap()
    {
        _image.Source = null;
        BitmapPixelsPerPoint = 0;
        BitmapVersion = -1;
    }

    /// <summary>Đổi khung hệ view (point) sang DIP của trang.</summary>
    public Rect ToLocal(RectD view) => new(view.X * Scale, view.Y * Scale, Math.Max(0, view.Width * Scale), Math.Max(0, view.Height * Scale));

    public Point ToLocal(PointD view) => new(view.X * Scale, view.Y * Scale);
}
