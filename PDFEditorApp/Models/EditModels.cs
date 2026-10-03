namespace PDFEditorApp.Models;

/// <summary>Loại nhận xét (annotation) app hiển thị / tạo được.</summary>
public enum AnnotationKind
{
    Other,
    Note,
    FreeText,
    Line,
    Square,
    Circle,
    Polygon,
    Highlight,
    Underline,
    Squiggly,
    StrikeOut,
    Stamp,
    Ink,
}

/// <summary>Một nhận xét trên trang (xem docs/annotations.md).</summary>
public sealed record AnnotationInfo(
    int PageIndex,
    int Index,
    AnnotationKind Kind,
    RectD ViewRect,
    RgbColor Color,
    string Contents,
    string Author,
    string Modified);

/// <summary>Loại page object (xem docs/page-objects.md).</summary>
public enum PageObjectKind
{
    Unknown,
    Text,
    Path,
    Image,
    Shading,
    Form,
}

/// <summary>Một object cấp trang (chữ, ảnh, hình vẽ…) với khung trong hệ view.</summary>
public sealed record PageObjectInfo(int Index, PageObjectKind Kind, RectD ViewRect);

/// <summary>Loại ô form.</summary>
public enum FormFieldKind
{
    Unknown,
    PushButton,
    CheckBox,
    RadioButton,
    ComboBox,
    ListBox,
    Text,
}

/// <summary>Một ô form (widget) trên trang (xem docs/forms.md).</summary>
public sealed record FormFieldInfo(
    int AnnotIndex,
    FormFieldKind Kind,
    string Name,
    string Value,
    bool IsChecked,
    bool ReadOnly,
    bool Multiline,
    bool Password,
    IReadOnlyList<string> Options,
    int SelectedIndex,
    RectD ViewRect);

/// <summary>Ảnh để chèn: hoặc dữ liệu JPEG gốc (giữ nén), hoặc bitmap BGRA.</summary>
public sealed record ImageData(int PixelWidth, int PixelHeight, double DpiX, double DpiY, RenderedPage? Bitmap, byte[]? Jpeg);

/// <summary>Vị trí đầu / chân trang.</summary>
public enum StampPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>Watermark chữ (xem docs/watermark.md). <see cref="Pages"/> null = mọi trang.</summary>
public sealed record WatermarkOptions(
    string Text,
    string FontFamily,
    double FontSize,
    RgbColor Color,
    double Opacity,
    double RotationDegrees,
    IReadOnlyList<int>? Pages);

/// <summary>
/// Đầu / chân trang (xem docs/header-footer.md). <see cref="Template"/> có thể chứa {n} (số trang),
/// {N} (tổng số trang), {date} (ngày hôm nay).
/// </summary>
public sealed record HeaderFooterOptions(
    string Template,
    StampPosition Position,
    string FontFamily,
    double FontSize,
    RgbColor Color,
    double Margin,
    IReadOnlyList<int>? Pages);

/// <summary>Mật khẩu & quyền khi lưu (xem docs/protect.md).</summary>
public sealed record SecurityOptions(string UserPassword, string OwnerPassword, bool AllowPrint, bool AllowCopy, bool AllowEdit);

/// <summary>Cách xử lý bảo mật khi lưu.</summary>
public enum SecurityMode
{
    /// <summary>Giữ nguyên bảo mật của file gốc.</summary>
    Keep,

    /// <summary>Gỡ mật khẩu.</summary>
    Remove,

    /// <summary>Đặt mật khẩu mới (<see cref="SecurityOptions"/>).</summary>
    Apply,
}

/// <summary>Chữ ký đã lưu (xem docs/sign.md). Vẽ: <see cref="Strokes"/> (toạ độ 0..1); gõ: <see cref="Text"/>.</summary>
public sealed class SavedSignature
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public List<List<PointD>>? Strokes { get; set; }

    /// <summary>Tỉ lệ rộng / cao của chữ ký vẽ.</summary>
    public double AspectRatio { get; set; } = 3;

    public string? Text { get; set; }

    public string? FontFamily { get; set; }

    public string Color { get; set; } = "#1E3A8A";
}
