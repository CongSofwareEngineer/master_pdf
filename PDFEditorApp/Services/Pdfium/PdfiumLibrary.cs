namespace PDFEditorApp.Services.Pdfium;

/// <summary>
/// Khởi tạo PDFium một lần cho cả process và cung cấp lock chung.
/// PDFium KHÔNG thread-safe: mọi lệnh gọi native phải nằm trong <c>lock (PdfiumLibrary.Sync)</c>.
/// </summary>
internal static class PdfiumLibrary
{
    public static readonly object Sync = new();

    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            PdfiumNative.FPDF_InitLibrary();
            _initialized = true;
        }
    }
}
