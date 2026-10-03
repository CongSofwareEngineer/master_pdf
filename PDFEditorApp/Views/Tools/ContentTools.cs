using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Tools;

/// <summary>Ô sửa chữ trực tiếp trên trang (Enter = xong, Shift+Enter = xuống dòng, Esc = hủy).</summary>
internal sealed class InlineTextEditor
{
    private readonly TextBox _box;
    private readonly Action<string> _commit;
    private bool _done;

    private InlineTextEditor(PageView page, PointD viewPoint, string text, TextStyle style, double minViewWidth, Action<string> commit)
    {
        _commit = commit;
        _box = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            MinWidth = Math.Max(60, minViewWidth * page.Scale) + 10,
            Padding = new Thickness(1),
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
            CaretBrush = Brushes.Black,
            FontFamily = new FontFamily(style.FontFamily),
            FontSize = Math.Max(6, style.FontSize * page.Scale),
            FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = ToolVisuals.Brush(style.Color),
            Style = null,
        };
        _box.SetResourceReference(Control.BorderBrushProperty, "AccentBrandBrush");
        Canvas.SetLeft(_box, (viewPoint.X * page.Scale) - 3);
        Canvas.SetTop(_box, (viewPoint.Y * page.Scale) - 3);
        page.ToolLayer.Children.Add(_box);
        _box.PreviewKeyDown += OnKeyDown;
        _box.LostKeyboardFocus += (_, _) => Finish(commit: true);
        _box.Loaded += (_, _) =>
        {
            _box.Focus();
            _box.SelectAll();
        };
    }

    public static InlineTextEditor Open(PageView page, PointD viewPoint, string text, TextStyle style, double minViewWidth, Action<string> commit) =>
        new(page, viewPoint, text, style, minViewWidth, commit);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Finish(commit: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Finish(commit: true);
            e.Handled = true;
        }
    }

    private void Finish(bool commit)
    {
        if (_done)
        {
            return;
        }

        _done = true;
        var text = _box.Text;
        if (_box.Parent is Canvas c)
        {
            c.Children.Remove(_box);
        }

        if (commit)
        {
            _commit(text);
        }
    }
}

/// <summary>Thêm chữ mới: bấm lên trang, gõ, Enter. Xem docs/text-edit.md.</summary>
public sealed class AddTextTool(IToolHost host) : Tool(host)
{
    public override Cursor? Cursor => Cursors.IBeam;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        e.Handled = true;
        var page = hit.Page;
        var point = hit.View;
        var style = Host.Options.TextStyle;
        InlineTextEditor.Open(hit.PageView, point, string.Empty, style, 0, text =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _ = Host.EditAsync(page, () => Host.Pdf.AddText(page, point.X, point.Y, text, style), "Status_TextAdded");
        });
    }
}

/// <summary>
/// Sửa nội dung (kiểu Acrobat "Edit PDF"): chọn chữ / ảnh / hình, kéo để di chuyển, kéo tay nắm để đổi
/// kích thước, nhấp đúp chữ để sửa, Delete để xóa, phím mũi tên để dịch. Xem docs/page-objects.md.
/// </summary>
public sealed class EditContentTool(IToolHost host) : Tool(host)
{
    private const double HandleSize = 8;

    private enum DragMode
    {
        None,
        Move,
        Resize,
    }

    private DragMode _mode;
    private int _handle;
    private Point _startHost;
    private RectD _startRect;
    private RectD _currentRect;
    private Rectangle? _preview;
    private Rectangle? _hover;

    public override Cursor? Cursor => Cursors.Arrow;

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        view.FocusView();
        if (hit is null)
        {
            Host.SetSelection(null);
            return;
        }

        // Tay nắm của object đang chọn.
        if (Host.Selection is ObjectSelection sel && sel.Page == hit.Page && sel.Text is null &&
            HandleAt(sel.Item.ViewRect, hit.View, hit.PageView.Scale) is var h and >= 0)
        {
            BeginDrag(view, DragMode.Resize, h, hostPoint, sel.Item.ViewRect, hit.PageView);
            return;
        }

        var obj = FindObject(hit);
        if (obj is null)
        {
            Host.SetSelection(null);
            return;
        }

        var text = obj.Kind == PageObjectKind.Text ? Host.GetTextObjects(hit.Page)?.FirstOrDefault(t => t.ObjectIndex == obj.Index) : null;
        Host.SetSelection(new ObjectSelection(hit.Page, obj, text));
        if (e.ClickCount >= 2 && text is not null)
        {
            EditTextInline(hit.PageView, hit.Page, text);
            return;
        }

