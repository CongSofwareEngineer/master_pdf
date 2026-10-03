using System.Windows;
using System.Windows.Input;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Tools;

/// <summary>Kéo để bôi đen chữ (dùng chung cho công cụ Chọn và Tô sáng / Gạch chân…).</summary>
internal sealed class TextDrag
{
    private int _page = -1;
    private int _anchor;

    public bool Active => _page >= 0;

    /// <summary>Bắt đầu bôi đen nếu điểm nhấn trúng chữ. Nhấp đúp = chọn cả từ.</summary>
    public bool Begin(IToolHost host, DocumentView view, PageHit hit, int clickCount)
    {
        var layout = host.GetTextLayout(hit.Page);
        if (layout is null)
        {
            return false;
        }

        var exact = layout.HitTest(hit.View.X, hit.View.Y, nearest: false);
        if (exact < 0)
        {
            return false;
        }

        if (clickCount >= 2)
        {
            var (s, e) = layout.WordAt(exact);
            host.SetSelection(new TextRangeSelection(hit.Page, s, e));
            return true;
        }

        _page = hit.Page;
        _anchor = layout.HitTest(hit.View.X, hit.View.Y, nearest: true);
        host.SetSelection(new TextRangeSelection(_page, _anchor, _anchor));
        view.CaptureMouseOnHost();
        return true;
    }

    public void Update(IToolHost host, DocumentView view, Point hostPoint)
    {
        if (_page < 0 || host.GetTextLayout(_page) is not { } layout)
        {
            return;
        }

        var v = view.ToPageView(_page, hostPoint);
        var index = layout.HitTest(v.X, v.Y, nearest: true);
        if (index >= 0)
        {
            host.SetSelection(new TextRangeSelection(_page, Math.Min(_anchor, index), Math.Max(_anchor, index)));
        }
    }

    public TextRangeSelection? End(IToolHost host, DocumentView view)
    {
        _page = -1;
        view.ReleaseMouseOnHost();
        return host.Selection is TextRangeSelection { Start: var s, End: var e } t && e > s ? t : null;
    }
}

/// <summary>
/// Công cụ Chọn (mặc định): bôi đen & sao chép chữ, bấm liên kết, chọn nhận xét (Delete để xóa).
/// Xem docs/text-select.md.
/// </summary>
public sealed class SelectTool(IToolHost host) : Tool(host)
{
    private readonly TextDrag _drag = new();
    private LinkInfo? _pendingLink;
    private Point _downPoint;

    public override Cursor? Cursor => Cursors.Arrow;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.FocusView();
        _downPoint = hostPoint;
        _pendingLink = null;
        if (hit is null)
        {
            Host.SetSelection(null);
            return;
        }

        _pendingLink = Host.GetLinks(hit.Page)?.FirstOrDefault(l => l.ViewRect.Contains(hit.View.X, hit.View.Y));
        if (_pendingLink is null && HitAnnotation(hit) is { } annotation)
        {
            Host.SetSelection(new AnnotationSelection(hit.Page, annotation));
            return;
        }

