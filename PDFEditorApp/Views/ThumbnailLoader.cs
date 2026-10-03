using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views;

/// <summary>
/// Render thumbnail lười cho danh sách trang (panel Trang, màn hình Sắp xếp trang): item hiện ra mới
/// xếp hàng, item mới nhất ưu tiên trước, một vòng render nền. Xem docs/viewer.md.
/// </summary>
public sealed class ThumbnailLoader(PdfService pdf, Visual dpiSource, double width)
{
    private readonly Stack<ThumbnailItem> _queue = new();
    private bool _running;
    private int _generation;

    public ObservableCollection<ThumbnailItem> Items { get; private set; } = [];

    public double Width { get; } = width;

    /// <summary>Dựng lại danh sách theo kích thước trang hiện tại.</summary>
    public async Task RebuildAsync()
    {
        _generation++;
        _queue.Clear();
        var sizes = pdf.IsOpen ? await Task.Run(pdf.GetPageSizes) : [];
        Items = new ObservableCollection<ThumbnailItem>(sizes.Select((s, i) =>
            new ThumbnailItem(i, Width, Math.Clamp(Width * s.Height / Math.Max(1, s.Width), 24, Width * 2.2), s.Width)));
    }

    public void Request(ThumbnailItem item)
    {
        if (item.Image is not null)
        {
            return;
        }

        _queue.Push(item);
        if (!_running)
        {
            _ = LoopAsync();
        }
    }

    public void Invalidate(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items[index].Image = null;
            Request(Items[index]);
        }
    }

    private async Task LoopAsync()
    {
        _running = true;
        try
        {
            while (_queue.TryPop(out var item))
            {
                var generation = _generation;
                if (item.Image is not null || !IsCurrent(item))
                {
                    continue;
                }

                var pixelsPerPoint = item.Width * VisualTreeHelper.GetDpi(dpiSource).PixelsPerDip / Math.Max(1, item.PageWidth);
                try
                {
                    var bitmap = await Task.Run(() => BitmapHelper.ToBitmapSource(pdf.RenderPage(item.Index, pixelsPerPoint, forScreen: false)));
                    if (generation == _generation && IsCurrent(item))
                    {
                        item.Image = bitmap;
                    }
                }
                catch (PdfException)
                {
                    // Trang bị xóa / tài liệu đóng: bỏ qua.
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    private bool IsCurrent(ThumbnailItem item) =>
        item.Index < Items.Count && ReferenceEquals(Items[item.Index], item);

    /// <summary>Lấy ThumbnailItem từ phần tử trong DataTemplate (sự kiện Loaded).</summary>
    public static ThumbnailItem? FromElement(object sender) => (sender as FrameworkElement)?.DataContext as ThumbnailItem;
}
