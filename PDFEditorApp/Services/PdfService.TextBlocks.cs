using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;

namespace PDFEditorApp.Services;

// Sửa văn bản theo khối (dòng / đoạn văn gộp từ nhiều text object). Xem docs/text-edit.md.
public sealed partial class PdfService
{
    /// <summary>Độ lệch bề rộng cho phép khi tự xuống dòng (font thay thế có thể rộng hơn font gốc một chút).</summary>
    private const double WrapSlack = 1.02;

    /// <summary>
    /// Các khối chữ trên trang (đơn vị chọn / sửa): text object liền mạch được gộp thành dòng, dòng liền
    /// mạch thành đoạn văn (<see cref="TextBlockBuilder"/>). Bỏ qua text đã bị lớp che phủ.
    /// </summary>
    public IReadOnlyList<TextBlockInfo> GetTextBlocks(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var covers = GetCovers(page);
            var fragments = new List<TextFragment>();
            var obstacles = new List<RectD>();
            var textPage = FPDFText_LoadPage(page);
            try
            {
                var count = FPDFPage_CountObjects(page);
                for (var i = 0; i < count; i++)
                {
                    var obj = FPDFPage_GetObject(page, i);
                    if (obj == IntPtr.Zero || HasMark(obj, CoverMark) ||
                        FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
                    {
                        continue;
                    }

                    var bounds = new PdfBounds(l, b, r, t);
                    if (IsCovered(i, bounds, covers))
                    {
                        continue;
                    }

                    var view = info.ToView(bounds);
                    var type = FPDFPageObj_GetType(obj);
                    if (type != FPDF_PAGEOBJ_TEXT)
                    {
                        if (type is FPDF_PAGEOBJ_PATH or FPDF_PAGEOBJ_IMAGE or FPDF_PAGEOBJ_FORM &&
                            (view.Width >= 0.5 || view.Height >= 0.5))
                        {
                            obstacles.Add(view);
                        }

                        continue;
                    }

                    var text = GetObjectText(obj, textPage);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    if (FPDFPageObj_GetMatrix(obj, out var m) == 0)
                    {
                        m = new FsMatrix { A = 1, D = 1, E = (float)l, F = (float)b };
                    }

                    var (style, fontName) = ReadStyle(obj);
                    var origin = info.PageToView.Transform(m.E, m.F);
                    var dir = info.PageToView.TransformVector(m.A, m.B);
                    var up = info.PageToView.TransformVector(m.C, m.D);
                    var horizontal = dir.X > 0 && Math.Abs(dir.Y) <= 0.02 * dir.X && up.Y < 0;
                    fragments.Add(new TextFragment(i, text, view, origin.X, origin.Y, horizontal, style, fontName));
                }
            }
            finally
            {
                if (textPage != IntPtr.Zero)
                {
                    FPDFText_ClosePage(textPage);
                }
            }

            return TextBlockBuilder.Build(fragments, obstacles);
        }
    }

    /// <summary>
    /// Thay cả khối chữ bằng text mới (rỗng = xóa), giữ vị trí dòng đầu, khoảng cách dòng và lề. Khối là
    /// đoạn văn (<see cref="TextBlockInfo.WrapWidth"/> &gt; 0) → tự xuống dòng theo bề rộng cũ; '\n' = xuống dòng cứng.
    /// Trả về chỉ số object đầu tiên mới tạo, -1 nếu đã xóa.
    /// </summary>
    public int UpdateTextBlock(int pageIndex, TextBlockInfo block, string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        return EditObjects(pageIndex, block.ObjectIndices, textOnly: true,
            (page, objs, overlay) => RewriteBlock(page, objs, block, text, style, overlay));
    }

    public void DeleteTextBlock(int pageIndex, TextBlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        EditObjects(pageIndex, block.ObjectIndices, textOnly: true, (page, objs, overlay) =>
        {
            RemoveOrCover(page, objs, overlay);
            GenerateContent(page);
            if (!overlay)
            {
                DestroyAll(objs);
            }

            return -1;
        });
    }

    /// <summary>Di chuyển cả khối một đoạn (dx, dy) hệ view. Trả về chỉ số object đầu của khối sau khi di chuyển.</summary>
    public int MoveTextBlock(int pageIndex, TextBlockInfo block, double deltaViewX, double deltaViewY)
    {
        ArgumentNullException.ThrowIfNull(block);
        return EditObjects(pageIndex, block.ObjectIndices, textOnly: true, (page, objs, overlay) =>
        {
            var delta = BuildPageInfo(page, pageIndex).ViewToPage.TransformVector(deltaViewX, deltaViewY);
            if (!overlay)
            {
                foreach (var obj in objs)
                {
                    FPDFPageObj_Transform(obj, 1, 0, 0, 1, delta.X, delta.Y);
                }

                GenerateContent(page);
                return block.FirstObjectIndex;
            }

            var first = -1;
            foreach (var obj in objs)
            {
                var (style, _) = ReadStyle(obj);
                var index = RewriteText(page, obj, ReadBackText(page, obj), style, delta, overlay: true);
                if (first < 0)
                {
                    first = index;
                }
            }

            return first;
        });
    }

