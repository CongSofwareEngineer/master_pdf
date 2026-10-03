using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Quy tắc kiến trúc trong CLAUDE.md.</summary>
public class ArchitectureTests
{
    [Theory]
    [InlineData("Models")]
    [InlineData("Services")]
    public void ModelsAndServices_DoNotUseWpf(string folder)
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(AppProjectDir, folder), "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("System.Windows", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Code_DoesNotHardCodeDriveLetters()
    {
        var offenders = Directory.EnumerateFiles(AppProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"""[A-Za-z]:\\\\"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }
}
