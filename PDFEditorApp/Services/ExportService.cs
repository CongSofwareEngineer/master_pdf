using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PDFEditorApp.Models;

namespace PDFEditorApp.Services;

/// <summary>Xuất PDF sang ảnh (PNG / JPG), văn bản TXT, Word DOCX. Xem docs/export.md.</summary>
public sealed class ExportService(PdfService pdf)
{
    /// <summary>
    /// Xuất các trang (chỉ số từ 0) thành PNG trong <paramref name="folder"/>, tên file
    /// "&lt;baseName&gt;_p&lt;số trang&gt;.png". Trả về danh sách file đã ghi.
    /// </summary>
    public IReadOnlyList<string> ExportPng(
        IReadOnlyList<int> pages,
        string folder,
        string baseName,
        int dpi,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default) =>
        ExportImages(pages, folder, baseName, dpi, ".png", PngEncoder.Write, progress, cancellationToken);

    /// <summary>Xuất ảnh với bộ mã hóa tùy chọn (vd. JPEG do tầng View cung cấp).</summary>
    public IReadOnlyList<string> ExportImages(
        IReadOnlyList<int> pages,
        string folder,
        string baseName,
        int dpi,
        string extension,
        Action<Stream, RenderedPage, int> encode,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(encode);
        Directory.CreateDirectory(folder);
        var digits = Math.Max(2, pdf.PageCount.ToString(CultureInfo.InvariantCulture).Length);
        var files = new List<string>(pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageNumber = (pages[i] + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
            var path = Path.Combine(folder, $"{baseName}_p{pageNumber}{extension}");
            var image = pdf.RenderPage(pages[i], dpi / 72.0, forScreen: false);
            FileService.WriteAtomic(path, stream => encode(stream, image, dpi));
            files.Add(path);
            progress?.Report(i + 1);
        }

        return files;
    }

    /// <summary>Xuất text của các trang ra file UTF-8; giữa các trang có dòng phân cách "----- n -----".</summary>
    public void ExportText(IReadOnlyList<int> pages, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var sb = new StringBuilder();
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sb.Length > 0)
            {
                sb.AppendLine().AppendLine();
            }

            sb.Append(CultureInfo.InvariantCulture, $"----- {page + 1} -----").AppendLine();
            sb.Append(pdf.GetPageText(page).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
        FileService.WriteAtomic(filePath, stream => stream.Write(bytes));
    }

    /// <summary>
    /// Xuất Word (.docx): mỗi dòng chữ của PDF gộp thành đoạn văn (theo khoảng cách dòng), giữ cỡ chữ gần
    /// đúng, ngắt trang giữa các trang PDF. Không giữ bố cục phức tạp (bảng, cột, ảnh).
    /// </summary>
    public void ExportDocx(IReadOnlyList<int> pages, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        using var ms = new MemoryStream();
        using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);
            for (var p = 0; p < pages.Count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (p > 0)
                {
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                foreach (var (text, size) in BuildParagraphs(pdf.GetTextLayout(pages[p])))
                {
                    var halfPoints = Math.Clamp((int)Math.Round(size * 2), 12, 144).ToString(CultureInfo.InvariantCulture);
                    var runProperties = new RunProperties(new FontSize { Val = halfPoints }, new FontSizeComplexScript { Val = halfPoints });
                    body.Append(new Paragraph(new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
                }
            }

            main.Document.Save();
        }

        var bytes = ms.ToArray();
        FileService.WriteAtomic(filePath, stream => stream.Write(bytes));
    }

    /// <summary>Gộp dòng thành đoạn: dòng kế tiếp sát nhau (khoảng cách &lt; 0.9 chiều cao dòng) thì nối vào đoạn.</summary>
    internal static List<(string Text, double Size)> BuildParagraphs(PageTextLayout layout)
    {
        var lines = new List<(string Text, double Top, double Bottom, double Height)>();
        var start = 0;
        for (var i = 0; i <= layout.Length; i++)
        {
            if (i < layout.Length && layout.Text[i] != '\n')
            {
                continue;
            }

            var text = layout.GetRangeText(start, i).TrimEnd('\r');
            var boxes = Enumerable.Range(start, i - start).Select(k => layout.Boxes[k]).Where(b => b.Height > 0).ToList();
            if (text.Trim().Length > 0 && boxes.Count > 0)
            {
                lines.Add((text.Trim(), boxes.Min(b => b.Y), boxes.Max(b => b.Bottom), boxes.Average(b => b.Height)));
            }

            start = i + 1;
        }

        var paragraphs = new List<(string, double)>();
        var current = new StringBuilder();
        double size = 0, lastBottom = double.MinValue, lastHeight = 0;
        foreach (var line in lines)
        {
            var gap = line.Top - lastBottom;
            if (current.Length > 0 && (gap > lastHeight * 0.9 || gap < -lastHeight))
            {
                paragraphs.Add((current.ToString(), size));
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            else
            {
                size = line.Height * 0.78;
            }

            current.Append(line.Text);
            lastBottom = line.Bottom;
            lastHeight = line.Height;
        }

        if (current.Length > 0)
        {
            paragraphs.Add((current.ToString(), size));
        }

        return paragraphs;
    }
}
