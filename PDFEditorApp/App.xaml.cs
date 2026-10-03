using System.Windows;
using System.Windows.Threading;
using PDFEditorApp.Services;
using PDFEditorApp.Views;

namespace PDFEditorApp;

/// <summary>
/// Điểm vào ứng dụng: đọc cài đặt, áp dụng giao diện (tối / sáng) và ngôn ngữ, mở cửa sổ chính
/// (kèm file truyền qua dòng lệnh). Xem docs/theme.md, docs/i18n.md.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);
        ThemeService.Apply(settings.Theme);

        var startupFile = e.Args.FirstOrDefault(FileService.IsPdfPath);
        var window = new MainWindow(settingsService, settings, new FontService(), startupFile);
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Lỗi không lường trước: báo cho người dùng thay vì đóng app đột ngột.
        MessageBox.Show(
            Loc.Get("Error_Unexpected") + Environment.NewLine + Environment.NewLine + e.Exception.Message,
            Loc.Get("Error_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
