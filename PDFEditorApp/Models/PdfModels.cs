namespace PDFEditorApp.Models;

/// <summary>
/// Thông tin một trang. <see cref="Width"/>/<see cref="Height"/> là kích thước hiển thị (point, đã tính
/// góc xoay). <see cref="PageToView"/> đổi tọa độ trang PDF sang hệ view (point, gốc trên-trái, Y xuống).
/// </summary>
public sealed record PdfPageInfo(int Index, double Width, double Height, int Rotation, Affine PageToView)
{
    /// <summary>Góc xoay theo độ (0, 90, 180, 270).</summary>
    public int RotationDegrees => Rotation * 90;

    public Affine ViewToPage { get; } = PageToView.Invert();

    public RectD ToView(PdfBounds bounds) => RectD.FromPoints(bounds.Corners().Select(PageToView.Transform));
}

/// <summary>Định dạng chữ dùng khi sửa / thêm text.</summary>
public sealed record TextStyle(string FontFamily, double FontSize, bool Bold, bool Italic, RgbColor Color)
{
    public static TextStyle Default { get; } = new("Arial", 14, false, false, RgbColor.Black);
}

/// <summary>Một text object trên trang (đơn vị chọn / sửa của chức năng chỉnh sửa văn bản).</summary>
/// <param name="ObjectIndex">Chỉ số object trong trang (dùng để gọi lại PdfService).</param>
/// <param name="Text">Nội dung chữ.</param>
/// <param name="Bounds">Khung bao trong hệ tọa độ trang.</param>
/// <param name="ViewBounds">Khung bao trong hệ view (point, gốc trên-trái).</param>
/// <param name="Style">Định dạng nhận diện được (font gần nhất có trên máy).</param>
/// <param name="FontName">Tên font gốc trong PDF.</param>
public sealed record TextObjectInfo(
    int ObjectIndex,
    string Text,
    PdfBounds Bounds,
    RectD ViewBounds,
    TextStyle Style,
    string FontName);

/// <summary>
/// Khối chữ = đơn vị chọn / sửa của chức năng chỉnh sửa văn bản: một dòng hoặc một đoạn văn gộp từ nhiều
/// text object liền mạch (xem docs/text-edit.md, <c>TextBlockBuilder</c>).
/// </summary>
/// <param name="ObjectIndices">Chỉ số các text object, theo thứ tự đọc (phần tử đầu = mảnh đầu dòng đầu).</param>
/// <param name="Text">Nội dung: dòng ngắt mềm (tự xuống dòng) nối bằng dấu cách, ngắt cứng bằng '\n'.</param>
/// <param name="ViewBounds">Khung bao cả khối trong hệ view.</param>
/// <param name="Style">Định dạng chiếm đa số trong khối.</param>
/// <param name="FontName">Tên font gốc (của định dạng chiếm đa số).</param>
/// <param name="LineCount">Số dòng gốc.</param>
/// <param name="LineHeight">Khoảng cách giữa 2 baseline (point); 0 = khối 1 dòng.</param>
/// <param name="WrapWidth">Bề rộng tự xuống dòng khi ghi lại (point, tính từ lề các dòng sau); 0 = không tự xuống dòng.</param>
/// <param name="Indent">Dòng đầu thụt vào so với các dòng sau (point, có thể âm).</param>
public sealed record TextBlockInfo(
    IReadOnlyList<int> ObjectIndices,
    string Text,
    RectD ViewBounds,
    TextStyle Style,
    string FontName,
    int LineCount,
    double LineHeight,
    double WrapWidth,
    double Indent)
{
    public int FirstObjectIndex => ObjectIndices[0];

    public bool Contains(int objectIndex) => ObjectIndices.Contains(objectIndex);
}

/// <summary>Ảnh render của một trang: pixel BGRA (32bpp, alpha luôn 255).</summary>
public sealed record RenderedPage(int Width, int Height, int Stride, byte[] Pixels);

/// <summary>Thông tin chung của tài liệu.</summary>
public sealed record PdfDocumentInfo(int PageCount, string Version, string Title, string Author, string Producer);
