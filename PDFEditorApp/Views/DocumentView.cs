using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using PDFEditorApp.Views.Tools;

namespace PDFEditorApp.Views;

/// <summary>Cách bố trí trang.</summary>
public enum ViewLayout
{
    Continuous,
    SinglePage,
    TwoPage,
}

/// <summary>Chế độ zoom.</summary>
public enum ZoomMode
{
    Custom,
    FitWidth,
    FitPage,
}

/// <summary>Điểm trên một trang (toạ độ view, point).</summary>
public sealed record PageHit(int Page, PointD View, PageView PageView);

/// <summary>
/// Vùng xem tài liệu: cuộn liên tục / một trang / hai trang, ảo hóa (chỉ tạo trang đang thấy), render nền
/// ưu tiên trang đang thấy, zoom neo theo chuột, chế độ đọc ban đêm. Chuột / phím chuyển cho công cụ
/// đang chọn (<see cref="Tool"/>). Xem docs/viewer.md.
/// </summary>
public sealed class DocumentView : UserControl
{
    private const double Margin0 = 20;
    private const double Gap = 14;
    private const double PointsToDip = 96.0 / 72.0;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8;

    private static readonly double[] ZoomSteps = [0.1, 0.25, 0.33, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5, 6, 8];

    private readonly ScrollViewer _scroller;
    private readonly Canvas _host;
    private readonly Dictionary<int, PageView> _views = [];
    private readonly List<int> _renderQueue = [];

    private PdfService? _pdf;
    private List<Size> _sizes = [];
    private Rect[] _rects = [];
    private int[] _versions = [];
    private bool _renderLoopRunning;
    private int _currentPage;
    private double _zoom = 1;
    private ZoomMode _zoomMode = ZoomMode.FitWidth;
    private ViewLayout _layout = ViewLayout.Continuous;
    private bool _nightMode;
    private Tool? _tool;
    private bool _layoutPending;

    public DocumentView()
    {
        Focusable = false;
        _host = new Canvas { Background = Brushes.Transparent };
        _scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            Focusable = true,
            FocusVisualStyle = null,
            PanningMode = PanningMode.Both,
            Content = _host,
        };
        _scroller.SetResourceReference(BackgroundProperty, "ViewerBackgroundBrush");
        Content = _scroller;

        _scroller.ScrollChanged += (_, e) =>
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
            {
                ScheduleLayout();
            }

