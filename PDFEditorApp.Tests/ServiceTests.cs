using PDFEditorApp.Models;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Settings, font, file, model nhỏ (docs/settings.md, text-edit.md, save.md).</summary>
public class ServiceTests
{
    [Fact]
    public void Settings_RoundTrip()
    {
        var path = Path.Combine(NewTempDir(), "sub", "settings.json");
        var service = new SettingsService(path);
        var settings = new AppSettings { Language = "vi", ExportDpi = 300 };
        settings.AddRecentFile(@"D:\a.pdf");
        Assert.True(service.Save(settings));

        var loaded = service.Load();
        Assert.Equal("vi", loaded.Language);
        Assert.Equal(300, loaded.ExportDpi);
        Assert.Equal([@"D:\a.pdf"], loaded.RecentFiles);
    }

    [Fact]
    public void Settings_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(NewTempDir(), "settings.json");
        File.WriteAllText(path, "{ not json");
        var loaded = new SettingsService(path).Load();
        Assert.Null(loaded.Language);
        Assert.Equal(150, loaded.ExportDpi);
    }

    [Fact]
    public void AppSettings_RecentFiles_DedupesAndLimits()
    {
        var s = new AppSettings();
        for (var i = 0; i < 15; i++)
        {
            s.AddRecentFile($"f{i}.pdf");
        }

        s.AddRecentFile("F3.PDF");
        Assert.Equal(AppSettings.MaxRecentFiles, s.RecentFiles.Count);
        Assert.Equal("F3.PDF", s.RecentFiles[0]);
        Assert.Single(s.RecentFiles, p => p.Equals("f3.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ABCDEF+TimesNewRomanPS-BoldMT", "Times New Roman")]
    [InlineData("Helvetica", "Arial")]
    [InlineData("ArialMT", "Arial")]
    [InlineData("CourierNewPSMT", "Courier New")]
    [InlineData("Calibri-Bold", "Calibri")]
    [InlineData("", "Arial")]
    public void FontService_MatchFamily(string pdfName, string expected)
    {
        Assert.Equal(expected, new FontService().MatchFamily(pdfName));
    }

    [Theory]
    [InlineData("Arial", false, false, "Helvetica")]
    [InlineData("Arial", true, true, "Helvetica-BoldOblique")]
    [InlineData("Times New Roman", false, false, "Times-Roman")]
    [InlineData("Times New Roman", false, true, "Times-Italic")]
    [InlineData("Courier New", true, false, "Courier-Bold")]
    [InlineData("Tahoma", false, false, null)]
    public void FontService_StandardFontName(string family, bool bold, bool italic, string? expected)
    {
        Assert.Equal(expected, new FontService().GetStandardFontName(family, bold, italic));
    }

    [Fact]
    public void FontService_Latin1Detection()
    {
        Assert.True(FontService.IsLatin1Text("Hello, café!"));
        Assert.False(FontService.IsLatin1Text("Tiếng Việt"));
    }

    [Fact]
    public void FontService_FindsWindowsFontFile()
    {
        var path = new FontService().GetFontFile("Arial", true, false);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void FileService_WriteAtomic_FailureKeepsOriginal()
    {
        var path = Path.Combine(NewTempDir(), "keep.txt");
        File.WriteAllText(path, "original");
        Assert.Throws<InvalidOperationException>(() => FileService.WriteAtomic(path, s =>
        {
            s.WriteByte(1);
            throw new InvalidOperationException("boom");
        }));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void RgbColor_HexRoundTrip()
    {
        Assert.True(RgbColor.TryParseHex("#2563eb", out var c));
        Assert.Equal(new RgbColor(0x25, 0x63, 0xEB), c);
        Assert.Equal("#2563EB", c.ToHex());
        Assert.False(RgbColor.TryParseHex("xyz", out _));
    }

    [Fact]
    public void Affine_Invert()
    {
        var m = new Affine(0, -2, 2, 0, 10, 20);
        var p = m.Invert().Transform(m.Transform(3, 4));
        Assert.Equal(3, p.X, 9);
        Assert.Equal(4, p.Y, 9);
    }
}