        if (!_drag.Begin(Host, view, hit, e.ClickCount) && _pendingLink is null)
        {
            Host.SetSelection(null);
        }
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_drag.Active)
        {
            _drag.Update(Host, view, hostPoint);
            return;
        }

        Cursor? cursor = Cursors.Arrow;
        if (hit is not null)
        {
            if (Host.GetLinks(hit.Page)?.Any(l => l.ViewRect.Contains(hit.View.X, hit.View.Y)) == true ||
                HitAnnotation(hit) is not null)
            {
                cursor = Cursors.Hand;
            }
            else if (Host.GetTextLayout(hit.Page)?.HitTest(hit.View.X, hit.View.Y, nearest: false) >= 0)
            {
                cursor = Cursors.IBeam;
            }
        }

        view.SetCursor(cursor);
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        var selection = _drag.Active ? _drag.End(Host, view) : null;
        if (_pendingLink is { } link && (hostPoint - _downPoint).Length < 4 && selection is null)
        {
            if (link.TargetPage >= 0)
            {
                Host.NavigateToPage(link.TargetPage);
            }
            else if (!string.IsNullOrEmpty(link.Uri))
            {
                Host.OpenLink(link.Uri);
            }
        }

        _pendingLink = null;
    }

    public override bool OnKeyDown(DocumentView view, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.C when ctrl && Host.Selection is TextRangeSelection t:
                if (Host.GetTextLayout(t.Page) is { } layout)
                {
                    TryCopy(layout.GetRangeText(t.Start, t.End));
                    Host.SetStatus(Loc.Get("Status_Copied"));
                }

                return true;
            case Key.A when ctrl:
                if (Host.GetTextLayout(view.CurrentPage) is { } all)
                {
                    Host.SetSelection(new TextRangeSelection(view.CurrentPage, 0, all.Length));
                }

                return true;
            case Key.Delete when Host.Selection is AnnotationSelection a:
                _ = Host.EditAsync(a.Page, () =>
                {
                    Host.Pdf.DeleteAnnotation(a.Page, a.Annotation.Index);
                    return -1;
                }, "Status_AnnotationDeleted");
                Host.SetSelection(null);
                return true;
            case Key.Escape when Host.Selection is not null:
                Host.SetSelection(null);
                return true;
            default:
                return false;
        }
    }

    private AnnotationInfo? HitAnnotation(PageHit hit) =>
        Host.GetAnnotations(hit.Page)?
            .Where(a => a.ViewRect.Inflate(2).Contains(hit.View.X, hit.View.Y))
            .OrderBy(a => a.ViewRect.Area)
            .FirstOrDefault();

    internal static void TryCopy(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Clipboard đang bị app khác khóa: bỏ qua.
        }
    }
}

/// <summary>Công cụ bàn tay: kéo để cuộn.</summary>
public sealed class HandTool(IToolHost host) : Tool(host)
{
    private Point? _last;

    public override Cursor? Cursor => Cursors.Hand;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        _last = e.GetPosition(view);
        view.CaptureMouseOnHost();
        view.SetCursor(Cursors.ScrollAll);
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_last is not { } last || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var p = e.GetPosition(view);
        view.ScrollBy(last.X - p.X, last.Y - p.Y);
        _last = p;
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        _last = null;
        view.ReleaseMouseOnHost();
        view.SetCursor(null);
    }
}

/// <summary>Tô sáng / gạch chân / gạch ngang / gạch sóng: kéo trên chữ. Xem docs/annotations.md.</summary>
public sealed class MarkupTool(IToolHost host, AnnotationKind kind) : Tool(host)
{
    private readonly TextDrag _drag = new();

    public AnnotationKind Kind { get; } = kind;

    public override Cursor? Cursor => Cursors.IBeam;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.FocusView();
        if (hit is null || !_drag.Begin(Host, view, hit, e.ClickCount))
        {
            Host.SetSelection(null);
            return;
        }

        if (e.ClickCount >= 2 && Host.Selection is TextRangeSelection word)
        {
            Apply(word);
        }
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_drag.Active)
        {
            _drag.Update(Host, view, hostPoint);
        }
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (_drag.Active && _drag.End(Host, view) is { } selection)
        {
            Apply(selection);
        }
    }

    /// <summary>Tạo nhận xét trên đoạn chữ đã chọn.</summary>
    public void Apply(TextRangeSelection selection)
    {
        if (Host.GetTextLayout(selection.Page) is not { } layout)
        {
            return;
        }

        var rects = layout.GetRangeRects(selection.Start, selection.End);
        if (rects.Count == 0)
        {
            return;
        }

        var text = layout.GetRangeText(selection.Start, selection.End);
        var color = Kind == AnnotationKind.Highlight ? Host.Options.MarkupColor : Host.Options.InkColor;
        var opacity = Host.Options.Opacity;
        var page = selection.Page;
        Host.SetSelection(null);
        _ = Host.EditAsync(page, () => Host.Pdf.AddTextMarkup(page, Kind, rects, color, opacity, text), "Status_AnnotationAdded");
    }
}