        BeginDrag(view, DragMode.Move, -1, hostPoint, obj.ViewRect, hit.PageView);
    }

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        if (_mode != DragMode.None && Host.Selection is ObjectSelection sel && view.GetPageView(sel.Page) is { } pv)
        {
            var d = (hostPoint - _startHost) / pv.Scale;
            _currentRect = _mode == DragMode.Move
                ? new RectD(_startRect.X + d.X, _startRect.Y + d.Y, _startRect.Width, _startRect.Height)
                : ResizeRect(_startRect, _handle, d.X, d.Y, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || sel.Item.Kind == PageObjectKind.Image);
            if (_preview is not null)
            {
                ToolVisuals.Place(_preview, pv.ToLocal(_currentRect));
            }

            return;
        }

        ClearHover();
        if (hit is null)
        {
            view.SetCursor(null);
            return;
        }

        if (Host.Selection is ObjectSelection s && s.Page == hit.Page && s.Text is null &&
            HandleAt(s.Item.ViewRect, hit.View, hit.PageView.Scale) is var h and >= 0)
        {
            view.SetCursor(h is 0 or 7 ? Cursors.SizeNWSE : h is 2 or 5 ? Cursors.SizeNESW : h is 1 or 6 ? Cursors.SizeNS : Cursors.SizeWE);
            return;
        }

        var obj = FindObject(hit);
        view.SetCursor(obj is null ? null : Cursors.SizeAll);
        if (obj is not null && !(Host.Selection is ObjectSelection cur && cur.Page == hit.Page && cur.Item.Index == obj.Index))
        {
            _hover = ToolVisuals.DashedRect((Brush)Application.Current.Resources["SelectionStrokeBrush"]);
            ToolVisuals.Place(_hover, hit.PageView.ToLocal(obj.ViewRect.Inflate(1)));
            hit.PageView.ToolLayer.Children.Add(_hover);
        }
    }

    public override void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (_mode == DragMode.None)
        {
            return;
        }

        var mode = _mode;
        _mode = DragMode.None;
        view.ReleaseMouseOnHost();
        if (_preview?.Parent is Canvas c)
        {
            c.Children.Remove(_preview);
        }

        _preview = null;
        if (Host.Selection is not ObjectSelection sel)
        {
            return;
        }

        var from = _startRect;
        var to = _currentRect;
        if (Math.Abs(from.X - to.X) < 0.5 && Math.Abs(from.Y - to.Y) < 0.5 &&
            Math.Abs(from.Width - to.Width) < 0.5 && Math.Abs(from.Height - to.Height) < 0.5)
        {
            return;
        }

        var transform = mode == DragMode.Move
            ? Affine.Translation(to.X - from.X, to.Y - from.Y)
            : Affine.Translation(-from.X, -from.Y)
                .Then(new Affine(to.Width / Math.Max(0.1, from.Width), 0, 0, to.Height / Math.Max(0.1, from.Height), 0, 0))
                .Then(Affine.Translation(to.X, to.Y));
        Transform(sel, transform);
    }

    public override bool OnKeyDown(DocumentView view, KeyEventArgs e)
    {
        if (Host.Selection is not ObjectSelection sel)
        {
            return false;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Delete:
                DeleteSelected(sel);
                return true;
            case Key.Escape:
                Host.SetSelection(null);
                return true;
            case Key.Left:
                Transform(sel, Affine.Translation(-step, 0));
                return true;
            case Key.Right:
                Transform(sel, Affine.Translation(step, 0));
                return true;
            case Key.Up:
                Transform(sel, Affine.Translation(0, -step));
                return true;
            case Key.Down:
                Transform(sel, Affine.Translation(0, step));
                return true;
            case Key.Enter when sel.Text is not null && view.GetPageView(sel.Page) is { } pv:
                EditTextInline(pv, sel.Page, sel.Text);
                return true;
            default:
                return false;
        }
    }

    public override void OnPageDecorate(DocumentView view, PageView page)
    {
        if (Host.Selection is not ObjectSelection sel || sel.Page != page.PageIndex)
        {
            return;
        }

        var stroke = (Brush)Application.Current.Resources["SelectionStrokeBrush"];
        var frame = new Rectangle { Stroke = stroke, StrokeThickness = 1.5, IsHitTestVisible = false };
        ToolVisuals.Place(frame, page.ToLocal(sel.Item.ViewRect.Inflate(1)));
        page.ToolLayer.Children.Add(frame);
        if (sel.Text is not null)
        {
            return;
        }

        foreach (var p in HandlePoints(sel.Item.ViewRect))
        {
            var local = page.ToLocal(p);
            var handle = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = Brushes.White,
                Stroke = stroke,
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(handle, local.X - (HandleSize / 2));
            Canvas.SetTop(handle, local.Y - (HandleSize / 2));
            page.ToolLayer.Children.Add(handle);
        }
    }

    /// <summary>Xóa object đang chọn (gọi từ phím Delete hoặc panel).</summary>
    public void DeleteSelected(ObjectSelection sel)
    {
        Host.SetSelection(null);
        _ = Host.EditAsync(sel.Page, () =>
        {
            Host.Pdf.DeleteObject(sel.Page, sel.Item.Index);
            return -1;
        }, "Status_ObjectDeleted");
    }

    private void Transform(ObjectSelection sel, Affine transform)
    {
        var page = sel.Page;
        var index = sel.Item.Index;
        _ = Host.EditAsync(page, () => Host.Pdf.TransformObject(page, index, transform), "Status_ObjectMoved",
            newIndex => Host.SelectObjectAfterEdit(page, newIndex));
    }

    private void EditTextInline(PageView pv, int page, TextObjectInfo text)
    {
        var style = Host.Options.TextStyle;
        InlineTextEditor.Open(pv, new PointD(text.ViewBounds.X, text.ViewBounds.Y), text.Text, style, text.ViewBounds.Width, value =>
        {
            if (value == text.Text && style == text.Style)
            {
                return;
            }

            _ = Host.EditAsync(page, () => Host.Pdf.UpdateTextObject(page, text.ObjectIndex, value, style), "Status_TextUpdated",
                newIndex => Host.SelectObjectAfterEdit(page, newIndex));
        });
    }

    private void BeginDrag(DocumentView view, DragMode mode, int handle, Point hostPoint, RectD rect, PageView pv)
    {
        _mode = mode;
        _handle = handle;
        _startHost = hostPoint;
        _startRect = rect;
        _currentRect = rect;
        _preview = ToolVisuals.DashedRect((Brush)Application.Current.Resources["SelectionStrokeBrush"],
            (Brush)Application.Current.Resources["SelectionFillBrush"]);
        ToolVisuals.Place(_preview, pv.ToLocal(rect));
        pv.ToolLayer.Children.Add(_preview);
        view.CaptureMouseOnHost();
    }

    private PageObjectInfo? FindObject(PageHit hit) =>
        Host.GetObjects(hit.Page)?
            .Where(o => o.ViewRect.Inflate(2).Contains(hit.View.X, hit.View.Y))
            .OrderBy(o => o.Kind == PageObjectKind.Text ? 0 : 1)
            .ThenBy(o => o.ViewRect.Area)
            .FirstOrDefault();

    private static IEnumerable<PointD> HandlePoints(RectD r)
    {
        double[] xs = [r.X, r.X + (r.Width / 2), r.Right];
        double[] ys = [r.Y, r.Y + (r.Height / 2), r.Bottom];
        // Thứ tự: 0 TL,1 T,2 TR,3 L,4 R,5 BL,6 B,7 BR.
        yield return new PointD(xs[0], ys[0]);
        yield return new PointD(xs[1], ys[0]);
        yield return new PointD(xs[2], ys[0]);
        yield return new PointD(xs[0], ys[1]);
        yield return new PointD(xs[2], ys[1]);
        yield return new PointD(xs[0], ys[2]);
        yield return new PointD(xs[1], ys[2]);
        yield return new PointD(xs[2], ys[2]);
    }

    private static int HandleAt(RectD r, PointD p, double scale)
    {
        var tolerance = (HandleSize / scale) * 0.8;
        var i = 0;
        foreach (var h in HandlePoints(r))
        {
            if (Math.Abs(h.X - p.X) <= tolerance && Math.Abs(h.Y - p.Y) <= tolerance)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    private static RectD ResizeRect(RectD r, int handle, double dx, double dy, bool keepAspect)
    {
        double left = r.X, top = r.Y, right = r.Right, bottom = r.Bottom;
        if (handle is 0 or 3 or 5)
        {
            left = Math.Min(right - 2, left + dx);
        }

        if (handle is 2 or 4 or 7)
        {
            right = Math.Max(left + 2, right + dx);
        }

        if (handle is 0 or 1 or 2)
        {
            top = Math.Min(bottom - 2, top + dy);
        }

        if (handle is 5 or 6 or 7)
        {
            bottom = Math.Max(top + 2, bottom + dy);
        }

        if (keepAspect && handle is 0 or 2 or 5 or 7 && r.Width > 0 && r.Height > 0)
        {
            var aspect = r.Width / r.Height;
            var w = right - left;
            var h = w / aspect;
            if (handle is 0 or 2)
            {
                top = bottom - h;
            }
            else
            {
                bottom = top + h;
            }
        }

        return new RectD(left, top, right - left, bottom - top);
    }

    private void ClearHover()
    {
        if (_hover?.Parent is Canvas c)
        {
            c.Children.Remove(_hover);
        }

        _hover = null;
    }
}

/// <summary>
/// Đặt ảnh / dấu ✓ ✗ / ngày / chữ ký: khung xem trước đi theo chuột, bấm để đặt. Xem docs/sign.md.
/// </summary>
public sealed class PlaceTool(IToolHost host) : Tool(host)
{
    private const double SignatureWidth = 160;
    private const double ImageWidth = 200;

    private FrameworkElement? _ghost;

    public override Cursor? Cursor => Cursors.Cross;

    public override void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
        RemoveGhost();
        if (hit is null)
        {
            return;
        }

        var size = GhostSize();
        var r = new RectD(hit.View.X - (size.Width / 2), hit.View.Y - (size.Height / 2), size.Width, size.Height);
        _ghost = ToolVisuals.DashedRect((Brush)Application.Current.Resources["SelectionStrokeBrush"],
            (Brush)Application.Current.Resources["SelectionFillBrush"]);
        ToolVisuals.Place(_ghost, hit.PageView.ToLocal(r));
        hit.PageView.ToolLayer.Children.Add(_ghost);
    }

    public override void OnMouseLeave(DocumentView view) => RemoveGhost();

    public override void Deactivate(DocumentView view) => RemoveGhost();

    public override void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
        if (hit is null)
        {
            return;
        }

        RemoveGhost();
        var page = hit.Page;
        var size = GhostSize();
        var rect = new RectD(hit.View.X - (size.Width / 2), hit.View.Y - (size.Height / 2), size.Width, size.Height);
        var o = Host.Options;
        if (o.PendingImage is { } image)
        {
            _ = Host.EditAsync(page, () => Host.Pdf.InsertImage(page, rect, image), "Status_ImageInserted",
                index => Host.SelectObjectAfterEdit(page, index));
            o.PendingImage = null;
            return;
        }

        var color = o.InkColor;
        switch (o.Stamp)
        {
            case StampKind.Check:
            case StampKind.Cross:
                var symbol = o.Stamp == StampKind.Check ? "✓" : "✗";
                var symbolStyle = new TextStyle(FontService.SymbolFamily, 18, false, false, color);
                _ = Host.EditAsync(page, () => Host.Pdf.AddText(page, rect.X, rect.Y, symbol, symbolStyle), "Status_TextAdded");
                break;
            case StampKind.Date:
                var date = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
                var dateStyle = Host.Options.TextStyle with { Color = color };
                _ = Host.EditAsync(page, () => Host.Pdf.AddText(page, rect.X, rect.Y, date, dateStyle), "Status_TextAdded");
                break;
            case StampKind.Signature when o.Signature is { } signature:
                PlaceSignature(page, rect, signature);
                Host.ActivateDefaultTool();
                break;
        }
    }

    private void PlaceSignature(int page, RectD rect, SavedSignature signature)
    {
        var color = RgbColor.TryParseHex(signature.Color, out var c) ? c : new RgbColor(30, 58, 138);
        if (signature.Strokes is { Count: > 0 } strokes)
        {
            var mapped = strokes
                .Select(s => (IReadOnlyList<PointD>)s.Select(p => new PointD(rect.X + (p.X * rect.Width), rect.Y + (p.Y * rect.Height))).ToList())
                .ToList();
            _ = Host.EditAsync(page, () => Host.Pdf.AddInk(page, mapped, color, 1.6), "Status_SignaturePlaced");
        }
        else if (!string.IsNullOrWhiteSpace(signature.Text))
        {
            var style = new TextStyle(signature.FontFamily ?? FontService.ScriptFamily, 26, false, false, color);
            var text = signature.Text;
            _ = Host.EditAsync(page, () => Host.Pdf.AddText(page, rect.X, rect.Y, text, style), "Status_SignaturePlaced");
        }
    }

    private Size GhostSize()
    {
        var o = Host.Options;
        if (o.PendingImage is { } image)
        {
            var aspect = image.PixelHeight / (double)Math.Max(1, image.PixelWidth);
            return new Size(ImageWidth, ImageWidth * aspect);
        }

        return o.Stamp switch
        {
            StampKind.Signature when o.Signature is { Strokes.Count: > 0 } s => new Size(SignatureWidth, SignatureWidth / Math.Max(0.2, s.AspectRatio)),
            StampKind.Signature => new Size(SignatureWidth, 36),
            StampKind.Date => new Size(80, 16),
            _ => new Size(16, 20),
        };
    }

    private void RemoveGhost()
    {
        if (_ghost?.Parent is Canvas c)
        {
            c.Children.Remove(_ghost);
        }

        _ghost = null;
    }
}
