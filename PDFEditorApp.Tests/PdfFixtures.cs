using System.Globalization;
using System.Text;
using PDFEditorApp.Models;

namespace PDFEditorApp.Tests;

/// <summary>Tạo PDF mẫu dạng văn bản (xref đúng offset) cho các tính năng PDFium không tự tạo được.</summary>
internal static class PdfFixtures
{
    /// <summary>objects[i] là nội dung của object (i+1). Object 1 phải là Catalog.</summary>
    public static byte[] Build(params string[] objects)
    {
        var sb = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = sb.Length;
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{o:D10} 00000 n \n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    public static string Stream(string dict, string content) =>
        $"<< {dict} /Length {content.Length} >>\nstream\n{content}\nendstream";

    /// <summary>
    /// 2 trang. Trang 1: chữ "Form test", ô text "name", ô đánh dấu "agree", liên kết tới trang 2,
    /// liên kết web. Mục lục: "Chapter 2" → trang 2.
    /// </summary>
    public static byte[] FormAndLinks() => Build(
        "<< /Type /Catalog /Pages 2 0 R /Outlines 7 0 R /AcroForm << /Fields [4 0 R 5 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 6 0 R >> >> >> >>",
        "<< /Type /Pages /Kids [3 0 R 9 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R 5 0 R 10 0 R 14 0 R] /Resources << /Font << /Helv 6 0 R >> >> /Contents 11 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /Rect [100 700 300 730] /DA (/Helv 12 Tf 0 g) /P 3 0 R /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /T (agree) /Rect [100 650 120 670] /V /Off /AS /Off /AP << /N << /Yes 12 0 R /Off 13 0 R >> >> /P 3 0 R /F 4 >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /Outlines /First 8 0 R /Last 8 0 R /Count 1 >>",
        "<< /Title (Chapter 2) /Parent 7 0 R /Dest [9 0 R /Fit] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>",
        "<< /Type /Annot /Subtype /Link /Rect [100 600 200 620] /Border [0 0 0] /Dest [9 0 R /Fit] >>",
        Stream(string.Empty, "BT /Helv 12 Tf 100 750 Td (Form test) Tj ET"),
        Stream("/Type /XObject /Subtype /Form /BBox [0 0 20 20]", "0 0 0 rg 4 4 12 12 re f"),
        Stream("/Type /XObject /Subtype /Form /BBox [0 0 20 20]", string.Empty),
        "<< /Type /Annot /Subtype /Link /Rect [100 560 200 580] /Border [0 0 0] /A << /S /URI /URI (https://example.com/) >> >>");

    /// <summary>Ảnh BGRA đặc một màu.</summary>
    public static ImageData SolidImage(int w, int h, byte r, byte g, byte b)
    {
        var pixels = new byte[w * h * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255;
        }

        return new ImageData(w, h, 72, 72, new RenderedPage(w, h, w * 4, pixels), null);
    }

    /// <summary>Màu pixel (R,G,B) tại điểm hệ view khi render ở 1 px/pt.</summary>
    public static (byte R, byte G, byte B) PixelAt(RenderedPage image, double x, double y)
    {
        var i = ((int)y * image.Stride) + ((int)x * 4);
        return (image.Pixels[i + 2], image.Pixels[i + 1], image.Pixels[i]);
    }
}
