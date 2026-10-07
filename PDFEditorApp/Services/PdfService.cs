using System.Runtime.InteropServices;
using System.Text;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;

namespace PDFEditorApp.Services;

/// <summary>
/// Phiên làm việc với MỘT tài liệu PDF trên engine PDFium: mở, render, đọc/sửa text, quản lý trang,
/// hoàn tác/làm lại và lưu. Mọi phương thức public đều thread-safe (dùng lock chung của PDFium),
/// UI nên gọi qua <c>Task.Run</c> để không đơ giao diện.
/// Xem docs/pdf-engine.md, text-edit.md, page-manage.md, undo-redo.md, save.md.
/// </summary>
public sealed partial class PdfService : IDisposable
{
    /// <summary>Kích thước A4 theo point.</summary>
    public const double A4Width = 595.276;
    public const double A4Height = 841.89;

    /// <summary>Khoảng cách dòng khi text có nhiều dòng (bội số cỡ chữ).</summary>
    public const double LineSpacing = 1.25;

    /// <summary>Phần trên baseline của một dòng chữ (bội số cỡ chữ) — đặt text mới ngay dưới điểm click.</summary>
    public const double Ascent = 0.8;

    /// <summary>Mark gắn trên lớp che của chế độ sửa "phủ".</summary>
    private const string CoverMark = "PDFEditorCover";

    /// <summary>Mark gắn trên text object do app tạo.</summary>
    private const string OwnTextMark = "PDFEditorText";

    /// <summary>Mark gắn trên ảnh / object khác do app tạo.</summary>
    private const string OwnObjectMark = "PDFEditorObject";

    private const int MaxUndoSteps = 30;
    private const long MaxUndoBytes = 512L * 1024 * 1024;
    private const long MaxRenderPixels = 40_000_000;

    private readonly FontService _fonts;
    private readonly Dictionary<string, IntPtr> _fontCache = [];
    private readonly LinkedList<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();

    private IntPtr _doc;
    private IntPtr _docBuffer;
    private byte[]? _password;
    private IntPtr _activePage;
    private int _activePageIndex = -1;
    private long _undoBytes;
    private long _stateId;
    private long _savedStateId;
    private long _nextStateId;
    private string? _filePath;
    private IntPtr _form;
    private IntPtr _formInfo;
    private SecurityMode _securityMode;
    private SecurityOptions? _securityOptions;
    private bool _fontFixFailed;

    public PdfService(FontService fonts)
    {
        _fonts = fonts;
        PdfiumLibrary.EnsureInitialized();
    }

    private sealed record Snapshot(byte[] Data, long StateId);

    public FontService Fonts => _fonts;

    public bool IsOpen
    {
        get { lock (PdfiumLibrary.Sync) { return _doc != IntPtr.Zero; } }
    }

    /// <summary>Đường dẫn file đang mở; null với tài liệu mới chưa lưu.</summary>
    public string? FilePath
    {
        get { lock (PdfiumLibrary.Sync) { return _filePath; } }
    }

    public int PageCount
    {
        get { lock (PdfiumLibrary.Sync) { return _doc == IntPtr.Zero ? 0 : FPDF_GetPageCount(_doc); } }
    }

    /// <summary>Có thay đổi chưa lưu (so sánh trạng thái hiện tại với trạng thái lúc lưu gần nhất).</summary>
    public bool IsModified
    {
        get { lock (PdfiumLibrary.Sync) { return _doc != IntPtr.Zero && _stateId != _savedStateId; } }
    }

    public bool CanUndo
    {
        get { lock (PdfiumLibrary.Sync) { return _undo.Count > 0; } }
    }

    public bool CanRedo
    {
        get { lock (PdfiumLibrary.Sync) { return _redo.Count > 0; } }
    }

    // =====================================================================
    // Mở / tạo / đóng
    // =====================================================================

    /// <summary>Mở tài liệu từ bytes. Ném <see cref="PdfException"/> (Password nếu cần/sai mật khẩu).</summary>
    public void Open(byte[] data, string? password, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (PdfiumLibrary.Sync)
        {
            var passwordBytes = ToCString(password);
            var (doc, buffer) = LoadDocument(data, passwordBytes);
            CloseCore();
            _doc = doc;
            _docBuffer = buffer;
            _password = passwordBytes;
            InitForms();
            _filePath = filePath;
            _stateId = _savedStateId = ++_nextStateId;
        }
    }

