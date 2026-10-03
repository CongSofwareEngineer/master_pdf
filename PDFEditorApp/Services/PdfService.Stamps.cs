using System.Globalization;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;

namespace PDFEditorApp.Services;

// Watermark và đầu / chân trang. Chỉ thêm object mới (stream mới) nên không đụng nội dung gốc;
// gắn mark riêng để gỡ được. Xem docs/watermark.md, header-footer.md.
public sealed partial class PdfService
{
    private const string WatermarkMark = "PDFEditorWatermark";
    private const string HeaderFooterMark = "PDFEditorHeaderFooter";

    public void AddWatermark(WatermarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Text))
        {
            throw new ArgumentException("Empty text.", nameof(options));
        }

        Mutate(() =>
        {
            foreach (var index in TargetPages(options.Pages))
            {
                EditPage(index, page =>
                {
                    var info = BuildPageInfo(page, index);
                    var style = new TextStyle(options.FontFamily, options.FontSize, true, false, options.Color);
                    var angle = options.RotationDegrees * Math.PI / 180;
                    var obj = PlaceText(page, info, options.Text, style,
                        new PointD(Math.Cos(angle), -Math.Sin(angle)), new PointD(-Math.Sin(angle), -Math.Cos(angle)), WatermarkMark);
                    _ = FPDFPageObj_SetFillColor(obj, options.Color.R, options.Color.G, options.Color.B,
                        (uint)Math.Clamp(Math.Round(options.Opacity * 255), 1, 255));
                    MoveCenterTo(obj, info, new PointD(info.Width / 2, info.Height / 2));
                });
            }

            return 0;
        });
    }

    public void AddHeaderFooter(HeaderFooterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Template))
        {
            throw new ArgumentException("Empty template.", nameof(options));
        }

        Mutate(() =>
        {
            var total = FPDF_GetPageCount(_doc);
            var date = DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            foreach (var index in TargetPages(options.Pages))
            {
                var text = options.Template
                    .Replace("{n}", (index + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{N}", total.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{date}", date, StringComparison.Ordinal);
                EditPage(index, page =>
                {
                    var info = BuildPageInfo(page, index);
                    var style = new TextStyle(options.FontFamily, options.FontSize, false, false, options.Color);
                    var obj = PlaceText(page, info, text, style, new PointD(1, 0), new PointD(0, -1), HeaderFooterMark);
                    var r = ObjectViewRect(obj, info);
                    var x = options.Position switch
                    {
                        StampPosition.TopLeft or StampPosition.BottomLeft => options.Margin,
                        StampPosition.TopRight or StampPosition.BottomRight => info.Width - options.Margin - r.Width,
                        _ => (info.Width - r.Width) / 2,
                    };
                    var y = options.Position is StampPosition.TopLeft or StampPosition.TopCenter or StampPosition.TopRight
                        ? options.Margin
                        : info.Height - options.Margin - r.Height;
                    MoveCenterTo(obj, info, new PointD(x + (r.Width / 2), y + (r.Height / 2)));
                });
            }

            return 0;
        });
    }

    /// <summary>Gỡ mọi watermark do app thêm. Trả về số object đã gỡ.</summary>
    public int RemoveWatermarks() => RemoveMarked(WatermarkMark);

    /// <summary>Gỡ mọi đầu / chân trang do app thêm. Trả về số object đã gỡ.</summary>
    public int RemoveHeaderFooters() => RemoveMarked(HeaderFooterMark);

    private int RemoveMarked(string mark) => Mutate(() =>
    {
        var removed = 0;
        for (var index = 0; index < FPDF_GetPageCount(_doc); index++)
        {
            EditPage(index, page =>
            {
                for (var i = FPDFPage_CountObjects(page) - 1; i >= 0; i--)
                {
                    var obj = FPDFPage_GetObject(page, i);
                    if (obj != IntPtr.Zero && HasMark(obj, mark) && FPDFPage_RemoveObject(page, obj) != 0)
                    {
                        FPDFPageObj_Destroy(obj);
                        removed++;
                    }
                }
            });
        }

        return removed;
    });

    private IEnumerable<int> TargetPages(IReadOnlyList<int>? pages)
    {
        var count = FPDF_GetPageCount(_doc);
        return (pages ?? Enumerable.Range(0, count).ToList()).Distinct().Where(p => p >= 0 && p < count).Order();
    }

    /// <summary>Sửa một trang rồi tái tạo nội dung (dùng trang đang hoạt động nếu trùng).</summary>
    private void EditPage(int pageIndex, Action<IntPtr> action)
    {
        if (_activePage != IntPtr.Zero && _activePageIndex == pageIndex)
        {
            action(_activePage);
            GenerateContent(_activePage);
            return;
        }

        var page = LoadPage(pageIndex);
        try
        {
            action(page);
            GenerateContent(page);
        }
        finally
        {
            FPDF_ClosePage(page);
        }
    }

    /// <summary>Tạo một dòng chữ với hướng (right, up) trong hệ view, gắn mark, chèn vào trang.</summary>
    private IntPtr PlaceText(IntPtr page, PdfPageInfo info, string text, TextStyle style, PointD viewRight, PointD viewUp, string mark)
    {
        var right = Normalize(info.ViewToPage.TransformVector(viewRight.X, viewRight.Y));
        var up = Normalize(info.ViewToPage.TransformVector(viewUp.X, viewUp.Y));
        var origin = info.ViewToPage.Transform(0, 0);
        var matrix = new FsMatrix
        {
            A = (float)right.X,
            B = (float)right.Y,
            C = (float)up.X,
            D = (float)up.Y,
            E = (float)origin.X,
            F = (float)origin.Y,
        };
        var obj = CreateTextObject(GetFont(style, text), text, style, matrix);
        _ = FPDFPageObj_AddMark(obj, ToCString(mark)!);
        if (FPDFPage_InsertObject(page, obj) == 0)
        {
            FPDFPageObj_Destroy(obj);
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        return obj;
    }

    private static RectD ObjectViewRect(IntPtr obj, PdfPageInfo info) =>
        FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) != 0
            ? info.ToView(new PdfBounds(l, b, r, t))
            : default;

    private static void MoveCenterTo(IntPtr obj, PdfPageInfo info, PointD viewCenter)
    {
        var r = ObjectViewRect(obj, info);
        var delta = info.ViewToPage.TransformVector(viewCenter.X - (r.X + (r.Width / 2)), viewCenter.Y - (r.Y + (r.Height / 2)));
        FPDFPageObj_Transform(obj, 1, 0, 0, 1, delta.X, delta.Y);
    }
}
