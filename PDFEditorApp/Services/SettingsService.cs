using System.Diagnostics;
using System.Text.Json;
using PDFEditorApp.Models;

namespace PDFEditorApp.Services;

/// <summary>Đọc / ghi settings.json. File hỏng hoặc không đọc được → dùng mặc định. Xem docs/settings.md.</summary>
public sealed class SettingsService(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService()
        : this(SystemService.SettingsFilePath)
    {
    }

    public string FilePath { get; } = filePath;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (settings is not null)
                {
                    settings.RecentFiles ??= [];
                    settings.ExportDpi = Math.Clamp(settings.ExportDpi, 36, 600);
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Debug.WriteLine(ex);
        }

        return new AppSettings();
    }

    /// <summary>Lưu cài đặt; lỗi IO chỉ ghi log (không làm phiền người dùng).</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            FileService.WriteAtomic(FilePath, stream =>
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json);
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PdfException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