    /// <summary>Tạo tài liệu mới gồm một trang trắng.</summary>
    public void CreateNew(double width = A4Width, double height = A4Height)
    {
        lock (PdfiumLibrary.Sync)
        {
            var doc = FPDF_CreateNewDocument();
            if (doc == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Unknown);
            }

            var page = FPDFPage_New(doc, 0, width, height);
            if (page != IntPtr.Zero)
            {
                FPDF_ClosePage(page);
            }

            CloseCore();
            _doc = doc;
            _filePath = null;
            InitForms();
            _stateId = _savedStateId = ++_nextStateId;
        }
    }

    public void Close()
    {
        lock (PdfiumLibrary.Sync)
        {
            CloseCore();
        }
    }

    public void Dispose() => Close();

    // =====================================================================
    // Đọc thông tin / render
    // =====================================================================

    public PdfDocumentInfo GetDocumentInfo()
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var version = FPDF_GetFileVersion(_doc, out var v) != 0 ? $"{v / 10}.{v % 10}" : "-";
            return new PdfDocumentInfo(
                FPDF_GetPageCount(_doc),
                version,
                GetMeta("Title"),
                GetMeta("Author"),
                GetMeta("Producer"));
        }
    }

    /// <summary>Kích thước hiển thị (point, đã tính xoay) của mọi trang — không cần load trang.</summary>
    public IReadOnlyList<(double Width, double Height)> GetPageSizes()
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var count = FPDF_GetPageCount(_doc);
            var result = new List<(double, double)>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(FPDF_GetPageSizeByIndexF(_doc, i, out var size) != 0
                    ? (size.Width, size.Height)
                    : (A4Width, A4Height));
            }

            return result;
        }
    }

    public PdfPageInfo GetPageInfo(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page => BuildPageInfo(page, pageIndex));
        }
    }

    /// <summary>
    /// Render trang thành ảnh BGRA. <paramref name="pixelsPerPoint"/> = số pixel cho mỗi point
    /// (vd. zoom 100% ở 96 DPI = 96/72). Ảnh quá lớn sẽ tự giảm độ phân giải.
    /// </summary>
    public RenderedPage RenderPage(int pageIndex, double pixelsPerPoint, bool forScreen = true, bool annotations = true)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page => RenderCore(page, pixelsPerPoint, forScreen, annotations,
                annotations ? _form : IntPtr.Zero, attachForm: page != _activePage));
        }
    }

    /// <summary>Toàn bộ text của trang (dùng cho export TXT).</summary>
    public string GetPageText(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page =>
            {
                var textPage = FPDFText_LoadPage(page);
                if (textPage == IntPtr.Zero)
                {
                    return string.Empty;
                }

                try
                {
                    var count = FPDFText_CountChars(textPage);
                    if (count <= 0)
                    {
                        return string.Empty;
                    }

                    var buffer = new byte[(count + 1) * 2];
                    var written = FPDFText_GetText(textPage, 0, count, buffer);
                    return written <= 1 ? string.Empty : NormalizeSpaces(Encoding.Unicode.GetString(buffer, 0, (written - 1) * 2));
                }
                finally
                {
                    FPDFText_ClosePage(textPage);
                }
            });
        }
    }

    /// <summary>
    /// Danh sách text object (chọn / sửa được) trên trang. Bỏ qua text đã bị "phủ" bởi lớp che của
    /// chế độ sửa an toàn (xem <see cref="EditTextObject"/>).
    /// </summary>
    public IReadOnlyList<TextObjectInfo> GetTextObjects(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var covers = GetCovers(page);
            var result = new List<TextObjectInfo>();
            var textPage = FPDFText_LoadPage(page);
            try
            {
                var count = FPDFPage_CountObjects(page);
                for (var i = 0; i < count; i++)
                {
                    var obj = FPDFPage_GetObject(page, i);
                    if (obj == IntPtr.Zero || FPDFPageObj_GetType(obj) != FPDF_PAGEOBJ_TEXT)
                    {
                        continue;
                    }

                    var text = GetObjectText(obj, textPage);
                    if (string.IsNullOrWhiteSpace(text) ||
                        FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
                    {
                        continue;
                    }

                    var bounds = new PdfBounds(l, b, r, t);
                    if (IsCovered(i, bounds, covers))
                    {
                        continue;
                    }

                    var (style, fontName) = ReadStyle(obj);
                    result.Add(new TextObjectInfo(i, text, bounds, info.ToView(bounds), style, fontName));
                }
            }
            finally
            {
                if (textPage != IntPtr.Zero)
                {
                    FPDFText_ClosePage(textPage);
                }
            }

            return result;
        }
    }

    // =====================================================================
    // Chỉnh sửa văn bản
    // =====================================================================

    /// <summary>
    /// Thêm text tại điểm (viewX, viewY) — tọa độ view (point, gốc trên-trái) là góc trên-trái của chữ.
    /// Trả về chỉ số object của dòng đầu tiên.
    /// </summary>
    public int AddText(int pageIndex, double viewX, double viewY, string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        return Mutate(() =>
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var toPage = info.ViewToPage;

            // Hướng chữ: luôn nằm ngang trên màn hình, kể cả khi trang bị xoay.
            var right = Normalize(toPage.TransformVector(1, 0));
            var up = Normalize(toPage.TransformVector(0, -1));

            var first = FPDFPage_CountObjects(page);
            var lines = SplitLines(text);
            for (var k = 0; k < lines.Length; k++)
            {
                if (lines[k].Length == 0)
                {
                    continue;
                }

                var origin = toPage.Transform(viewX, viewY + (style.FontSize * (Ascent + (k * LineSpacing))));
                var matrix = new FsMatrix
                {
                    A = (float)right.X,
                    B = (float)right.Y,
                    C = (float)up.X,
                    D = (float)up.Y,
                    E = (float)origin.X,
                    F = (float)origin.Y,
                };
                InsertTextLine(page, lines[k], style, matrix, IntPtr.Zero);
            }

            GenerateContent(page);
            if (!VerifyPage(page, pageIndex))
            {
                throw new PdfException(PdfErrorKind.Regeneration);
            }

            return first;
        });
    }

    /// <summary>
    /// Sửa nội dung / định dạng một text object, giữ vị trí & hướng. Text rỗng = xóa.
    /// Trả về chỉ số object mới (dòng đầu), -1 nếu đã xóa.
    /// </summary>
    public int UpdateTextObject(int pageIndex, int objectIndex, string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        return EditTextObject(pageIndex, objectIndex,
            (page, obj, overlay) => RewriteText(page, obj, text, style, default, overlay));
    }

    public void DeleteTextObject(int pageIndex, int objectIndex) =>
        EditTextObject(pageIndex, objectIndex,
            (page, obj, overlay) => RewriteText(page, obj, string.Empty, TextStyle.Default, default, overlay));

    /// <summary>
    /// Di chuyển text object một đoạn (dx, dy) tính theo hệ view (point).
    /// Trả về chỉ số object sau khi di chuyển (có thể khác chỉ số cũ ở chế độ sửa an toàn).
    /// </summary>
    public int MoveTextObject(int pageIndex, int objectIndex, double deltaViewX, double deltaViewY) =>
        EditTextObject(pageIndex, objectIndex, (page, obj, overlay) =>
        {
            var delta = BuildPageInfo(page, pageIndex).ViewToPage.TransformVector(deltaViewX, deltaViewY);
            if (!overlay)
            {
                FPDFPageObj_Transform(obj, 1, 0, 0, 1, delta.X, delta.Y);
                GenerateContent(page);
                return objectIndex;
            }

            var (style, _) = ReadStyle(obj);
            return RewriteText(page, obj, ReadBackText(page, obj), style, delta, overlay: true);
        });

    /// <summary>
    /// Khung chung cho sửa / xóa / di chuyển text có sẵn, tránh lỗi của PDFium khi tái tạo nội dung trang.
    /// <para>
    /// PDFium tái tạo cả content stream chứa object bị sửa, và gộp nhầm các font khác nhau nhưng trùng
    /// tên BaseFont (hay gặp ở PDF do Chrome / Skia tạo) → mất chữ ở chỗ khác trên trang. Vì vậy:
    /// </para>
    /// <list type="number">
    /// <item>Trang có font trùng tên (và object không phải do app tạo) → dùng ngay chế độ "phủ".</item>
    /// <item>Ngược lại sửa trực tiếp, rồi đọc lại trang để kiểm tra mọi text khác còn nguyên;
    /// nếu không → khôi phục snapshot và làm lại bằng chế độ "phủ".</item>
    /// </list>
    /// Chế độ "phủ": không đụng tới content stream gốc; vẽ hình chữ nhật màu nền lên text cũ và
    /// thêm text mới phía trên (object mới nằm trong stream mới).
    /// </summary>
    private int EditTextObject(int pageIndex, int objectIndex, Func<IntPtr, IntPtr, bool, int> edit) =>
        EditObject(pageIndex, objectIndex, textOnly: true, edit);

    /// <summary>
    /// Khung chung sửa object có sẵn (text, ảnh, hình). Trước khi sửa trang có font trùng tên, thử
    /// <see cref="FontNameFixer"/> để PDFium tái tạo đúng; không được mới dùng chế độ phủ.
    /// </summary>
    private int EditObject(int pageIndex, int objectIndex, bool textOnly, Func<IntPtr, IntPtr, bool, int> edit) =>
        EditObjects(pageIndex, [objectIndex], textOnly, (page, objs, overlay) => edit(page, objs[0], overlay));

    /// <summary>Như <see cref="EditObject"/> nhưng cho nhiều object cùng lúc (vd. cả khối chữ).</summary>
    private int EditObjects(int pageIndex, IReadOnlyList<int> objectIndices, bool textOnly, Func<IntPtr, IntPtr[], bool, int> edit)
    {
        if (objectIndices.Count == 0 || objectIndices.Distinct().Count() != objectIndices.Count)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var before = new Snapshot(SaveToBytesCore(), _stateId);
            int result;
            try
            {
                var page = ActivatePage(pageIndex);
                var objs = ResolveObjects(page, objectIndices, textOnly);
                var own = objs.All(IsOwnObject);
                if (!own && HasAmbiguousFonts(page) && TryFixFontNames(before.Data))
                {
                    page = ActivatePage(pageIndex);
                    objs = ResolveObjects(page, objectIndices, textOnly);
                }

                var overlay = !own && HasAmbiguousFonts(page);
                result = edit(page, objs, overlay);
                if (!VerifyPage(ActivatePage(pageIndex), pageIndex))
                {
                    if (overlay)
                    {
                        throw new PdfException(PdfErrorKind.Regeneration);
                    }

                    ReplaceDocument(before.Data);
                    page = ActivatePage(pageIndex);
                    objs = ResolveObjects(page, objectIndices, textOnly);
                    result = edit(page, objs, true);
                    if (!VerifyPage(page, pageIndex))
                    {
                        throw new PdfException(PdfErrorKind.Regeneration);
                    }
                }
            }
            catch
            {
                ReplaceDocument(before.Data);
                throw;
            }

            CommitMutation(before);
            return result;
        }
    }

    private static IntPtr[] ResolveObjects(IntPtr page, IReadOnlyList<int> objectIndices, bool textOnly) =>
        objectIndices.Select(i => textOnly ? GetTextObjectHandle(page, i) : GetObjectHandle(page, i)).ToArray();

    private static bool IsOwnObject(IntPtr obj) => HasMark(obj, OwnTextMark) || HasMark(obj, OwnObjectMark);

    /// <summary>Đổi tên font trùng (bỏ qua nếu đã thất bại một lần). True nếu tài liệu đã được thay.</summary>
    private bool TryFixFontNames(byte[] current)
    {
        if (_fontFixFailed || _password is not null)
        {
            return false;
        }

        var fixedBytes = FontNameFixer.MakeFontNamesUnique(current);
        if (fixedBytes is null)
        {
            _fontFixFailed = true;
            return false;
        }

        ReplaceDocument(fixedBytes);
        return true;
    }

    /// <summary>
    /// Thay text object bằng text mới (rỗng = chỉ xóa), dời thêm <paramref name="delta"/> (hệ trang).
    /// overlay = false: gỡ object cũ. overlay = true: giữ object cũ, phủ lớp che màu nền lên trên.
    /// </summary>
    private int RewriteText(IntPtr page, IntPtr obj, string text, TextStyle style, PointD delta, bool overlay)
    {
        if (FPDFPageObj_GetMatrix(obj, out var m) == 0)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        var scale = Math.Sqrt((m.C * m.C) + (m.D * m.D));
        if (scale < 1e-6)
        {
            scale = 1;
        }

        // Giữ font gốc nếu người dùng không đổi font/kiểu và mọi ký tự mới đã có trong text cũ
        // (font nhúng dạng subset chỉ chứa glyph đã dùng). Không áp dụng ở chế độ phủ vì font gốc
        // chính là thứ PDFium có thể gộp nhầm.
        var originalFont = IntPtr.Zero;
        if (!overlay)
        {
            var (oldStyle, _) = ReadStyle(obj);
            var oldText = ReadBackText(page, obj);
            var sameFont = string.Equals(style.FontFamily, oldStyle.FontFamily, StringComparison.Ordinal) &&
                           style.Bold == oldStyle.Bold && style.Italic == oldStyle.Italic;
            if (sameFont && text.All(c => c == '\n' || c == '\r' || oldText.Contains(c, StringComparison.Ordinal)))
            {
                originalFont = FPDFTextObj_GetFont(obj);
            }
        }

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

        try
        {
            var lines = SplitLines(text);
            var first = FPDFPage_CountObjects(page);
            var created = 0;
            for (var k = 0; k < lines.Length; k++)
            {
                if (lines[k].Length == 0)
                {
                    continue;
                }

                var offset = style.FontSize * LineSpacing * k;
                var matrix = new FsMatrix
                {
                    A = (float)(m.A / scale),
                    B = (float)(m.B / scale),
                    C = (float)(m.C / scale),
                    D = (float)(m.D / scale),
                    E = (float)(m.E + delta.X - (offset * m.C / scale)),
                    F = (float)(m.F + delta.Y - (offset * m.D / scale)),
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
                FPDFPageObj_Destroy(obj);
            }
        }
    }

    /// <summary>Thêm lớp che (hình chữ nhật màu nền, có mark <see cref="CoverMark"/>) lên vùng text.</summary>
    private static void AddCover(IntPtr page, PdfBounds bounds)
    {
        const float pad = 0.75f;
        var color = SampleBackground(page, bounds);
        var rect = FPDFPageObj_CreateNewRect(
            (float)bounds.Left - pad,
            (float)bounds.Bottom - pad,
            (float)(bounds.Right - bounds.Left) + (2 * pad),
            (float)(bounds.Top - bounds.Bottom) + (2 * pad));
        if (rect == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        _ = FPDFPageObj_SetFillColor(rect, color.R, color.G, color.B, 255);
        _ = FPDFPath_SetDrawMode(rect, FPDF_FILLMODE_WINDING, 0);
        _ = FPDFPageObj_AddMark(rect, ToCString(CoverMark)!);
        if (FPDFPage_InsertObject(page, rect) == 0)
        {
            FPDFPageObj_Destroy(rect);
            throw new PdfException(PdfErrorKind.InvalidObject);
        }
    }

    /// <summary>Màu nền quanh vùng text: render trang rồi lấy màu phổ biến nhất trên viền ngoài khung.</summary>
    private static RgbColor SampleBackground(IntPtr page, PdfBounds bounds)
    {
        var info = BuildPageInfo(page, -1);
        var image = RenderCore(page, 1.0, forScreen: false);
        var sx = image.Width / info.Width;
        var sy = image.Height / info.Height;
        var r = info.ToView(bounds);
        var x0 = (int)Math.Floor(r.X * sx) - 3;
        var y0 = (int)Math.Floor(r.Y * sy) - 3;
        var x1 = (int)Math.Ceiling(r.Right * sx) + 3;
        var y1 = (int)Math.Ceiling(r.Bottom * sy) + 3;

        var counts = new Dictionary<int, (int Count, long R, long G, long B)>();
        void Sample(int x, int y)
        {
            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
            {
                return;
            }

            var i = (y * image.Stride) + (x * 4);
            int b = image.Pixels[i], g = image.Pixels[i + 1], red = image.Pixels[i + 2];
            var key = ((red >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
            counts.TryGetValue(key, out var c);
            counts[key] = (c.Count + 1, c.R + red, c.G + g, c.B + b);
        }

        for (var x = x0; x <= x1; x++)
        {
            Sample(x, y0);
            Sample(x, y1);
        }

        for (var y = y0; y <= y1; y++)
        {
            Sample(x0, y);
            Sample(x1, y);
        }

        if (counts.Count == 0)
        {
            return new RgbColor(255, 255, 255);
        }

        var best = counts.Values.MaxBy(v => v.Count);
        return new RgbColor((byte)(best.R / best.Count), (byte)(best.G / best.Count), (byte)(best.B / best.Count));
    }

    /// <summary>Các lớp che (chỉ số object + khung) trên trang.</summary>
    private static List<(int Index, PdfBounds Bounds)> GetCovers(IntPtr page)
    {
        var covers = new List<(int, PdfBounds)>();
        var count = FPDFPage_CountObjects(page);
        for (var i = 0; i < count; i++)
        {
            var obj = FPDFPage_GetObject(page, i);
            if (obj != IntPtr.Zero && FPDFPageObj_GetType(obj) == FPDF_PAGEOBJ_PATH && HasMark(obj, CoverMark) &&
                FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) != 0)
            {
                covers.Add((i, new PdfBounds(l, b, r, t)));
            }
        }

        return covers;
    }

    /// <summary>Text bị che khi có lớp che vẽ SAU nó phủ ≥ 80% diện tích khung.</summary>
    private static bool IsCovered(int index, PdfBounds text, List<(int Index, PdfBounds Bounds)> covers)
    {
        var area = Math.Max(1e-6, (text.Right - text.Left) * (text.Top - text.Bottom));
        foreach (var (coverIndex, c) in covers)
        {
            if (coverIndex <= index)
            {
                continue;
            }

            var w = Math.Min(text.Right, c.Right) - Math.Max(text.Left, c.Left);
            var h = Math.Min(text.Top, c.Top) - Math.Max(text.Bottom, c.Bottom);
            if (w > 0 && h > 0 && w * h >= 0.8 * area)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Trang có ≥ 2 font khác nhau nhưng trùng tên BaseFont → PDFium sẽ gộp nhầm khi tái tạo.</summary>
    private static bool HasAmbiguousFonts(IntPtr page)
    {
        var fonts = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        var count = FPDFPage_CountObjects(page);
        for (var i = 0; i < count; i++)
        {
            var obj = FPDFPage_GetObject(page, i);
            if (obj == IntPtr.Zero || FPDFPageObj_GetType(obj) != FPDF_PAGEOBJ_TEXT)
            {
                continue;
            }

            var font = FPDFTextObj_GetFont(obj);
            if (font == IntPtr.Zero)
            {
                continue;
            }

            var name = ReadAnsi(FPDFFont_GetBaseFontName, font);
            if (fonts.TryGetValue(name, out var other) && other != font)
            {
                return true;
            }

            fonts[name] = font;
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra tái tạo nội dung: load lại trang từ content mới và so toàn bộ text với các object
    /// đang có trong bộ nhớ. Khác nhau = PDFium đã làm hỏng nội dung.
    /// </summary>
    private bool VerifyPage(IntPtr page, int pageIndex)
    {
        var expected = CollectTexts(page);
        var fresh = FPDF_LoadPage(_doc, pageIndex);
        if (fresh == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return expected.SequenceEqual(CollectTexts(fresh), StringComparer.Ordinal);
        }
        finally
        {
            FPDF_ClosePage(fresh);
        }
    }

    private static List<string> CollectTexts(IntPtr page)
    {
        var texts = new List<string>();
        var textPage = FPDFText_LoadPage(page);
        try
        {
            var count = FPDFPage_CountObjects(page);
            for (var i = 0; i < count; i++)
            {
                var obj = FPDFPage_GetObject(page, i);
                if (obj != IntPtr.Zero && FPDFPageObj_GetType(obj) == FPDF_PAGEOBJ_TEXT)
                {
                    texts.Add(GetObjectText(obj, textPage));
                }
            }
        }
        finally
        {
            if (textPage != IntPtr.Zero)
            {
                FPDFText_ClosePage(textPage);
            }
        }

        texts.Sort(StringComparer.Ordinal);
        return texts;
    }

    private static bool HasMark(IntPtr obj, string name)
    {
        var count = FPDFPageObj_CountMarks(obj);
        for (var i = 0; i < count; i++)
        {
            var mark = FPDFPageObj_GetMark(obj, (uint)i);
            if (mark == IntPtr.Zero || FPDFPageObjMark_GetName(mark, null, 0, out var size) == 0 || size <= 2)
            {
                continue;
            }

            var buffer = new byte[size];
            if (FPDFPageObjMark_GetName(mark, buffer, size, out _) != 0 &&
                Encoding.Unicode.GetString(buffer, 0, (int)size - 2) == name)
            {
                return true;
            }
        }

        return false;
    }

    // =====================================================================
    // Quản lý trang
    // =====================================================================

    /// <summary>Xoay trang thêm <paramref name="quarterTurns"/> × 90° theo chiều kim đồng hồ (âm = ngược).</summary>
    public void RotatePage(int pageIndex, int quarterTurns)
    {
        Mutate(() =>
        {
            InvalidateActivePage();
            var page = LoadPage(pageIndex);
            try
            {
                var rotation = (((FPDFPage_GetRotation(page) + quarterTurns) % 4) + 4) % 4;
                FPDFPage_SetRotation(page, rotation);
            }
            finally
            {
                FPDF_ClosePage(page);
            }

            return 0;
        });
    }

    public void DeletePage(int pageIndex)
    {
        Mutate(() =>
        {
            var count = FPDF_GetPageCount(_doc);
            if (count <= 1)
            {
                throw new PdfException(PdfErrorKind.LastPage);
            }

            CheckPageIndex(pageIndex);
            InvalidateActivePage();
            FPDFPage_Delete(_doc, pageIndex);
            return 0;
        });
    }

    /// <summary>Chèn trang trắng tại vị trí <paramref name="pageIndex"/> (0 = đầu tài liệu).</summary>
    public void InsertBlankPage(int pageIndex, double width, double height)
    {
        Mutate(() =>
        {
            var index = Math.Clamp(pageIndex, 0, FPDF_GetPageCount(_doc));
            InvalidateActivePage();
            var page = FPDFPage_New(_doc, index, width, height);
            if (page == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Page);
            }

            _ = FPDFPage_GenerateContent(page);
            FPDF_ClosePage(page);
            return 0;
        });
    }

    /// <summary>Chuyển trang <paramref name="fromIndex"/> tới vị trí <paramref name="toIndex"/>.</summary>
    public void MovePage(int fromIndex, int toIndex)
    {
        Mutate(() =>
        {
            CheckPageIndex(fromIndex);
            CheckPageIndex(toIndex);
            if (fromIndex == toIndex)
            {
                return 0;
            }

            InvalidateActivePage();
            if (FPDF_MovePages(_doc, [fromIndex], 1, toIndex) == 0)
            {
                throw new PdfException(PdfErrorKind.Page);
            }

            return 0;
        });
    }

    /// <summary>Chèn toàn bộ trang của một PDF khác vào vị trí <paramref name="insertIndex"/>. Trả về số trang đã chèn.</summary>
    public int ImportPages(byte[] data, string? password, int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var (src, buffer) = LoadDocument(data, ToCString(password));
            try
            {
                var count = FPDF_GetPageCount(src);
                Mutate(() =>
                {
                    InvalidateActivePage();
                    var index = Math.Clamp(insertIndex, 0, FPDF_GetPageCount(_doc));
                    if (FPDF_ImportPages(_doc, src, null, index) == 0)
                    {
                        throw new PdfException(PdfErrorKind.Page);
                    }

                    return 0;
                });
                return count;
            }
            finally
            {
                FPDF_CloseDocument(src);
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    // =====================================================================
    // Hoàn tác / làm lại
    // =====================================================================

    public bool Undo()
    {
        lock (PdfiumLibrary.Sync)
        {
            if (_undo.Last is not { } node)
            {
                return false;
            }

            var current = new Snapshot(SaveToBytesCore(), _stateId);
            _undo.RemoveLast();
            _undoBytes -= node.Value.Data.LongLength;
            ReplaceDocument(node.Value.Data);
            _stateId = node.Value.StateId;
            _redo.Push(current);
            return true;
        }
    }

    public bool Redo()
    {
        lock (PdfiumLibrary.Sync)
        {
            if (!_redo.TryPop(out var next))
            {
                return false;
            }

            var current = new Snapshot(SaveToBytesCore(), _stateId);
            ReplaceDocument(next.Data);
            _stateId = next.StateId;
            PushUndo(current);
            return true;
        }
    }

    // =====================================================================
    // Lưu
    // =====================================================================

    /// <summary>Lưu ra file (ghi nguyên tử) và đánh dấu tài liệu là "đã lưu".</summary>
    public void SaveToFile(string path)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var bytes = SaveWithSecurity();
            FileService.WriteAtomic(path, stream => stream.Write(bytes));
            _filePath = Path.GetFullPath(path);
            _savedStateId = _stateId;
        }
    }

    /// <summary>Ghi bản sao tài liệu vào stream (không đổi trạng thái "đã lưu").</summary>
    public void SaveTo(Stream stream)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            SaveCore(stream);
        }
    }

    public byte[] SaveToBytes()
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            return SaveToBytesCore();
        }
    }

    // =====================================================================
    // Nội bộ
    // =====================================================================

    /// <summary>
    /// Thực hiện một thao tác sửa: chụp snapshot trước (để undo); nếu thao tác lỗi thì khôi phục
    /// snapshot để tài liệu không ở trạng thái dở dang.
    /// </summary>
    private T Mutate<T>(Func<T> action)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var before = new Snapshot(SaveToBytesCore(), _stateId);
            T result;
            try
            {
                result = action();
            }
            catch
            {
                ReplaceDocument(before.Data);
                throw;
            }

            CommitMutation(before);
            return result;
        }
    }

    /// <summary>Ghi nhận một thao tác sửa thành công: lưu bước undo, xóa nhánh redo, đổi trạng thái.</summary>
    private void CommitMutation(Snapshot before)
    {
        PushUndo(before);
        _redo.Clear();
        _stateId = ++_nextStateId;
    }

    private void PushUndo(Snapshot snapshot)
    {
        _undo.AddLast(snapshot);
        _undoBytes += snapshot.Data.LongLength;
        while (_undo.Count > 1 && (_undo.Count > MaxUndoSteps || _undoBytes > MaxUndoBytes))
        {
            _undoBytes -= _undo.First!.Value.Data.LongLength;
            _undo.RemoveFirst();
        }
    }

    private static (IntPtr Doc, IntPtr Buffer) LoadDocument(byte[] data, byte[]? password)
    {
        if (data.Length == 0)
        {
            throw new PdfException(PdfErrorKind.Format);
        }

        // PDFium đọc lười từ buffer → buffer phải sống tới khi đóng tài liệu (bộ nhớ không quản lý).
        var buffer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, buffer, data.Length);
        var doc = FPDF_LoadMemDocument64(buffer, (nuint)data.Length, password);
        if (doc == IntPtr.Zero)
        {
            var error = FPDF_GetLastError();
            Marshal.FreeHGlobal(buffer);
            throw new PdfException(error switch
            {
                FPDF_ERR_FILE => PdfErrorKind.File,
                FPDF_ERR_FORMAT => PdfErrorKind.Format,
                FPDF_ERR_PASSWORD => PdfErrorKind.Password,
                FPDF_ERR_SECURITY => PdfErrorKind.Security,
                FPDF_ERR_PAGE => PdfErrorKind.Page,
                _ => PdfErrorKind.Unknown,
            });
        }

        return (doc, buffer);
    }

    /// <summary>Thay tài liệu hiện tại bằng bản từ snapshot (giữ đường dẫn, mật khẩu).</summary>
    private void ReplaceDocument(byte[] data)
    {
        var (doc, buffer) = LoadDocument(data, _password);
        CloseDocumentHandles();
        _doc = doc;
        _docBuffer = buffer;
        InitForms();
    }

    private void CloseCore()
    {
        CloseDocumentHandles();
        _undo.Clear();
        _redo.Clear();
        _undoBytes = 0;
        _password = null;
        _filePath = null;
        _securityMode = SecurityMode.Keep;
        _securityOptions = null;
        _fontFixFailed = false;
    }

    private void CloseDocumentHandles()
    {
        InvalidateActivePage();
        ExitForms();

        // Font phải đóng TRƯỚC khi đóng tài liệu.
        foreach (var font in _fontCache.Values)
        {
            FPDFFont_Close(font);
        }

        _fontCache.Clear();

        if (_doc != IntPtr.Zero)
        {
            FPDF_CloseDocument(_doc);
            _doc = IntPtr.Zero;
        }

        if (_docBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_docBuffer);
            _docBuffer = IntPtr.Zero;
        }
    }

    private void EnsureOpen()
    {
        if (_doc == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.NotOpen);
        }
    }

    private void CheckPageIndex(int pageIndex)
    {
        EnsureOpen();
        if (pageIndex < 0 || pageIndex >= FPDF_GetPageCount(_doc))
        {
            throw new PdfException(PdfErrorKind.Page);
        }
    }

    private IntPtr LoadPage(int pageIndex)
    {
        CheckPageIndex(pageIndex);
        var page = FPDF_LoadPage(_doc, pageIndex);
        return page == IntPtr.Zero ? throw new PdfException(PdfErrorKind.Page) : page;
    }

    /// <summary>
    /// Trang "đang hoạt động" được giữ mở để handle của các text object còn hợp lệ giữa các lần sửa.
    /// Các thao tác thay đổi cấu trúc trang sẽ đóng nó (<see cref="InvalidateActivePage"/>).
    /// </summary>
    private IntPtr ActivatePage(int pageIndex)
    {
        if (_activePage != IntPtr.Zero && _activePageIndex == pageIndex)
        {
            return _activePage;
        }

        var page = LoadPage(pageIndex);
        InvalidateActivePage();
        _activePage = page;
        _activePageIndex = pageIndex;
        if (_form != IntPtr.Zero)
        {
            PdfiumNativeExtra.FORM_OnAfterLoadPage(page, _form);
        }

        return page;
    }

    private void InvalidateActivePage()
    {
        if (_activePage != IntPtr.Zero)
        {
            if (_form != IntPtr.Zero)
            {
                PdfiumNativeExtra.FORM_OnBeforeClosePage(_activePage, _form);
            }

            FPDF_ClosePage(_activePage);
        }

        _activePage = IntPtr.Zero;
        _activePageIndex = -1;
    }

    /// <summary>Chạy hàm với handle trang: dùng trang đang hoạt động nếu trùng, không thì mở tạm rồi đóng.</summary>
    private T WithPage<T>(int pageIndex, Func<IntPtr, T> func)
    {
        if (_activePage != IntPtr.Zero && _activePageIndex == pageIndex)
        {
            return func(_activePage);
        }

        var page = LoadPage(pageIndex);
        try
        {
            return func(page);
        }
        finally
        {
            FPDF_ClosePage(page);
        }
    }

    private static PdfPageInfo BuildPageInfo(IntPtr page, int pageIndex)
    {
        double width = FPDF_GetPageWidthF(page);
        double height = FPDF_GetPageHeightF(page);
        var rotation = FPDFPage_GetRotation(page);

        // Lấy ma trận trang → view bằng cách chiếu 3 điểm qua FPDF_PageToDevice ở độ phân giải
        // cao (K pixel / point) để sai số làm tròn số nguyên không đáng kể. Cách này tự xử lý
        // CropBox lệch gốc và góc xoay của trang.
        const int K = 100;
        const double D = 100;
        var sizeX = Math.Max(1, (int)Math.Round(width * K));
        var sizeY = Math.Max(1, (int)Math.Round(height * K));
        _ = FPDF_PageToDevice(page, 0, 0, sizeX, sizeY, 0, 0, 0, out var x0, out var y0);
        _ = FPDF_PageToDevice(page, 0, 0, sizeX, sizeY, 0, D, 0, out var x1, out var y1);
        _ = FPDF_PageToDevice(page, 0, 0, sizeX, sizeY, 0, 0, D, out var x2, out var y2);
        var sx = sizeX / (width * K);
        var sy = sizeY / (height * K);
        var matrix = new Affine(
            (x1 - x0) / (D * K * sx),
            (y1 - y0) / (D * K * sy),
            (x2 - x0) / (D * K * sx),
            (y2 - y0) / (D * K * sy),
            x0 / (K * sx),
            y0 / (K * sy));
        return new PdfPageInfo(pageIndex, width, height, rotation, matrix);
    }

    private static RenderedPage RenderCore(
        IntPtr page, double pixelsPerPoint, bool forScreen, bool annotations = true, IntPtr form = default, bool attachForm = false)
    {
        double width = FPDF_GetPageWidthF(page);
        double height = FPDF_GetPageHeightF(page);
        var w = Math.Max(1, (int)Math.Round(width * pixelsPerPoint));
        var h = Math.Max(1, (int)Math.Round(height * pixelsPerPoint));
        if ((long)w * h > MaxRenderPixels)
        {
            var f = Math.Sqrt(MaxRenderPixels / ((double)w * h));
            w = Math.Max(1, (int)(w * f));
            h = Math.Max(1, (int)(h * f));
        }

        var bitmap = FPDFBitmap_Create(w, h, 0);
        if (bitmap == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.Render);
        }

        try
        {
            _ = FPDFBitmap_FillRect(bitmap, 0, 0, w, h, 0xFFFFFFFF);
            var flags = (annotations ? FPDF_ANNOT : 0) | (forScreen ? FPDF_LCD_TEXT : 0);
            FPDF_RenderPageBitmap(bitmap, page, 0, 0, w, h, 0, flags);
            if (form != IntPtr.Zero)
            {
                // Có môi trường form: PDFium chỉ vẽ ô form qua FPDF_FFLDraw (xem docs/forms.md).
                if (attachForm)
                {
                    PdfiumNativeExtra.FORM_OnAfterLoadPage(page, form);
                }

                PdfiumNativeExtra.FPDF_FFLDraw(form, bitmap, page, 0, 0, w, h, 0, flags);
                if (attachForm)
                {
                    PdfiumNativeExtra.FORM_OnBeforeClosePage(page, form);
                }
            }

            var stride = FPDFBitmap_GetStride(bitmap);
            var pixels = new byte[stride * h];
            Marshal.Copy(FPDFBitmap_GetBuffer(bitmap), pixels, 0, pixels.Length);

            // Định dạng BGRx: đặt alpha = 255 để dùng được như BGRA.
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return new RenderedPage(w, h, stride, pixels);
        }
        finally
        {
            FPDFBitmap_Destroy(bitmap);
        }
    }

    private static IntPtr GetTextObjectHandle(IntPtr page, int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= FPDFPage_CountObjects(page))
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        var obj = FPDFPage_GetObject(page, objectIndex);
        return obj == IntPtr.Zero || FPDFPageObj_GetType(obj) != FPDF_PAGEOBJ_TEXT
            ? throw new PdfException(PdfErrorKind.InvalidObject)
            : obj;
    }

    /// <summary>Đọc text của object qua text page (UTF-16LE).</summary>
    private static string GetObjectText(IntPtr obj, IntPtr textPage)
    {
        if (textPage == IntPtr.Zero)
        {
            return string.Empty;
        }

        var size = FPDFTextObj_GetText(obj, textPage, null, 0);
        if (size <= 2)
        {
            return string.Empty;
        }

        var buffer = new byte[size];
        _ = FPDFTextObj_GetText(obj, textPage, buffer, size);
        return NormalizeSpaces(Encoding.Unicode.GetString(buffer, 0, (int)size - 2));
    }

    private (TextStyle Style, string FontName) ReadStyle(IntPtr obj)
    {
        if (FPDFTextObj_GetFontSize(obj, out var fontSize) == 0)
        {
            fontSize = 0;
        }

        if (FPDFPageObj_GetMatrix(obj, out var m) == 0)
        {
            m = new FsMatrix { A = 1, D = 1 };
        }

        var scale = Math.Sqrt((m.C * m.C) + (m.D * m.D));
        if (scale < 1e-6)
        {
            scale = 1;
        }

        var font = FPDFTextObj_GetFont(obj);
        var baseName = font == IntPtr.Zero ? string.Empty : ReadAnsi(FPDFFont_GetBaseFontName, font);
        var familyName = font == IntPtr.Zero ? string.Empty : ReadAnsi(FPDFFont_GetFamilyName, font);
        var fontName = StripSubsetPrefix(string.IsNullOrEmpty(baseName) ? familyName : baseName);

        var (bold, italic) = FontService.DetectStyle(fontName);
        if (font != IntPtr.Zero)
        {
            bold |= FPDFFont_GetWeight(font) >= 600;
            if (FPDFFont_GetItalicAngle(font, out var angle) != 0 && angle != 0)
            {
                italic = true;
            }
        }

        var color = FPDFPageObj_GetFillColor(obj, out var r, out var g, out var b, out _) != 0
            ? new RgbColor((byte)r, (byte)g, (byte)b)
            : RgbColor.Black;

        var size = Math.Round(fontSize * scale, 1);
        var style = new TextStyle(_fonts.MatchFamily(fontName), size > 0 ? size : TextStyle.Default.FontSize, bold, italic, color);
        return (style, fontName);
    }

    /// <summary>Tạo một dòng text và chèn vào trang. Nếu font gốc không mã hóa được text → dùng font thay thế.</summary>
    private void InsertTextLine(IntPtr page, string line, TextStyle style, FsMatrix matrix, IntPtr preferredFont)
    {
        if (preferredFont != IntPtr.Zero)
        {
            var obj = CreateTextObject(preferredFont, line, style, matrix);
            if (FPDFPage_InsertObject(page, obj) != 0)
            {
                if (ReadBackText(page, obj) == line)
                {
                    return;
                }

                _ = FPDFPage_RemoveObject(page, obj);
            }

            FPDFPageObj_Destroy(obj);
        }

        var font = GetFont(style, line);
        var newObj = CreateTextObject(font, line, style, matrix);
        if (FPDFPage_InsertObject(page, newObj) == 0)
        {
            FPDFPageObj_Destroy(newObj);
            throw new PdfException(PdfErrorKind.InvalidObject);
        }
    }

    private IntPtr CreateTextObject(IntPtr font, string line, TextStyle style, FsMatrix matrix)
    {
        var obj = FPDFPageObj_CreateTextObj(_doc, font, (float)style.FontSize);
        if (obj == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.Font);
        }

        if (FPDFText_SetText(obj, line) == 0)
        {
            FPDFPageObj_Destroy(obj);
            throw new PdfException(PdfErrorKind.Font);
        }

        _ = FPDFTextObj_SetTextRenderMode(obj, FPDF_TEXTRENDERMODE_FILL);
        _ = FPDFPageObj_AddMark(obj, ToCString(OwnTextMark)!);
        _ = FPDFPageObj_SetFillColor(obj, style.Color.R, style.Color.G, style.Color.B, 255);
        _ = FPDFPageObj_SetMatrix(obj, ref matrix);
        return obj;
    }

    private static string ReadBackText(IntPtr page, IntPtr obj)
    {
        var textPage = FPDFText_LoadPage(page);
        if (textPage == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            return GetObjectText(obj, textPage);
        }
        finally
        {
            FPDFText_ClosePage(textPage);
        }
    }

    /// <summary>Lấy (và cache theo tài liệu) font phù hợp với style + nội dung text.</summary>
    private IntPtr GetFont(TextStyle style, string text)
    {
        if (FontService.IsLatin1Text(text) &&
            _fonts.GetStandardFontName(style.FontFamily, style.Bold, style.Italic) is { } standardName)
        {
            var key = "std:" + standardName;
            if (!_fontCache.TryGetValue(key, out var std))
            {
                std = FPDFText_LoadStandardFont(_doc, ToCString(standardName)!);
                if (std == IntPtr.Zero)
                {
                    throw new PdfException(PdfErrorKind.Font);
                }

                _fontCache[key] = std;
            }

            return std;
        }

        var path = _fonts.GetFontFile(style.FontFamily, style.Bold, style.Italic)
                   ?? throw new PdfException(PdfErrorKind.Font);
        var ttfKey = "ttf:" + path;
        if (_fontCache.TryGetValue(ttfKey, out var cached))
        {
            return cached;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PdfException(PdfErrorKind.Font, ex);
        }

        IntPtr font;
        unsafe
        {
            fixed (byte* p = data)
            {
                // cid = 1: font CID (Identity-H) → hỗ trợ Unicode (tiếng Việt). PDFium tự copy dữ liệu font.
                font = FPDFText_LoadFont(_doc, (IntPtr)p, (uint)data.Length, FPDF_FONT_TRUETYPE, 1);
            }
        }

        if (font == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.Font);
        }

        _fontCache[ttfKey] = font;
        return font;
    }

    private static void GenerateContent(IntPtr page)
    {
        if (FPDFPage_GenerateContent(page) == 0)
        {
            throw new PdfException(PdfErrorKind.Save);
        }
    }

    private byte[] SaveToBytesCore()
    {
        using var ms = new MemoryStream();
        SaveCore(ms);
        return ms.ToArray();
    }

    /// <summary>Bytes để ghi ra file, áp dụng chế độ bảo mật (giữ / gỡ / đặt mật khẩu mới).</summary>
    private byte[] SaveWithSecurity()
    {
        if (_securityMode == SecurityMode.Keep)
        {
            return SaveToBytesCore();
        }

        using var ms = new MemoryStream();
        SaveCore(ms, FPDF_NO_INCREMENTAL | PdfiumNativeExtra.FPDF_REMOVE_SECURITY);
        var plain = ms.ToArray();
        return _securityMode == SecurityMode.Apply && _securityOptions is { } options
            ? SecurityService.Encrypt(plain, options)
            : plain;
    }

    private void SaveCore(Stream stream) => SaveCore(stream, FPDF_NO_INCREMENTAL);

    private void SaveCore(Stream stream, uint flags) => SaveDocument(_doc, stream, flags);

    private static unsafe void SaveDocument(IntPtr doc, Stream stream, uint flags)
    {
        Exception? error = null;
        WriteBlockCallback callback = (_, data, size) =>
        {
            try
            {
                stream.Write(new ReadOnlySpan<byte>((void*)data, checked((int)size)));
                return 1;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException or OverflowException)
            {
                error = ex;
                return 0;
            }
        };

        var fileWrite = new FpdfFileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
        var ok = FPDF_SaveAsCopy(doc, ref fileWrite, flags);
        GC.KeepAlive(callback);
        if (error is not null)
        {
            throw new PdfException(PdfErrorKind.Save, error);
        }

        if (ok == 0)
        {
            throw new PdfException(PdfErrorKind.Save);
        }
    }

    private string GetMeta(string tag)
    {
        var tagBytes = ToCString(tag)!;
        var size = FPDF_GetMetaText(_doc, tagBytes, null, 0);
        if (size <= 2)
        {
            return string.Empty;
        }

        var buffer = new byte[size];
        _ = FPDF_GetMetaText(_doc, tagBytes, buffer, size);
        return Encoding.Unicode.GetString(buffer, 0, (int)size - 2).Trim();
    }

    private static string ReadAnsi(Func<IntPtr, byte[]?, nuint, nuint> getter, IntPtr font)
    {
        var size = getter(font, null, 0);
        if (size <= 1)
        {
            return string.Empty;
        }

        var buffer = new byte[(int)size];
        _ = getter(font, buffer, size);
        return Encoding.Latin1.GetString(buffer, 0, (int)size - 1);
    }

    /// <summary>Bỏ tiền tố subset "ABCDEF+" của tên font nhúng.</summary>
    private static string StripSubsetPrefix(string name) =>
        name.Length > 7 && name[6] == '+' && name.Take(6).All(char.IsUpper) ? name[7..] : name;

    private static byte[]? ToCString(string? value) =>
        value is null ? null : Encoding.UTF8.GetBytes(value + "\0");

    /// <summary>PDFium trả khoảng trắng của font CID nhúng thành U+00A0; đổi về dấu cách thường.</summary>
    private static string NormalizeSpaces(string text) => text.Replace(' ', ' ');

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static PointD Normalize(PointD v)
    {
        var len = Math.Sqrt((v.X * v.X) + (v.Y * v.Y));
        return len < 1e-12 ? new PointD(1, 0) : new PointD(v.X / len, v.Y / len);
    }
}
