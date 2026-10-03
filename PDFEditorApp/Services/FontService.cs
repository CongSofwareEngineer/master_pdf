namespace PDFEditorApp.Services;

/// <summary>
/// Chọn font cho text mới / text đã sửa (xem docs/text-edit.md):
/// - Text chỉ gồm ký tự Latin-1 + font có bản chuẩn PDF (Arial/Times/Courier) → dùng font chuẩn
///   (không nhúng, file nhỏ).
/// - Còn lại (vd. tiếng Việt) → nhúng file TrueType của Windows dưới dạng font CID (Unicode).
/// </summary>
public sealed class FontService
{
    private sealed record FontFamilyFiles(string Name, string Regular, string? Bold, string? Italic, string? BoldItalic, string? StandardName);

    // Các font phổ biến có sẵn trên Windows 10/11 (đều hỗ trợ tiếng Việt).
    private static readonly FontFamilyFiles[] KnownFamilies =
    [
        new("Arial", "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf", "Helvetica"),
        new("Times New Roman", "times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf", "Times"),
        new("Courier New", "cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf", "Courier"),
        new("Calibri", "calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf", null),
        new("Segoe UI", "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf", null),
        new("Tahoma", "tahoma.ttf", "tahomabd.ttf", null, null, null),
        new("Verdana", "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf", null),
        new("Georgia", "georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf", null),
        new(SymbolFamily, "seguisym.ttf", null, null, null, null),
        new(ScriptFamily, "segoesc.ttf", "segoescb.ttf", null, null, null),
        new("Segoe Print", "segoepr.ttf", "segoeprb.ttf", null, null, null),
        new("Ink Free", "Inkfree.ttf", null, null, null, null),
    ];

    /// <summary>Font có ký hiệu ✓ ✗ (Điền & Ký).</summary>
    public const string SymbolFamily = "Segoe UI Symbol";

    /// <summary>Font viết tay cho chữ ký gõ.</summary>
    public const string ScriptFamily = "Segoe Script";

    /// <summary>Các font viết tay có trên máy (cho hộp thoại chữ ký).</summary>
    public IReadOnlyList<string> HandwritingFamilies =>
        Families.Where(f => f is ScriptFamily or "Segoe Print" or "Ink Free").ToList();

    private readonly string[] _fontDirectories;
    private readonly List<FontFamilyFiles> _available;

    public FontService()
        : this([SystemService.FontsDirectory, SystemService.UserFontsDirectory])
    {
    }

    public FontService(IEnumerable<string> fontDirectories)
    {
        _fontDirectories = fontDirectories.Where(d => !string.IsNullOrEmpty(d)).ToArray();
        _available = KnownFamilies.Where(f => FindFile(f.Regular) is not null || f.StandardName is not null).ToList();
        if (_available.Count == 0)
        {
            _available.Add(KnownFamilies[0]);
        }
    }

    /// <summary>Danh sách font hiển thị trong ô chọn font.</summary>
    public IReadOnlyList<string> Families => _available.Select(f => f.Name).ToList();

    public string DefaultFamily => _available[0].Name;

    /// <summary>Text có thể ghi bằng font chuẩn PDF (bảng mã WinAnsi) hay không.</summary>
    public static bool IsLatin1Text(string text) =>
        text.All(c => (c >= 0x20 && c <= 0x7E) || (c >= 0xA0 && c <= 0xFF));

    /// <summary>Tên font chuẩn PDF (vd. "Helvetica-BoldOblique"), null nếu family không có bản chuẩn.</summary>
    public string? GetStandardFontName(string family, bool bold, bool italic)
    {
        var f = Find(family);
        if (f?.StandardName is null)
        {
            return null;
        }

        var italicWord = f.StandardName == "Times" ? "Italic" : "Oblique";
        return (bold, italic, f.StandardName) switch
        {
            (false, false, "Times") => "Times-Roman",
            (false, false, _) => f.StandardName,
            (true, false, _) => f.StandardName + "-Bold",
            (false, true, _) => f.StandardName + "-" + italicWord,
            _ => f.StandardName + "-Bold" + italicWord,
        };
    }

    /// <summary>Đường dẫn file .ttf phù hợp nhất; null nếu không tìm thấy.</summary>
    public string? GetFontFile(string family, bool bold, bool italic)
    {
        var f = Find(family) ?? _available[0];
        var candidates = (bold, italic) switch
        {
            (true, true) => new[] { f.BoldItalic, f.Bold, f.Italic, f.Regular },
            (true, false) => [f.Bold, f.Regular],
            (false, true) => [f.Italic, f.Regular],
            _ => [f.Regular],
        };

        foreach (var name in candidates)
        {
            if (name is not null && name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && FindFile(name) is { } path)
            {
                return path;
            }
        }

        // Font dự phòng hỗ trợ Unicode.
        return family == "Arial" ? null : GetFontFile("Arial", bold, italic);
    }

    /// <summary>Đoán family có trên máy gần nhất với tên font trong PDF (vd. "ABCDEF+TimesNewRomanPS-BoldMT").</summary>
    public string MatchFamily(string? pdfFontName)
    {
        var name = (pdfFontName ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
        string? match = null;
        if (Contains(name, "Times") || Contains(name, "Roman") || (Contains(name, "Serif") && !Contains(name, "Sans")))
        {
            match = "Times New Roman";
        }
        else if (Contains(name, "Courier") || Contains(name, "Mono"))
        {
            match = "Courier New";
        }
        else
        {
            foreach (var f in KnownFamilies)
            {
                if (Contains(name, f.Name.Replace(" ", string.Empty, StringComparison.Ordinal)))
                {
                    match = f.Name;
                    break;
                }
            }
        }

        match ??= "Arial";
        return Find(match)?.Name ?? DefaultFamily;
    }

    /// <summary>Nhận diện đậm / nghiêng từ tên font.</summary>
    public static (bool Bold, bool Italic) DetectStyle(string? pdfFontName)
    {
        var n = pdfFontName ?? string.Empty;
        var bold = Contains(n, "Bold") || Contains(n, "Black") || Contains(n, "Heavy") || Contains(n, "Semibold") || Contains(n, "Demi");
        var italic = Contains(n, "Italic") || Contains(n, "Oblique");
        return (bold, italic);
    }

    private FontFamilyFiles? Find(string family) =>
        _available.FirstOrDefault(f => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));

    private string? FindFile(string fileName)
    {
        foreach (var dir in _fontDirectories)
        {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static bool Contains(string text, string part) => text.Contains(part, StringComparison.OrdinalIgnoreCase);
}
