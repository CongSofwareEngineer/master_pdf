using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Tools;

/// <summary>Loại hình vẽ của <see cref="ShapeTool"/>.</summary>
public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Line,
    Arrow,
}

internal static class ToolVisuals
{
    public static SolidColorBrush Brush(RgbColor c, double opacity = 1)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Round(opacity * 255), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    public static Rectangle DashedRect(Brush stroke, Brush? fill = null) => new()
    {
        Stroke = stroke,
        Fill = fill ?? Brushes.Transparent,
        StrokeThickness = 1.5,
        StrokeDashArray = [4, 3],
        IsHitTestVisible = false,
    };

    public static void Place(FrameworkElement e, Rect r)
    {
        Canvas.SetLeft(e, r.X);
        Canvas.SetTop(e, r.Y);
        e.Width = Math.Max(1, r.Width);
        e.Height = Math.Max(1, r.Height);
    }

    public static RectD Normalize(PointD a, PointD b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

/// <summary>Bút vẽ tự do (Ink). Xem docs/annotations.md.</summary>
public sealed class InkTool(IToolHost host) : Tool(host)
{
    private readonly List<PointD> _points = [];
    private Polyline? _line;
    private int _page = -1;

    public override Cursor? Cursor => Cursors.Pen;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        _page = hit.Page;
        _points.Clear();
        _points.Add(hit.View);
        _line = new Polyline
        {
            Stroke = ToolVisuals.Brush(Host.Options.InkColor, Host.Options.Opacity),
            StrokeThickness = Math.Max(1, Host.Options.InkWidth * hit.PageView.Scale),
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
        };
        _line.Points.Add(hit.PageView.ToLocal(hit.View));
        hit.PageView.ToolLayer.Children.Add(_line);
        view.CaptureMouseOnHost();
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_page < 0 || _line is null || view.GetPageView(_page) is not { } pv)
        {
            return;
        }

        var v = view.ToPageView(_page, hostPoint);
        var last = _points[^1];
        if (Math.Abs(v.X - last.X) + Math.Abs(v.Y - last.Y) < 0.6)
        {
            return;
        }

        _points.Add(v);
        _line.Points.Add(pv.ToLocal(v));
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.ReleaseMouseOnHost();
        if (_page < 0)
        {
            return;
        }

        var page = _page;
        var points = _points.ToList();
        var o = Host.Options;
        var (color, width, opacity) = (o.InkColor, o.InkWidth, o.Opacity);
        _page = -1;
        _ = Host.EditAsync(page, () => Host.Pdf.AddInk(page, [points], color, width, opacity), "Status_AnnotationAdded");
    }
}

/// <summary>Hình chữ nhật / elip / đường thẳng / mũi tên: kéo để vẽ.</summary>
public sealed class ShapeTool(IToolHost host, ShapeKind kind) : Tool(host)
{
    private int _page = -1;
    private PointD _start;
    private Shape? _preview;
    private Polyline? _head;

    public ShapeKind Kind { get; } = kind;

    public override Cursor? Cursor => Cursors.Cross;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        _page = hit.Page;
        _start = hit.View;
        var stroke = ToolVisuals.Brush(Host.Options.InkColor);
        var thickness = Math.Max(1, Host.Options.InkWidth * hit.PageView.Scale);
        _preview = Kind switch
        {
            ShapeKind.Rectangle => new Rectangle(),
            ShapeKind.Ellipse => new Ellipse(),
            _ => new Line(),
        };
        _preview.Stroke = stroke;
        _preview.StrokeThickness = thickness;
        _preview.IsHitTestVisible = false;
        if (Host.Options.FillShape && Kind is ShapeKind.Rectangle or ShapeKind.Ellipse)
        {
            _preview.Fill = ToolVisuals.Brush(Host.Options.MarkupColor);
        }

        hit.PageView.ToolLayer.Children.Add(_preview);
        if (Kind == ShapeKind.Arrow)
        {
            _head = new Polyline { Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false };
            hit.PageView.ToolLayer.Children.Add(_head);
        }

        view.CaptureMouseOnHost();
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_page < 0 || _preview is null || view.GetPageView(_page) is not { } pv)
        {
            return;
        }

        var end = view.ToPageView(_page, hostPoint);
        if (_preview is Line line)
        {
            var a = pv.ToLocal(_start);
            var b = pv.ToLocal(end);
            (line.X1, line.Y1, line.X2, line.Y2) = (a.X, a.Y, b.X, b.Y);
            if (_head is not null)
            {
                var angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
                var size = Math.Max(8, Host.Options.InkWidth * 4) * pv.Scale;
                _head.Points = [
                    new Point(b.X - (size * Math.Cos(angle - (Math.PI / 7))), b.Y - (size * Math.Sin(angle - (Math.PI / 7)))),
                    b,
                    new Point(b.X - (size * Math.Cos(angle + (Math.PI / 7))), b.Y - (size * Math.Sin(angle + (Math.PI / 7)))),
                ];
            }
        }
        else
        {
            ToolVisuals.Place(_preview, pv.ToLocal(ToolVisuals.Normalize(_start, end)));
        }
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.ReleaseMouseOnHost();
        if (_page < 0)
        {
            return;
        }

        var page = _page;
        _page = -1;
        var end = view.ToPageView(page, hostPoint);
        var rect = ToolVisuals.Normalize(_start, end);
        if (rect.Width < 3 && rect.Height < 3)
        {
            view.GetPageView(page)?.ToolLayer.Children.Clear();
            return;
        }

        var start = _start;
        var o = Host.Options;
        var (stroke, width) = (o.InkColor, o.InkWidth);
        RgbColor? fill = o.FillShape ? o.MarkupColor : null;
        Func<int> edit = Kind switch
        {
            ShapeKind.Rectangle => () => Host.Pdf.AddShape(page, AnnotationKind.Square, rect, stroke, fill, width),
            ShapeKind.Ellipse => () => Host.Pdf.AddShape(page, AnnotationKind.Circle, rect, stroke, fill, width),
            ShapeKind.Line => () => Host.Pdf.AddLine(page, start, end, false, stroke, width),
            _ => () => Host.Pdf.AddLine(page, start, end, true, stroke, width),
        };
        _ = Host.EditAsync(page, edit, "Status_AnnotationAdded");
    }
}

