using System.Windows;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Tools;

/// <summary>Các công cụ của vùng xem (mỗi giá trị có chuỗi "Tool_&lt;Tên&gt;" trong Strings.resx).</summary>
public enum ToolId
{
    Select,
    Hand,
    EditContent,
    AddText,
    PlaceImage,
    Highlight,
    Underline,
    StrikeOut,
    Squiggly,
    Note,
    Ink,
    Rectangle,
    Ellipse,
    Line,
    Arrow,
    Eraser,
    Stamp,
    Redact,
}

/// <summary>Đối tượng đang chọn trong vùng xem.</summary>
public abstract record EditorSelection(int Page);

/// <summary>Đoạn chữ đang bôi đen [Start, End).</summary>
public sealed record TextRangeSelection(int Page, int Start, int End) : EditorSelection(Page);

/// <summary>Nhận xét đang chọn.</summary>
public sealed record AnnotationSelection(int Page, AnnotationInfo Annotation) : EditorSelection(Page);

/// <summary>Object (chữ / ảnh / hình) đang chọn trong chế độ sửa nội dung.</summary>
public sealed record ObjectSelection(int Page, PageObjectInfo Item, TextObjectInfo? Text) : EditorSelection(Page);

/// <summary>Loại dấu đặt nhanh (Điền & Ký).</summary>
public enum StampKind
{
    Check,
    Cross,
    Date,
    Signature,
}

/// <summary>Tùy chọn của công cụ (màu, độ dày, kiểu chữ…), chỉnh ở panel thuộc tính.</summary>
public sealed class ToolOptions
{
    public RgbColor MarkupColor { get; set; } = new(255, 212, 0);

    public RgbColor InkColor { get; set; } = new(229, 57, 53);

    public double InkWidth { get; set; } = 2;

    public double Opacity { get; set; } = 1;

    public bool FillShape { get; set; }

    public TextStyle TextStyle { get; set; } = TextStyle.Default;

    /// <summary>Ảnh chờ đặt (công cụ chèn ảnh).</summary>
    public ImageData? PendingImage { get; set; }

    public StampKind Stamp { get; set; } = StampKind.Check;

    public SavedSignature? Signature { get; set; }

    /// <summary>Vùng che thông tin chờ áp dụng theo trang (hệ view).</summary>
    public Dictionary<int, List<RectD>> Redactions { get; } = [];
}

/// <summary>Những gì công cụ cần từ workspace (dữ liệu trang đã cache, thao tác sửa, chọn…).</summary>
public interface IToolHost
{
    PdfService Pdf { get; }

    ToolOptions Options { get; }

    EditorSelection? Selection { get; }

    Window? OwnerWindow { get; }

    /// <summary>Dữ liệu trang đã cache; null = đang tải (workspace sẽ vẽ lại trang khi xong).</summary>
    PageTextLayout? GetTextLayout(int page);

    IReadOnlyList<LinkInfo>? GetLinks(int page);

    IReadOnlyList<AnnotationInfo>? GetAnnotations(int page);

    IReadOnlyList<PageObjectInfo>? GetObjects(int page);

    IReadOnlyList<TextObjectInfo>? GetTextObjects(int page);

    void SetSelection(EditorSelection? selection);

    /// <summary>Chạy thao tác sửa nền; hàm trả về chỉ số object / nhận xét mới (-1 nếu không có).</summary>
    Task EditAsync(int page, Func<int> edit, string statusKey, Action<int>? after = null);

    void NavigateToPage(int page);

    void OpenLink(string uri);

    void SetStatus(string text);

    /// <summary>Quay về công cụ mặc định (Chọn).</summary>
    void ActivateDefaultTool();

    /// <summary>Sau khi thêm object mới: chuyển sang công cụ sửa nội dung và chọn nó.</summary>
    void SelectObjectAfterEdit(int page, int objectIndex);
}
