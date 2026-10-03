using System.Text.RegularExpressions;
using System.Xml.Linq;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>
/// Kiểm tra quy tắc đa ngôn ngữ (docs/i18n.md, CLAUDE.md):
/// mọi key có bản dịch vi, mọi key dùng trong code tồn tại, XAML không hard-code chữ.
/// </summary>
public partial class I18nTests
{
    private static readonly string[] TextAttributes =
        ["Text", "Content", "Header", "ToolTip", "Title", "Description", "Label", "PlaceholderText", "Message", "OnContent", "OffContent"];

    // Phần tử hiển thị chữ trực tiếp từ nội dung (không tính tài nguyên như <Color>, <FontFamily>).
    private static readonly string[] TextElements =
    [
        "TextBlock", "Run", "Label", "Button", "ToggleButton", "RadioButton", "CheckBox", "MenuItem",
        "Hyperlink", "ToolTip", "GroupBox", "TabItem", "Window", "Paragraph", "Span", "Bold", "Italic",
    ];

    private static Dictionary<string, string> LoadResx(string fileName) =>
        XDocument.Load(Path.Combine(AppProjectDir, "Resources", fileName))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppProjectDir, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Fact]
    public void EveryKey_HasVietnameseTranslation()
    {
        var en = LoadResx("Strings.resx");
        var vi = LoadResx("Strings.vi.resx");
        Assert.Empty(en.Keys.Except(vi.Keys));
        Assert.Empty(vi.Keys.Except(en.Keys));
        Assert.Empty(en.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key));
        Assert.Empty(vi.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key));
    }

    [Fact]
    public void FormatPlaceholders_MatchBetweenLanguages()
    {
        var en = LoadResx("Strings.resx");
        var vi = LoadResx("Strings.vi.resx");
        foreach (var (key, value) in en)
        {
            var a = Placeholder().Matches(value).Select(m => m.Groups[1].Value).Distinct().Order();
            var b = Placeholder().Matches(vi[key]).Select(m => m.Groups[1].Value).Distinct().Order();
            Assert.True(a.SequenceEqual(b), $"Placeholder khác nhau ở key {key}");
        }
    }

    [Fact]
    public void KeysUsedInXamlAndCode_Exist()
    {
        var keys = LoadResx("Strings.resx").Keys.ToHashSet();
        var missing = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            missing.AddRange(XamlKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value)
                .Where(k => !keys.Contains(k)).Select(k => $"{Path.GetFileName(file)}: {k}"));
        }

        foreach (var file in SourceFiles("*.cs"))
        {
            missing.AddRange(CodeKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value)
                .Where(k => !keys.Contains(k)).Select(k => $"{Path.GetFileName(file)}: {k}"));
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryErrorKind_HasMessage()
    {
        var keys = LoadResx("Strings.resx").Keys.ToHashSet();
        Assert.All(Enum.GetNames<PdfErrorKind>(), kind => Assert.Contains("Error_" + kind, keys));
    }

    [Fact]
    public void Xaml_HasNoHardCodedText()
    {
        var problems = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            var doc = XDocument.Load(file);
            foreach (var element in doc.Descendants())
            {
                foreach (var attr in element.Attributes().Where(a => TextAttributes.Contains(a.Name.LocalName)))
                {
                    if (!attr.Value.StartsWith('{') && attr.Value.Any(char.IsLetter))
                    {
                        problems.Add($"{Path.GetFileName(file)}: {element.Name.LocalName}.{attr.Name.LocalName}=\"{attr.Value}\"");
                    }
                }

                foreach (var text in element.Nodes().OfType<XText>().Where(_ => TextElements.Contains(element.Name.LocalName)))
                {
                    if (text.Value.Any(char.IsLetter))
                    {
                        problems.Add($"{Path.GetFileName(file)}: <{element.Name.LocalName}> \"{text.Value.Trim()}\"");
                    }
                }
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Localization_SwitchesLanguage()
    {
        var service = LocalizationService.Instance;
        var previous = service.Language;
        try
        {
            service.SetLanguage("vi");
            Assert.Equal("Lỗi", service["Error_Title"]);
            service.SetLanguage("en");
            Assert.Equal("Error", service["Error_Title"]);
            Assert.Equal("[No_Such_Key]", service["No_Such_Key"]);
        }
        finally
        {
            service.SetLanguage(previous);
        }
    }

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{v:Tr\s+(?:Key=)?([A-Za-z0-9_]+)\s*\}")]
    private static partial Regex XamlKey();

    [GeneratedRegex(@"Loc\.(?:Get|Format)\(\s*""([A-Za-z0-9_]+)""\s*[,)]")]
    private static partial Regex CodeKey();
}
