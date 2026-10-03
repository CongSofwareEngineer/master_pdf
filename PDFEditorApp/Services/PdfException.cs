namespace PDFEditorApp.Services;

/// <summary>Loại lỗi PDF; UI đổi sang thông báo đa ngôn ngữ (key "Error_&lt;Kind&gt;").</summary>
public enum PdfErrorKind
{
    Unknown,
    File,
    Format,
    Password,
    Security,
    Page,
    Save,
    Font,
    Render,
    LastPage,
    NotOpen,
    InvalidObject,

    /// <summary>PDFium không tái tạo được nội dung trang mà không làm hỏng phần khác.</summary>
    Regeneration,
}

public sealed class PdfException : Exception
{
    public PdfException()
        : this(PdfErrorKind.Unknown)
    {
    }

    public PdfException(string message)
        : base(message)
    {
    }

    public PdfException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PdfException(PdfErrorKind kind, Exception? innerException = null)
        : base($"PDF error: {kind}", innerException)
    {
        Kind = kind;
    }

    public PdfErrorKind Kind { get; }
}
