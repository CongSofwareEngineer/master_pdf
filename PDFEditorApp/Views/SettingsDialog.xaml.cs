using System.Windows;
using System.Windows.Controls;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>Cài đặt: giao diện tối / sáng, ngôn ngữ, tên tác giả nhận xét, hỏi khi ghi đè. Xem docs/settings.md.</summary>
public partial class SettingsDialog : FluentWindow
{
    private static readonly AppTheme[] Themes = [AppTheme.Dark, AppTheme.Light, AppTheme.System];
    private readonly AppSettings _settings;
    private readonly AppTheme _originalTheme;

    public SettingsDialog(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settings = settings;
        _originalTheme = settings.Theme;
        ThemeBox.ItemsSource = Themes.Select(t => Loc.Get("Theme_" + t)).ToList();
        ThemeBox.SelectedIndex = Array.IndexOf(Themes, settings.Theme);
        LanguageBox.ItemsSource = new[] { Loc.Get("Lang_Vietnamese"), Loc.Get("Lang_English") };
        LanguageBox.SelectedIndex = LocalizationService.Instance.Language == LocalizationService.English ? 1 : 0;
        AuthorBox.Text = string.IsNullOrWhiteSpace(settings.AnnotationAuthor) ? SystemService.UserName : settings.AnnotationAuthor;
        ConfirmSwitch.IsChecked = settings.ConfirmBeforeOverwrite;
        Closed += (_, _) =>
        {
            if (DialogResult != true)
            {
                ThemeService.Apply(_originalTheme);
            }
        };
    }

    public string SelectedLanguage => LanguageBox.SelectedIndex == 1 ? LocalizationService.English : LocalizationService.Vietnamese;

    private AppTheme SelectedTheme => ThemeBox.SelectedIndex >= 0 ? Themes[ThemeBox.SelectedIndex] : AppTheme.Dark;

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ThemeService.Apply(SelectedTheme); // xem trước
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _settings.Theme = SelectedTheme;
        _settings.Language = SelectedLanguage;
        _settings.AnnotationAuthor = AuthorBox.Text.Trim();
        _settings.ConfirmBeforeOverwrite = ConfirmSwitch.IsChecked == true;
        DialogResult = true;
    }
}
