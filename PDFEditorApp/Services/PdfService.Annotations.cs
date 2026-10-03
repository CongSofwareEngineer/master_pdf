using System.Globalization;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;
using static PDFEditorApp.Services.Pdfium.PdfiumNativeExtra;

namespace PDFEditorApp.Services;

// Nhận xét (annotation) chuẩn PDF. Không đụng content stream nên an toàn với mọi file.
// PDFium tự sinh appearance (AP) khi render cho Highlight/Underline/StrikeOut/Squiggly/Ink/Square/Circle/Text.
// Xem docs/annotations.md.
public sealed partial class PdfService
{
    /// <summary>Tên tác giả ghi vào nhận xét mới (/T).</summary>
    public string AnnotationAuthor { get; set; } = SystemService.UserName;

    /// <summary>Danh sách nhận xét trên trang (bỏ qua liên kết, ô form, popup).</summary>
    public IReadOnlyList<AnnotationInfo> GetAnnotations(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page =>
            {
                var info = BuildPageInfo(page, pageIndex);
                var result = new List<AnnotationInfo>();
                var count = FPDFPage_GetAnnotCount(page);
                for (var i = 0; i < count; i++)
                {
                    var annot = FPDFPage_GetAnnot(page, i);
                    if (annot == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        var subtype = FPDFAnnot_GetSubtype(annot);
                        if (subtype is FPDF_ANNOT_LINK or FPDF_ANNOT_WIDGET or FPDF_ANNOT_POPUP ||
                            FPDFAnnot_GetRect(annot, out var r) == 0)
                        {
                            continue;
                        }

                        var color = FPDFAnnot_GetColor(annot, FPDFANNOT_COLORTYPE_Color, out var cr, out var cg, out var cb, out _) != 0
                            ? new RgbColor((byte)cr, (byte)cg, (byte)cb)
                            : new RgbColor(255, 212, 0);
                        var bounds = new PdfBounds(r.Left, Math.Min(r.Top, r.Bottom), r.Right, Math.Max(r.Top, r.Bottom));
                        result.Add(new AnnotationInfo(
                            pageIndex,
                            i,
                            KindOf(subtype),
                            info.ToView(bounds),
                            color,
                            ReadAnnotString(annot, "Contents"),
                            ReadAnnotString(annot, "T"),
                            ReadAnnotString(annot, "M")));
                    }
                    finally
                    {
                        FPDFPage_CloseAnnot(annot);
                    }
                }

                return result;
            });
        }
    }

    /// <summary>Tô sáng / gạch chân / gạch ngang / gạch sóng các vùng chữ (hệ view). Trả về chỉ số annotation.</summary>
    public int AddTextMarkup(int pageIndex, AnnotationKind kind, IReadOnlyList<RectD> viewRects, RgbColor color, double opacity = 1, string? contents = null)
    {
        ArgumentNullException.ThrowIfNull(viewRects);
        var subtype = kind switch
        {
            AnnotationKind.Highlight => FPDF_ANNOT_HIGHLIGHT,
            AnnotationKind.Underline => FPDF_ANNOT_UNDERLINE,
            AnnotationKind.Squiggly => FPDF_ANNOT_SQUIGGLY,
            AnnotationKind.StrikeOut => FPDF_ANNOT_STRIKEOUT,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (viewRects.Count == 0)
        {
            throw new ArgumentException("No area.", nameof(viewRects));
        }

        return CreateAnnotation(pageIndex, subtype, (annot, info) =>
        {
            SetAnnotColor(annot, FPDFANNOT_COLORTYPE_Color, color, opacity);
            var all = new List<PointD>();
            foreach (var v in viewRects)
            {
                var ul = info.ViewToPage.Transform(v.X, v.Y);
                var ur = info.ViewToPage.Transform(v.Right, v.Y);
                var ll = info.ViewToPage.Transform(v.X, v.Bottom);
                var lr = info.ViewToPage.Transform(v.Right, v.Bottom);
                var quad = new FsQuadPointsF
                {
                    X1 = (float)ul.X,
                    Y1 = (float)ul.Y,
                    X2 = (float)ur.X,
                    Y2 = (float)ur.Y,
                    X3 = (float)ll.X,
                    Y3 = (float)ll.Y,
                    X4 = (float)lr.X,
                    Y4 = (float)lr.Y,
                };
                _ = FPDFAnnot_AppendAttachmentPoints(annot, ref quad);
                all.AddRange([ul, ur, ll, lr]);
            }

            SetAnnotRect(annot, all, 0);
            if (!string.IsNullOrEmpty(contents))
            {
                SetAnnotString(annot, "Contents", contents);
            }
        });
    }

    /// <summary>Nét vẽ tự do (bút). Mỗi nét là danh sách điểm hệ view. <paramref name="width"/> theo point.</summary>
    public int AddInk(int pageIndex, IReadOnlyList<IReadOnlyList<PointD>> strokes, RgbColor color, double width, double opacity = 1)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (strokes.All(s => s.Count == 0))
        {
            throw new ArgumentException("No stroke.", nameof(strokes));
        }

        return CreateAnnotation(pageIndex, FPDF_ANNOT_INK, (annot, info) =>
        {
            SetAnnotColor(annot, FPDFANNOT_COLORTYPE_Color, color, opacity);
            _ = FPDFAnnot_SetBorder(annot, 0, 0, (float)width);
            var all = new List<PointD>();
            foreach (var stroke in strokes.Where(s => s.Count > 0))
            {
                // Nét chỉ có 1 điểm (chấm) → nhân đôi điểm để vẫn vẽ được.
                var points = stroke.Count == 1 ? [stroke[0], new PointD(stroke[0].X + 0.1, stroke[0].Y)] : stroke;
                var mapped = points.Select(p => info.ViewToPage.Transform(p)).ToArray();
                all.AddRange(mapped);
                var native = mapped.Select(p => new FsPointF { X = (float)p.X, Y = (float)p.Y }).ToArray();
                _ = FPDFAnnot_AddInkStroke(annot, native, (nuint)native.Length);
            }

            SetAnnotRect(annot, all, width);
        });
    }

    /// <summary>Đường thẳng từ a tới b (hệ view), có thể có đầu mũi tên ở b. Lưu dạng Ink.</summary>
    public int AddLine(int pageIndex, PointD a, PointD b, bool arrow, RgbColor color, double width)
    {
        var strokes = new List<IReadOnlyList<PointD>> { new[] { a, b } };
        if (arrow)
        {
            var angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            var head = Math.Max(8, width * 4);
            const double spread = Math.PI / 7;
            var left = new PointD(b.X - (head * Math.Cos(angle - spread)), b.Y - (head * Math.Sin(angle - spread)));
            var right = new PointD(b.X - (head * Math.Cos(angle + spread)), b.Y - (head * Math.Sin(angle + spread)));
            strokes.Add([left, b, right]);
        }

        return AddInk(pageIndex, strokes, color, width);
    }

    /// <summary>Hình chữ nhật (Square) hoặc elip (Circle) trong khung hệ view.</summary>
    public int AddShape(int pageIndex, AnnotationKind kind, RectD viewRect, RgbColor stroke, RgbColor? fill, double width)
    {
        var subtype = kind switch
        {
            AnnotationKind.Square => FPDF_ANNOT_SQUARE,
            AnnotationKind.Circle => FPDF_ANNOT_CIRCLE,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return CreateAnnotation(pageIndex, subtype, (annot, info) =>
        {
            SetAnnotColor(annot, FPDFANNOT_COLORTYPE_Color, stroke, 1);
            if (fill is { } f)
            {
                SetAnnotColor(annot, FPDFANNOT_COLORTYPE_InteriorColor, f, 1);
            }

            _ = FPDFAnnot_SetBorder(annot, 0, 0, (float)width);
            var corners = new[]
            {
                info.ViewToPage.Transform(viewRect.X, viewRect.Y),
                info.ViewToPage.Transform(viewRect.Right, viewRect.Bottom),
                info.ViewToPage.Transform(viewRect.X, viewRect.Bottom),
                info.ViewToPage.Transform(viewRect.Right, viewRect.Y),
            };
            SetAnnotRect(annot, corners, 0);
        });
    }

    /// <summary>Ghi chú (sticky note) tại điểm hệ view.</summary>
    public int AddNote(int pageIndex, PointD viewPoint, string text, RgbColor color)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CreateAnnotation(pageIndex, FPDF_ANNOT_TEXT, (annot, info) =>
        {
            SetAnnotColor(annot, FPDFANNOT_COLORTYPE_Color, color, 1);
            const double size = 20;
            var corners = new[]
            {
                info.ViewToPage.Transform(viewPoint.X, viewPoint.Y),
                info.ViewToPage.Transform(viewPoint.X + size, viewPoint.Y + size),
            };
            SetAnnotRect(annot, corners, 0);
            SetAnnotString(annot, "Contents", text);
        });
    }

    public void DeleteAnnotation(int pageIndex, int annotIndex)
    {
        Mutate(() =>
        {
            var page = ActivatePage(pageIndex);
            if (FPDFPage_RemoveAnnot(page, annotIndex) == 0)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            return 0;
        });
    }

    /// <summary>Sửa nội dung chữ của nhận xét (ghi chú).</summary>
    public void SetAnnotationContents(int pageIndex, int annotIndex, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Mutate(() =>
        {
            var page = ActivatePage(pageIndex);
            var annot = FPDFPage_GetAnnot(page, annotIndex);
            if (annot == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            try
            {
                SetAnnotString(annot, "Contents", text);
                SetAnnotString(annot, "M", PdfDate(DateTime.Now));
            }
            finally
            {
                FPDFPage_CloseAnnot(annot);
            }

            return 0;
        });
    }

    /// <summary>Định dạng ngày PDF: D:yyyyMMddHHmmss.</summary>
    public static string PdfDate(DateTime time) => "D:" + time.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private int CreateAnnotation(int pageIndex, int subtype, Action<IntPtr, PdfPageInfo> configure) => Mutate(() =>
    {
        var page = ActivatePage(pageIndex);
        var info = BuildPageInfo(page, pageIndex);
        var annot = FPDFPage_CreateAnnot(page, subtype);
        if (annot == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        try
        {
            configure(annot, info);
            SetAnnotString(annot, "T", AnnotationAuthor);
            var now = PdfDate(DateTime.Now);
            SetAnnotString(annot, "M", now);
            SetAnnotString(annot, "CreationDate", now);
        }
        finally
        {
            FPDFPage_CloseAnnot(annot);
        }

        return FPDFPage_GetAnnotCount(page) - 1;
    });

    private static void SetAnnotColor(IntPtr annot, int type, RgbColor color, double opacity) =>
        _ = FPDFAnnot_SetColor(annot, type, color.R, color.G, color.B, (uint)Math.Clamp(Math.Round(opacity * 255), 0, 255));

    private static void SetAnnotRect(IntPtr annot, IReadOnlyList<PointD> pagePoints, double inflate)
    {
        var pad = (float)(inflate / 2) + 1;
        var rect = new FsRectF
        {
            Left = (float)pagePoints.Min(p => p.X) - pad,
            Right = (float)pagePoints.Max(p => p.X) + pad,
            Bottom = (float)pagePoints.Min(p => p.Y) - pad,
            Top = (float)pagePoints.Max(p => p.Y) + pad,
        };
        _ = FPDFAnnot_SetRect(annot, ref rect);
    }

    private static void SetAnnotString(IntPtr annot, string key, string value) =>
        _ = FPDFAnnot_SetStringValue(annot, ToCString(key)!, value);

    private static string ReadAnnotString(IntPtr annot, string key)
    {
        var keyBytes = ToCString(key)!;
        return ReadWide((b, n) => FPDFAnnot_GetStringValue(annot, keyBytes, b, n));
    }

    private static AnnotationKind KindOf(int subtype) => subtype switch
    {
        FPDF_ANNOT_TEXT => AnnotationKind.Note,
        FPDF_ANNOT_FREETEXT => AnnotationKind.FreeText,
        FPDF_ANNOT_LINE => AnnotationKind.Line,
        FPDF_ANNOT_SQUARE => AnnotationKind.Square,
        FPDF_ANNOT_CIRCLE => AnnotationKind.Circle,
        FPDF_ANNOT_POLYGON or FPDF_ANNOT_POLYLINE => AnnotationKind.Polygon,
        FPDF_ANNOT_HIGHLIGHT => AnnotationKind.Highlight,
        FPDF_ANNOT_UNDERLINE => AnnotationKind.Underline,
        FPDF_ANNOT_SQUIGGLY => AnnotationKind.Squiggly,
        FPDF_ANNOT_STRIKEOUT => AnnotationKind.StrikeOut,
        FPDF_ANNOT_STAMP => AnnotationKind.Stamp,
        FPDF_ANNOT_INK => AnnotationKind.Ink,
        _ => AnnotationKind.Other,
    };
}