/// <summary>Ghi chú (sticky note): bấm lên trang rồi nhập nội dung.</summary>
public sealed class NoteTool(IToolHost host) : Tool(host)
{
    public override Cursor? Cursor => Cursors.Cross;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        var text = TextInputDialog.Ask(Host.OwnerWindow, Loc.Get("Note_Title"), Loc.Get("Note_Prompt"), string.Empty, multiline: true);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var page = hit.Page;
        var point = new PointD(hit.View.X - 10, hit.View.Y - 10);
        var color = Host.Options.MarkupColor;
        _ = Host.EditAsync(page, () => Host.Pdf.AddNote(page, point, text, color), "Status_AnnotationAdded");
    }
}

/// <summary>Tẩy: bấm vào nhận xét để xóa.</summary>
public sealed class EraserTool(IToolHost host) : Tool(host)
{
    private Rectangle? _hover;

    public override Cursor? Cursor => Cursors.Hand;

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        ClearHover();
        if (hit is null || Find(hit) is not { } annotation)
        {
            return;
        }

        _hover = ToolVisuals.DashedRect((Brush)Application.Current.Resources["DangerBrush"]);
        ToolVisuals.Place(_hover, hit.PageView.ToLocal(annotation.ViewRect.Inflate(2)));
        hit.PageView.ToolLayer.Children.Add(_hover);
    }

    public override void OnMouseLeave(DocumentView view) => ClearHover();

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null || Find(hit) is not { } annotation)
        {
            return;
        }

        ClearHover();
        var page = hit.Page;
        _ = Host.EditAsync(page, () =>
        {
            Host.Pdf.DeleteAnnotation(page, annotation.Index);
            return -1;
        }, "Status_AnnotationDeleted");
    }

    private AnnotationInfo? Find(PageHit hit) =>
        Host.GetAnnotations(hit.Page)?
            .Where(a => a.ViewRect.Inflate(2).Contains(hit.View.X, hit.View.Y))
            .OrderBy(a => a.ViewRect.Area)
            .FirstOrDefault();

    private void ClearHover()
    {
        if (_hover?.Parent is Canvas c)
        {
            c.Children.Remove(_hover);
        }

        _hover = null;
    }
}

/// <summary>Đánh dấu vùng cần che thông tin (chưa áp dụng). Bấm vào vùng đã đánh dấu để bỏ.</summary>
public sealed class RedactTool(IToolHost host) : Tool(host)
{
    private int _page = -1;
    private PointD _start;
    private Rectangle? _preview;

    /// <summary>Gọi khi danh sách vùng che thay đổi (để workspace vẽ lại / cập nhật panel).</summary>
    public event EventHandler? Changed;

    public override Cursor? Cursor => Cursors.Cross;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        if (Host.Options.Redactions.TryGetValue(hit.Page, out var list) &&
            list.FindIndex(r => r.Contains(hit.View.X, hit.View.Y)) is var i and >= 0)
        {
            list.RemoveAt(i);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _page = hit.Page;
        _start = hit.View;
        _preview = ToolVisuals.DashedRect((Brush)Application.Current.Resources["DangerBrush"], (Brush)Application.Current.Resources["RedactBrush"]);
        hit.PageView.ToolLayer.Children.Add(_preview);
        view.CaptureMouseOnHost();
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_page >= 0 && _preview is not null && view.GetPageView(_page) is { } pv)
        {
            ToolVisuals.Place(_preview, pv.ToLocal(ToolVisuals.Normalize(_start, view.ToPageView(_page, hostPoint))));
        }
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.ReleaseMouseOnHost();
        if (_page < 0)
        {
            return;
        }

        var rect = ToolVisuals.Normalize(_start, view.ToPageView(_page, hostPoint));
        view.GetPageView(_page)?.ToolLayer.Children.Clear();
        if (rect.Width > 2 && rect.Height > 2)
        {
            if (!Host.Options.Redactions.TryGetValue(_page, out var list))
            {
                Host.Options.Redactions[_page] = list = [];
            }

            list.Add(rect);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        _page = -1;
    }
}
