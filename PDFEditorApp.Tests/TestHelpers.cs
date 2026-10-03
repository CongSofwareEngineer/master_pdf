using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Tests;

internal static class TestHelpers
{
    /// <summary>Thư mục gốc repo (chứa PDFEditorApp.sln).</summary>
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PDFEditorApp.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy PDFEditorApp.sln");
        }
    }

    public static string AppProjectDir => Path.Combine(RepoRoot, "PDFEditorApp");

    public static PdfService NewService() => new(new FontService());

    public static TextStyle Style(double size = 14, string family = "Arial", bool bold = false) =>
        new(family, size, bold, false, RgbColor.Black);

    /// <summary>Tạo PDF (bytes) gồm <paramref name="pages"/> trang A4, trang i có dòng chữ "Page i+1".</summary>
    public static byte[] CreatePdf(int pages = 1, string? text = null)
    {
        using var pdf = NewService();
        pdf.CreateNew();
        for (var i = 1; i < pages; i++)
        {
            pdf.InsertBlankPage(i, PdfService.A4Width, PdfService.A4Height);
        }

        for (var i = 0; i < pages; i++)
        {
            pdf.AddText(i, 72, 72, text ?? $"Page {i + 1}", Style());
        }

        return pdf.SaveToBytes();
    }

    public static PdfService Open(byte[] data)
    {
        var pdf = NewService();
        pdf.Open(data, null, null);
        return pdf;
    }

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PDFEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