            UpdateVisiblePages();
        };
        _scroller.PreviewMouseWheel += OnPreviewMouseWheel;
        // Chế độ Vừa trang / Vừa chiều rộng phải tính lại mỗi khi vùng xem đổi kích thước
        // (kể cả lần đo đầu tiên sau khi tab hiện ra).
        SizeChanged += (_, _) => ScheduleLayout();
        _host.MouseLeftButtonDown += (_, e) => ForwardMouse(e, (t, h, p) => t.OnMouseDown(this, h, p, e));
        _host.MouseMove += (_, e) => ForwardMouse(e, (t, h, p) => t.OnMouseMove(this, h, p, e));
        _host.MouseLeftButtonUp += (_, e) => ForwardMouse(e, (t, h, p) => t.OnMouseUp(this, h, p, e));
        _host.MouseLeave += (_, _) => _tool?.OnMouseLeave(this);
        PreviewKeyDown += (_, e) =>
        {
            // Đang gõ trong ô nhập đặt trên trang (ô sửa chữ trực tiếp, ô điền form) → phím thuộc về ô đó:
            // Shift+Enter xuống dòng, mũi tên di chuyển con trỏ, Delete xóa ký tự. PreviewKeyDown đi từ
            // ngoài vào nên nếu không chặn ở đây, công cụ sẽ nuốt phím trước khi ô nhập thấy.
            if (e.OriginalSource is TextBoxBase or PasswordBox)
            {
                return;
            }

            if (_tool?.OnKeyDown(this, e) == true)
            {
                e.Handled = true;
            }
        };
        ThemeService.ThemeChanged += (_, _) => _scroller.SetResourceReference(BackgroundProperty, "ViewerBackgroundBrush");
    }

    /// <summary>Trang hiện tại đổi (trang chiếm nhiều diện tích nhất trong khung nhìn).</summary>
    public event EventHandler<int>? CurrentPageChanged;

    public event EventHandler? ZoomChanged;

    /// <summary>Gọi khi một trang được tạo / đổi tỉ lệ → vẽ lại lớp phủ (tìm kiếm, form, công cụ…).</summary>
    public event EventHandler<PageView>? PageDecorate;

    public int PageCount => _sizes.Count;

    public int CurrentPage => _currentPage;

    public double Zoom => _zoom;

    public ZoomMode ZoomMode => _zoomMode;

    public ViewLayout Layout => _layout;

    public bool NightMode
    {
        get => _nightMode;
        set
        {
            if (_nightMode == value)
            {
                return;
            }

            _nightMode = value;
            InvalidateAll();
        }
    }

    /// <summary>DIP trên mỗi point ở mức zoom hiện tại.</summary>
    public double Scale => _zoom * PointsToDip;

    public IEnumerable<PageView> RealizedPages => _views.Values;

    public Tool? Tool
    {
        get => _tool;
        set
        {
            if (ReferenceEquals(_tool, value))
            {
                return;
            }

            _tool?.Deactivate(this);
            _tool = value;
            _host.Cursor = value?.Cursor;
            value?.Activate(this);
            foreach (var pv in _views.Values)
            {
                pv.ToolLayer.Children.Clear();
                value?.OnPageDecorate(this, pv);
            }
        }
    }

    public void SetCursor(Cursor? cursor) => _host.Cursor = cursor ?? _tool?.Cursor;

    /// <summary>Gắn tài liệu (kích thước hiển thị từng trang theo point).</summary>
    public void SetDocument(PdfService pdf, IReadOnlyList<(double Width, double Height)> sizes, int page = 0)
    {
        _pdf = pdf;
        ResetPages(sizes);
        _currentPage = Math.Clamp(page, 0, Math.Max(0, _sizes.Count - 1));
        RelayoutNow();
        GoToPage(_currentPage);
        ScheduleLayout();
    }

    /// <summary>Số trang / kích thước đổi (xoay, xóa, chèn…) → dựng lại bố cục, render lại.</summary>
    public void UpdatePages(IReadOnlyList<(double Width, double Height)> sizes, int currentPage)
    {
        var offset = _scroller.VerticalOffset;
        ResetPages(sizes);
        _currentPage = Math.Clamp(currentPage, 0, Math.Max(0, _sizes.Count - 1));
        RelayoutNow();
        if (_layout == ViewLayout.SinglePage)
        {
            GoToPage(_currentPage);
        }
        else
        {
            _scroller.ScrollToVerticalOffset(offset);
            UpdateVisiblePages();
        }
    }

    public void Clear()
    {
        _pdf = null;
        ResetPages([]);
        RelayoutNow();
    }

    /// <summary>Trang đã sửa → render lại ảnh và lớp phủ.</summary>
    public void InvalidatePage(int index)
    {
        if (index < 0 || index >= _versions.Length)
        {
            return;
        }

        _versions[index]++;
        if (_views.TryGetValue(index, out var pv))
        {
            Decorate(pv);
            Enqueue(index);
        }
    }

    public void InvalidateAll()
    {
        for (var i = 0; i < _versions.Length; i++)
        {
            _versions[i]++;
        }

        foreach (var pv in _views.Values)
        {
            Decorate(pv);
            Enqueue(pv.PageIndex);
        }
    }

    /// <summary>Vẽ lại lớp phủ của mọi trang đang hiện (không render lại ảnh).</summary>
    public void RedecorateAll()
    {
        foreach (var pv in _views.Values)
        {
            Decorate(pv);
        }
    }

    public PageView? GetPageView(int index) => _views.GetValueOrDefault(index);

    public void FocusView() => _scroller.Focus();

    public void CaptureMouseOnHost() => _host.CaptureMouse();

    public void ReleaseMouseOnHost()
    {
        if (_host.IsMouseCaptured)
        {
            _host.ReleaseMouseCapture();
        }
    }

    /// <summary>Điểm (DIP của vùng cuộn) → điểm view (point) trên một trang cụ thể (có thể nằm ngoài trang).</summary>
    public PointD ToPageView(int page, Point hostPoint)
    {
        var r = _rects[page];
        return new PointD((hostPoint.X - r.X) / Scale, (hostPoint.Y - r.Y) / Scale);
    }

    /// <summary>Kéo màn hình (công cụ bàn tay).</summary>
    public void ScrollBy(double dx, double dy)
    {
        _scroller.ScrollToHorizontalOffset(_scroller.HorizontalOffset + dx);
        _scroller.ScrollToVerticalOffset(_scroller.VerticalOffset + dy);
    }

    // ------------------------------------------------------------------
    // Điều hướng & zoom
    // ------------------------------------------------------------------

    public void GoToPage(int index, double viewY = 0)
    {
        if (_sizes.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, _sizes.Count - 1);
        if (_layout == ViewLayout.SinglePage && index != _currentPage)
        {
            SetCurrentPage(index);
            RelayoutNow();
        }

        var r = _rects[index];
        _scroller.ScrollToVerticalOffset(Math.Max(0, r.Y - Margin0 + (viewY * Scale)));
        SetCurrentPage(index);
        UpdateVisiblePages();
    }

    /// <summary>Cuộn để vùng (view, point) của trang nằm trong khung nhìn.</summary>
    public void ScrollIntoView(int page, RectD view)
    {
        if (page < 0 || page >= _sizes.Count)
        {
            return;
        }

        if (_layout == ViewLayout.SinglePage && page != _currentPage)
        {
            GoToPage(page);
        }

        var r = _rects[page];
        var top = r.Y + (view.Y * Scale);
        var left = r.X + (view.X * Scale);
        if (top < _scroller.VerticalOffset + 40 || top + (view.Height * Scale) > _scroller.VerticalOffset + _scroller.ViewportHeight - 40)
        {
            _scroller.ScrollToVerticalOffset(Math.Max(0, top - (_scroller.ViewportHeight / 3)));
        }

        if (left < _scroller.HorizontalOffset || left > _scroller.HorizontalOffset + _scroller.ViewportWidth - 40)
        {
            _scroller.ScrollToHorizontalOffset(Math.Max(0, left - 40));
        }
    }

    public void SetLayout(ViewLayout layout)
    {
        if (_layout == layout)
        {
            return;
        }

        var page = _currentPage;
        _layout = layout;
        RelayoutNow();
        GoToPage(page);
    }

    public void SetZoom(ZoomMode mode, double zoom = 1, Point? anchor = null)
    {
        _zoomMode = mode;
        var target = mode == ZoomMode.Custom ? zoom : ComputeFitZoom(mode);
        ApplyZoom(Math.Clamp(target, MinZoom, MaxZoom), anchor);
    }

    public void ZoomStep(int direction, Point? anchor = null)
    {
        var next = direction > 0
            ? ZoomSteps.FirstOrDefault(s => s > _zoom + 0.001, MaxZoom)
            : ZoomSteps.LastOrDefault(s => s < _zoom - 0.001, MinZoom);
        SetZoom(ZoomMode.Custom, next, anchor);
    }

    private void ApplyZoom(double zoom, Point? anchorInViewport)
    {
        if (_sizes.Count == 0)
        {
            _zoom = zoom;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Neo: giữ nguyên điểm của tài liệu dưới chuột (hoặc giữa khung nhìn).
        var anchor = anchorInViewport ?? new Point(_scroller.ViewportWidth / 2, _scroller.ViewportHeight / 2);
        var hostPoint = new Point(anchor.X + _scroller.HorizontalOffset, anchor.Y + _scroller.VerticalOffset);
        var hit = HitTestHost(hostPoint, clampToNearest: true);
        var page = hit?.Page ?? _currentPage;
        var view = hit?.View ?? new PointD(0, 0);

        _zoom = zoom;
        RelayoutNow();
        var r = _rects[page];
        _scroller.ScrollToHorizontalOffset(Math.Max(0, r.X + (view.X * Scale) - anchor.X));
        _scroller.ScrollToVerticalOffset(Math.Max(0, r.Y + (view.Y * Scale) - anchor.Y));
        UpdateVisiblePages();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private double ComputeFitZoom(ZoomMode mode)
    {
        if (_sizes.Count == 0)
        {
            return 1;
        }

        var width = Math.Max(100, _scroller.ActualWidth - SystemParameters.VerticalScrollBarWidth - (2 * Margin0));
        var height = Math.Max(100, _scroller.ActualHeight - (2 * Margin0));
        double rowWidth;
        Size current;
        if (_layout == ViewLayout.TwoPage)
        {
            var first = _currentPage - (_currentPage % 2);
            var w2 = first + 1 < _sizes.Count ? _sizes[first + 1].Width : 0;
            rowWidth = _sizes[first].Width + w2 + (w2 > 0 ? Gap / PointsToDip : 0);
            current = new Size(rowWidth, Math.Max(_sizes[first].Height, first + 1 < _sizes.Count ? _sizes[first + 1].Height : 0));
        }
        else
        {
            // Như Acrobat: vừa theo trang hiện tại (tài liệu lẫn trang dọc / ngang).
            rowWidth = _sizes[_currentPage].Width;
            current = _sizes[_currentPage];
        }

        var fitWidth = width / (rowWidth * PointsToDip);
        return mode == ZoomMode.FitPage
            ? Math.Min(fitWidth, height / (current.Height * PointsToDip))
            : fitWidth;
    }

    // ------------------------------------------------------------------
    // Bố cục & ảo hóa
    // ------------------------------------------------------------------

    private void ResetPages(IReadOnlyList<(double Width, double Height)> sizes)
    {
        foreach (var pv in _views.Values)
        {
            _host.Children.Remove(pv);
        }

        _views.Clear();
        _renderQueue.Clear();
        _sizes = sizes.Select(s => new Size(Math.Max(1, s.Width), Math.Max(1, s.Height))).ToList();
        _rects = new Rect[_sizes.Count];
        var oldVersions = _versions;
        _versions = new int[_sizes.Count];
        for (var i = 0; i < _versions.Length; i++)
        {
            _versions[i] = (i < oldVersions.Length ? oldVersions[i] : 0) + 1;
        }
    }

    private void ScheduleLayout()
    {
        if (_layoutPending)
        {
            return;
        }

        _layoutPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _layoutPending = false;
            if (_zoomMode != ZoomMode.Custom && _sizes.Count > 0)
            {
                var target = Math.Clamp(ComputeFitZoom(_zoomMode), MinZoom, MaxZoom);
                if (Math.Abs(target - _zoom) > 0.002)
                {
                    ApplyZoom(target, null);
                    return;
                }
            }

            RelayoutNow();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void RelayoutNow()
    {
        var scale = Scale;
        var viewportWidth = Math.Max(0, _scroller.ViewportWidth > 0 ? _scroller.ViewportWidth : _scroller.ActualWidth);
        double hostWidth = viewportWidth, y = Margin0;
        if (_sizes.Count == 0)
        {
            _host.Width = viewportWidth;
            _host.Height = 0;
            return;
        }

        switch (_layout)
        {
            case ViewLayout.SinglePage:
                {
                    for (var i = 0; i < _rects.Length; i++)
                    {
                        _rects[i] = Rect.Empty;
                    }

                    var s = _sizes[_currentPage];
                    var w = s.Width * scale;
                    hostWidth = Math.Max(viewportWidth, w + (2 * Margin0));
                    _rects[_currentPage] = new Rect(CenterX(w, viewportWidth), Margin0, w, s.Height * scale);
                    y = Margin0 + (s.Height * scale) + Margin0;
                    break;
                }

            case ViewLayout.TwoPage:
                {
                    var maxRow = 0.0;
                    for (var i = 0; i < _sizes.Count; i += 2)
                    {
                        var rowW = (_sizes[i].Width * scale) + (i + 1 < _sizes.Count ? Gap + (_sizes[i + 1].Width * scale) : 0);
                        maxRow = Math.Max(maxRow, rowW);
                    }

                    hostWidth = Math.Max(viewportWidth, maxRow + (2 * Margin0));
                    for (var i = 0; i < _sizes.Count; i += 2)
                    {
                        var wL = _sizes[i].Width * scale;
                        var hL = _sizes[i].Height * scale;
                        var hasRight = i + 1 < _sizes.Count;
                        var wR = hasRight ? _sizes[i + 1].Width * scale : 0;
                        var hR = hasRight ? _sizes[i + 1].Height * scale : 0;
                        var rowW = wL + (hasRight ? Gap + wR : 0);
                        var x = CenterX(rowW, viewportWidth);
                        _rects[i] = new Rect(x, y, wL, hL);
                        if (hasRight)
                        {
                            _rects[i + 1] = new Rect(x + wL + Gap, y, wR, hR);
                        }

                        y += Math.Max(hL, hR) + Gap;
                    }

                    y += Margin0 - Gap;
                    break;
                }

            default:
                {
                    var maxW = _sizes.Max(s => s.Width) * scale;
                    hostWidth = Math.Max(viewportWidth, maxW + (2 * Margin0));
                    for (var i = 0; i < _sizes.Count; i++)
                    {
                        var w = _sizes[i].Width * scale;
                        var h = _sizes[i].Height * scale;
                        _rects[i] = new Rect(CenterX(w, viewportWidth), y, w, h);
                        y += h + Gap;
                    }

                    y += Margin0 - Gap;
                    break;
                }
        }

        _host.Width = hostWidth;
        _host.Height = y;
        foreach (var pv in _views.Values.ToList())
        {
            var r = _rects[pv.PageIndex];
            if (r.IsEmpty)
            {
                RemoveView(pv);
                continue;
            }

            PlaceView(pv, r);
        }

        UpdateVisiblePages();
    }

    /// <summary>Trang hẹp hơn khung nhìn: căn giữa khung nhìn; rộng hơn: sát lề trái (cuộn ngang).</summary>
    private static double CenterX(double width, double viewportWidth) =>
        width + (2 * Margin0) <= viewportWidth ? (viewportWidth - width) / 2 : Margin0;

    private void PlaceView(PageView pv, Rect r)
    {
        var oldScale = pv.Scale;
        pv.SetScale(Scale, _sizes[pv.PageIndex]);
        Canvas.SetLeft(pv, r.X);
        Canvas.SetTop(pv, r.Y);
        if (Math.Abs(oldScale - pv.Scale) > 1e-6)
        {
            Decorate(pv);
        }

        if (NeedsRender(pv))
        {
            Enqueue(pv.PageIndex);
        }
    }

    private void UpdateVisiblePages()
    {
        if (_sizes.Count == 0)
        {
            return;
        }

        var top = _scroller.VerticalOffset - _scroller.ViewportHeight;
        var bottom = _scroller.VerticalOffset + (2 * _scroller.ViewportHeight);
        var wanted = new HashSet<int>();
        for (var i = 0; i < _rects.Length; i++)
        {
            var r = _rects[i];
            if (!r.IsEmpty && r.Bottom >= top && r.Top <= bottom)
            {
                wanted.Add(i);
            }
        }

        foreach (var pv in _views.Values.Where(v => !wanted.Contains(v.PageIndex)).ToList())
        {
            RemoveView(pv);
        }

        foreach (var i in wanted)
        {
            if (!_views.ContainsKey(i))
            {
                var pv = new PageView(i);
                _views[i] = pv;
                _host.Children.Add(pv);
                pv.SetScale(Scale, _sizes[i]);
                Canvas.SetLeft(pv, _rects[i].X);
                Canvas.SetTop(pv, _rects[i].Y);
                Decorate(pv);
                Enqueue(i);
            }
        }

        if (_layout != ViewLayout.SinglePage)
        {
            UpdateCurrentPage();
        }
    }

    private void RemoveView(PageView pv)
    {
        _views.Remove(pv.PageIndex);
        _host.Children.Remove(pv);
    }

    private void UpdateCurrentPage()
    {
        var viewTop = _scroller.VerticalOffset;
        var viewBottom = viewTop + _scroller.ViewportHeight;
        var best = _currentPage;
        var bestVisible = -1.0;
        for (var i = 0; i < _rects.Length; i++)
        {
            var r = _rects[i];
            if (r.IsEmpty || r.Bottom < viewTop || r.Top > viewBottom)
            {
                continue;
            }

            var visible = Math.Min(r.Bottom, viewBottom) - Math.Max(r.Top, viewTop);
            if (visible > bestVisible + 1)
            {
                bestVisible = visible;
                best = i;
            }
        }

        SetCurrentPage(best);
    }

    private void SetCurrentPage(int page)
    {
        if (page == _currentPage)
        {
            return;
        }

        _currentPage = page;
        CurrentPageChanged?.Invoke(this, page);
    }

    private void Decorate(PageView pv)
    {
        PageDecorate?.Invoke(this, pv);
        pv.ToolLayer.Children.Clear();
        _tool?.OnPageDecorate(this, pv);
    }

    // ------------------------------------------------------------------
    // Render nền
    // ------------------------------------------------------------------

    private double PixelsPerPoint => Scale * VisualTreeHelper.GetDpi(this).PixelsPerDip;

    private bool NeedsRender(PageView pv) =>
        pv.BitmapVersion != _versions[pv.PageIndex] || Math.Abs(pv.BitmapPixelsPerPoint - PixelsPerPoint) > PixelsPerPoint * 0.02;

    private void Enqueue(int page)
    {
        if (!_renderQueue.Contains(page))
        {
            _renderQueue.Add(page);
        }

        if (!_renderLoopRunning)
        {
            _ = RenderLoopAsync();
        }
    }

    private async Task RenderLoopAsync()
    {
        _renderLoopRunning = true;
        try
        {
            while (_renderQueue.Count > 0 && _pdf is { } pdf)
            {
                // Ưu tiên trang gần trang hiện tại nhất.
                var page = _renderQueue.OrderBy(p => Math.Abs(p - _currentPage)).First();
                _renderQueue.Remove(page);
                if (!_views.TryGetValue(page, out var pv) || !NeedsRender(pv))
                {
                    continue;
                }

                var pixelsPerPoint = PixelsPerPoint;
                var version = _versions[page];
                var dpi = 96 * VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var night = _nightMode;
                try
                {
                    var bitmap = await Task.Run(() =>
                    {
                        var image = pdf.RenderPage(page, pixelsPerPoint);
                        if (night)
                        {
                            Invert(image);
                        }

                        return BitmapHelper.ToBitmapSource(image, dpi);
                    });
                    if (_views.TryGetValue(page, out var current) && ReferenceEquals(current, pv) &&
                        version == _versions[page] && night == _nightMode)
                    {
                        pv.SetBitmap(bitmap, pixelsPerPoint, version);
                        pv.Background = night ? Brushes.Black : Brushes.White;
                    }
                }
                catch (PdfException)
                {
                    // Tài liệu đóng / trang bị xóa trong lúc render.
                }
            }
        }
        finally
        {
            _renderLoopRunning = false;
        }
    }

    /// <summary>Chế độ đêm: đảo màu nhưng giữ sắc (đảo độ sáng gần đúng).</summary>
    private static void Invert(RenderedPage image)
    {
        var p = image.Pixels;
        for (var i = 0; i < p.Length; i += 4)
        {
            p[i] = (byte)(255 - p[i]);
            p[i + 1] = (byte)(255 - p[i + 1]);
            p[i + 2] = (byte)(255 - p[i + 2]);
        }
    }

    // ------------------------------------------------------------------
    // Chuột
    // ------------------------------------------------------------------

    /// <summary>Trang dưới một điểm của vùng cuộn; <paramref name="clampToNearest"/> = lấy trang gần nhất.</summary>
    public PageHit? HitTestHost(Point hostPoint, bool clampToNearest = false)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _rects.Length; i++)
        {
            var r = _rects[i];
            if (r.IsEmpty)
            {
                continue;
            }

            if (r.Contains(hostPoint))
            {
                best = i;
                break;
            }

            if (clampToNearest)
            {
                var dy = hostPoint.Y < r.Top ? r.Top - hostPoint.Y : hostPoint.Y > r.Bottom ? hostPoint.Y - r.Bottom : 0;
                var dx = hostPoint.X < r.Left ? r.Left - hostPoint.X : hostPoint.X > r.Right ? hostPoint.X - r.Right : 0;
                if (dx + dy < bestDistance)
                {
                    bestDistance = dx + dy;
                    best = i;
                }
            }
        }

        if (best < 0)
        {
            return null;
        }

        var view = ToPageView(best, hostPoint);
        var pv = _views.GetValueOrDefault(best);
        return pv is null ? null : new PageHit(best, view, pv);
    }

    private void ForwardMouse(MouseEventArgs e, Action<Tool, PageHit?, Point> action)
    {
        if (_tool is null || _pdf is null)
        {
            return;
        }

        var p = e.GetPosition(_host);
        action(_tool, HitTestHost(p), p);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_sizes.Count == 0)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomStep(e.Delta > 0 ? 1 : -1, e.GetPosition(_scroller));
            e.Handled = true;
            return;
        }

        if (_layout != ViewLayout.SinglePage)
        {
            return;
        }

        // Một trang: lăn quá cuối / đầu → sang trang kế / trước.
        if (e.Delta < 0 && _scroller.VerticalOffset >= _scroller.ScrollableHeight - 0.5 && _currentPage < _sizes.Count - 1)
        {
            GoToPage(_currentPage + 1);
            e.Handled = true;
        }
        else if (e.Delta > 0 && _scroller.VerticalOffset <= 0.5 && _currentPage > 0)
        {
            GoToPage(_currentPage - 1);
            _scroller.ScrollToBottom();
            e.Handled = true;
        }
    }
}
