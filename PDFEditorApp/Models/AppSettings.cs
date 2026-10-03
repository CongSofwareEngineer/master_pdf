namespace PDFEditorApp.Models;

/// <summary>Giao diện tối / sáng (xem docs/theme.md).</summary>
public enum AppTheme
{
    Dark,
    Light,
    System,
}

/// <summary>Cài đặt người dùng, lưu ở settings.json (xem docs/settings.md).</summary>
public sealed class AppSettings
{
    public const int MaxRecentFiles = 12;

    /// <summary>"en" / "vi"; null = mặc định (tiếng Việt).</summary>
    public string? Language { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public List<string> RecentFiles { get; set; } = [];

    public int ExportDpi { get; set; } = 150;

    public bool ConfirmBeforeOverwrite { get; set; } = true;

    public bool ShowSidePanel { get; set; } = true;

    public bool ShowProperties { get; set; } = true;

    public double WindowWidth { get; set; } = 1360;

    public double WindowHeight { get; set; } = 860;

    public bool WindowMaximized { get; set; }

    public string TextFontFamily { get; set; } = TextStyle.Default.FontFamily;

    public double TextFontSize { get; set; } = TextStyle.Default.FontSize;

    public string TextColor { get; set; } = TextStyle.Default.Color.ToHex();

    /// <summary>Màu mặc định cho tô sáng / gạch chân / ghi chú.</summary>
    public string AnnotationColor { get; set; } = "#FFD400";

    /// <summary>Màu / độ dày mặc định cho bút vẽ và hình.</summary>
    public string InkColor { get; set; } = "#E53935";

    public double InkWidth { get; set; } = 2;

    /// <summary>Tên tác giả ghi vào nhận xét; rỗng = tên người dùng Windows.</summary>
    public string AnnotationAuthor { get; set; } = string.Empty;

    public List<SavedSignature> Signatures { get; set; } = [];

    /// <summary>Đưa file lên đầu danh sách gần đây (bỏ trùng, giới hạn số lượng).</summary>
    public void AddRecentFile(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }
    }
}