    private int RewriteBlock(IntPtr page, IntPtr[] objs, TextBlockInfo block, string text, TextStyle style, bool overlay)
    {
        if (FPDFPageObj_GetMatrix(objs[0], out var m) == 0)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        var scale = Math.Sqrt((m.C * m.C) + (m.D * m.D));
        if (scale < 1e-6)
        {
            scale = 1;
        }

        var right = Normalize(new PointD(m.A, m.B));
        var up = Normalize(new PointD(m.C, m.D));
        var originalFont = overlay ? IntPtr.Zero : FindReusableFont(page, objs, text, style);

        RemoveOrCover(page, objs, overlay);
        try
        {
            var lines = LayoutBlock(text, block, style, originalFont);
            var lineHeight = block.LineHeight > 0
                ? block.LineHeight * style.FontSize / Math.Max(1e-6, block.Style.FontSize)
                : style.FontSize * LineSpacing;
            var first = FPDFPage_CountObjects(page);
            var created = 0;
            for (var k = 0; k < lines.Count; k++)
            {
                if (lines[k].Length == 0)
                {
                    continue;
                }

                var dx = k == 0 ? 0 : -block.Indent;
                var dy = k * lineHeight;
                var matrix = new FsMatrix
                {
                    A = (float)(m.A / scale),
                    B = (float)(m.B / scale),
                    C = (float)(m.C / scale),
                    D = (float)(m.D / scale),
                    E = (float)(m.E + (dx * right.X) - (dy * up.X)),
                    F = (float)(m.F + (dx * right.Y) - (dy * up.Y)),
                };
                InsertTextLine(page, lines[k], style, matrix, originalFont);
                created++;
            }

            GenerateContent(page);
            return created == 0 ? -1 : first;
        }
        finally
        {
            // Object cũ chỉ hủy sau khi tạo xong object mới (font của nó có thể còn được dùng).
            if (!overlay)
            {
                DestroyAll(objs);
            }
        }
    }

    /// <summary>
    /// Font gốc dùng lại được khi người dùng không đổi font / kiểu và mọi ký tự mới đã có trong các mảnh
    /// dùng font đó (font nhúng subset chỉ chứa glyph đã dùng).
    /// </summary>
    private IntPtr FindReusableFont(IntPtr page, IntPtr[] objs, string text, TextStyle style)
    {
        var textPage = FPDFText_LoadPage(page);
        try
        {
            foreach (var obj in objs)
            {
                var (oldStyle, _) = ReadStyle(obj);
                if (!string.Equals(style.FontFamily, oldStyle.FontFamily, StringComparison.Ordinal) ||
                    style.Bold != oldStyle.Bold || style.Italic != oldStyle.Italic)
                {
                    continue;
                }

                var font = FPDFTextObj_GetFont(obj);
                if (font == IntPtr.Zero)
                {
                    continue;
                }

                var oldText = string.Concat(objs.Where(o => FPDFTextObj_GetFont(o) == font).Select(o => GetObjectText(o, textPage)));
                return text.All(c => c is '\n' or '\r' or ' ' || oldText.Contains(c, StringComparison.Ordinal)) ? font : IntPtr.Zero;
            }

            return IntPtr.Zero;
        }
        finally
        {
            if (textPage != IntPtr.Zero)
            {
                FPDFText_ClosePage(textPage);
            }
        }
    }

    /// <summary>Chia text thành các dòng hiển thị: '\n' = xuống dòng cứng; đoạn văn thì tự xuống dòng theo bề rộng.</summary>
    private List<string> LayoutBlock(string text, TextBlockInfo block, TextStyle style, IntPtr preferredFont)
    {
        var hard = SplitLines(text);
        if (block.WrapWidth <= 0)
        {
            return [.. hard];
        }

        var lines = new List<string>();
        foreach (var paragraph in hard)
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = current + " " + words[i];
                var width = lines.Count == 0 ? block.WrapWidth - block.Indent : block.WrapWidth;
                if (MeasureText(candidate, style, preferredFont) > (width * WrapSlack) + 1)
                {
                    lines.Add(current);
                    current = words[i];
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);
        }

        return lines;
    }

    /// <summary>Bề rộng (point) của một dòng chữ với style + font sẽ dùng.</summary>
    private double MeasureText(string text, TextStyle style, IntPtr preferredFont)
    {
        var font = preferredFont != IntPtr.Zero ? preferredFont : GetFont(style, text);
        var obj = CreateTextObject(font, text, style, new FsMatrix { A = 1, D = 1 });
        try
        {
            return FPDFPageObj_GetBounds(obj, out var l, out _, out var r, out _) != 0
                ? r - l
                : text.Length * style.FontSize * 0.5;
        }
        finally
        {
            FPDFPageObj_Destroy(obj);
        }
    }

    /// <summary>Gỡ các object khỏi trang (chưa hủy), hoặc ở chế độ phủ: che từng object bằng màu nền.</summary>
    private static void RemoveOrCover(IntPtr page, IntPtr[] objs, bool overlay)
    {
        foreach (var obj in objs)
        {
            if (overlay)
            {
                if (FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
                {
                    throw new PdfException(PdfErrorKind.InvalidObject);
                }

                AddCover(page, new PdfBounds(l, b, r, t));
            }
            else if (FPDFPage_RemoveObject(page, obj) == 0)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }
        }
    }

    private static void DestroyAll(IntPtr[] objs)
    {
        foreach (var obj in objs)
        {
            FPDFPageObj_Destroy(obj);
        }
    }
}
