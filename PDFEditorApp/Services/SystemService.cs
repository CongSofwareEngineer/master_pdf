using System.Diagnostics;

namespace PDFEditorApp.Services;

/// <summary>
/// Mọi phần phụ thuộc hệ điều hành (Windows 10/11) tập trung ở đây: thư mục dữ liệu app,
/// thư mục Fonts, mở Explorer. Không hard-code đường dẫn "C:\...".
/// </summary>
public static class SystemService
{
    public const string AppFolderName = "PDFEditorApp";

    /// <summary>%APPDATA%\PDFEditorApp</summary>
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>Thư mục Fonts của Windows (thường là %WINDIR%\Fonts).</summary>
    public static string FontsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    /// <summary>Thư mục font cài riêng cho user (Windows 10 1809+).</summary>
    public static string UserFontsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");

    public static string DocumentsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static string PicturesDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    /// <summary>Tên người dùng Windows (tác giả mặc định của nhận xét).</summary>
    public static string UserName => Environment.UserName;

    /// <summary>Mở địa chỉ web bằng trình duyệt mặc định (chỉ http/https/mailto).</summary>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine(ex);
        }
    }

    /// <summary>Mở Explorer và chọn sẵn file/thư mục.</summary>
    public static void RevealInExplorer(string path)
    {
        try
        {
            var args = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine(ex);
        }
    }
}
