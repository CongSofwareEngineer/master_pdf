using System.Text.RegularExpressions;
using System.Xml.Linq;
using PDFEditorApp.Models;
using PDFEditorApp.Views;
using PDFEditorApp.Views.Tools;
using Wpf.Ui.Controls;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>
/// Kiểm tra tài nguyên giao diện mà trình biên dịch không bắt được (docs/theme.md, docs/i18n.md):
/// tên icon trong XAML chỉ được đọc lúc chạy → sai tên sẽ làm app lỗi khi mở cửa sổ.
/// </summary>
public partial class UiResourceTests
{
    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(AppProjectDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static HashSet<string> Keys() =>
        XDocument.Load(Path.Combine(AppProjectDir, "Resources", "Strings.resx"))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

    [Fact]
    public void AllSymbolsInXaml_Exist()
    {
        var missing = new List<string>();
        foreach (var file in XamlFiles())
        {
            var text = File.ReadAllText(file);
            var names = SymbolAttribute().Matches(text).Select(m => m.Groups[1].Value)
                .Concat(SymbolExtension().Matches(text).Select(m => m.Groups[1].Value));
            missing.AddRange(names.Where(n => !Enum.TryParse<SymbolRegular>(n, out _)).Select(n => $"{Path.GetFileName(file)}: {n}"));
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void DynamicKeys_Exist()
    {
        var keys = Keys();
        var expected = Enum.GetNames<ToolId>().SelectMany(t => new[] { "Tool_" + t, "Hint_Tool_" + t })
            .Concat(Enum.GetNames<AnnotationKind>().Select(k => "Annot_" + k))
            .Concat(Enum.GetNames<PageObjectKind>().Select(k => "Obj_" + k))
            .Concat(Enum.GetNames<AppTheme>().Select(k => "Theme_" + k))
            .Concat(Enum.GetNames<StampPosition>().Select(k => "Position_" + k));
        var missing = expected.Where(k => !keys.Contains(k)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void KeysUsedIndirectlyInCode_Exist()
    {
        // Key truyền qua biến / toán tử điều kiện, vd. Loc.Get(watermark ? "Watermark_Title" : "HeaderFooter_Title").
        var keys = Keys();
        var prefixes = keys.Select(k => k[..(k.IndexOf('_', StringComparison.Ordinal) + 1)]).ToHashSet();
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppProjectDir, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (Match m in KeyLiteral().Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[1].Value;
                var prefix = key[..(key.IndexOf('_', StringComparison.Ordinal) + 1)];
                if (prefixes.Contains(prefix) && !key.EndsWith('_') && !keys.Contains(key))
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void ToolHintKeysCoverEveryTool() =>
        Assert.Equal(Enum.GetValues<ToolId>().Length, Enum.GetNames<ToolId>().Distinct().Count());

    [Fact]
    public void ExportFormats_AreAllHandled() =>
        Assert.Equal(4, Enum.GetValues<ExportFormat>().Length);

    [GeneratedRegex(@"\bSymbol=""([A-Za-z0-9]+)""")]
    private static partial Regex SymbolAttribute();

    [GeneratedRegex(@"\{ui:SymbolIcon\s+([A-Za-z0-9]+)\s*\}")]
    private static partial Regex SymbolExtension();

    [GeneratedRegex(@"""([A-Z][A-Za-z]+_[A-Za-z0-9_]+)""")]
    private static partial Regex KeyLiteral();
}
