using System.Text;
using System.Windows.Media.Imaging;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Export PNG / TXT và phân tích phạm vi trang (docs/export.md).</summary>
public class ExportTests
{
    [Theory]
    [InlineData("", 5, new[] { 0, 1, 2, 3, 4 })]
    [InlineData("1-3", 5, new[] { 0, 1, 2 })]
    [InlineData("2, 4", 5, new[] { 1, 3 })]
    [InlineData("4-, 1", 5, new[] { 0, 3, 4 })]
    [InlineData("-2", 5, new[] { 0, 1 })]
    [InlineData("3;3;1-2", 5, new[] { 0, 1, 2 })]
    public void PageRangeParser_ValidInput(string text, int pageCount, int[] expected)
    {
        Assert.True(PageRangeParser.TryParse(text, pageCount, out var pages));
        Assert.Equal(expected, pages);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("3-1")]
    [InlineData("abc")]
    [InlineData("1-2-3")]
    [InlineData(",")]
    public void PageRangeParser_InvalidInput(string text)
    {
        Assert.False(PageRangeParser.TryParse(text, 5, out _));
    }

    [Fact]
    public void PngEncoder_ProducesDecodablePng()
    {
        // Ảnh 3x2: pixel (0,0) đỏ, còn lại trắng.
        const int w = 3, h = 2, stride = w * 4;
        var pixels = Enumerable.Repeat((byte)255, stride * h).ToArray();
        pixels[0] = 0;
        pixels[1] = 0;
        pixels[2] = 255;
        using var ms = new MemoryStream();
        PngEncoder.Write(ms, new RenderedPage(w, h, stride, pixels), 150);

        ms.Position = 0;
        var frame = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(w, frame.PixelWidth);
        Assert.Equal(h, frame.PixelHeight);
        Assert.Equal(150, frame.DpiX, 0);

        var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var outPixels = new byte[stride * h];
        converted.CopyPixels(outPixels, stride, 0);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, outPixels[..4]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, outPixels[4..8]);
    }

    [Fact]
    public void PngEncoder_Crc32_MatchesKnownValue()
    {
        Assert.Equal(0xCBF43926u, PngEncoder.Crc32("123456789"u8));
    }

    [Fact]
    public void ExportPng_WritesOneFilePerPage()
    {
        var dir = NewTempDir();
        using var pdf = Open(CreatePdf(3));
        var files = new ExportService(pdf).ExportPng([0, 2], dir, "doc", 72);
        Assert.Equal(2, files.Count);
        Assert.EndsWith("doc_p01.png", files[0], StringComparison.Ordinal);
        Assert.EndsWith("doc_p03.png", files[1], StringComparison.Ordinal);
        Assert.All(files, f => Assert.True(new FileInfo(f).Length > 100));
    }

    [Fact]
    public void ExportText_WritesUtf8TextOfPages()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "out.txt");
        using var pdf = Open(CreatePdf(2, "Xin chào"));
        new ExportService(pdf).ExportText([0, 1], path);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var text = File.ReadAllText(path);
        Assert.Contains("----- 1 -----", text, StringComparison.Ordinal);
        Assert.Contains("----- 2 -----", text, StringComparison.Ordinal);
        Assert.Contains("Xin chào", text, StringComparison.Ordinal);
    }
}
