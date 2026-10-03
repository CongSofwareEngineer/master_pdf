using System.Windows;
using System.Windows.Media;
using PDFEditorApp.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>
/// Áp dụng giao diện tối / sáng (WPF-UI Fluent) + màu nhấn của app + brush riêng (nền vùng xem trang…).
/// Mọi XAML dùng DynamicResource nên đổi theme có hiệu lực ngay. Xem docs/theme.md.
/// </summary>
public static class ThemeService
{
    /// <summary>Màu nhấn thương hiệu (xanh dương hiện đại).</summary>
    public static readonly Color Accent = Color.FromRgb(0x4F, 0x8D, 0xFF);

    public static event EventHandler? ThemeChanged;

    public static bool IsDark { get; private set; } = true;

    public static void Apply(AppTheme theme)
    {
        var dark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.System => ApplicationThemeManager.GetSystemTheme() is not (SystemTheme.Light or SystemTheme.HCWhite),
            _ => true,
        };
        var appTheme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(appTheme, WindowBackdropType.Mica, false);

        // Đặt rõ 4 sắc độ nhấn: nền sáng cần màu đậm hơn để chữ trắng trên nút chính đủ tương phản.
        if (dark)
        {
            ApplicationAccentColorManager.Apply(Accent, Color.FromRgb(0x6E, 0xA2, 0xFF), Color.FromRgb(0x8E, 0xB8, 0xFF), Color.FromRgb(0xB3, 0xD0, 0xFF));
        }
        else
        {
            ApplicationAccentColorManager.Apply(Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x1D, 0x4E, 0xD8), Color.FromRgb(0x1E, 0x40, 0xAF), Color.FromRgb(0x1E, 0x3A, 0x8A));
        }
        IsDark = dark;

        var resources = Application.Current.Resources;
        SetBrush(resources, "ViewerBackgroundBrush", dark ? Color.FromRgb(0x1A, 0x1B, 0x1F) : Color.FromRgb(0xE4, 0xE6, 0xEB));
        SetBrush(resources, "RibbonBackgroundBrush", dark ? Color.FromArgb(0x66, 0x26, 0x28, 0x2E) : Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
        SetBrush(resources, "PanelBackgroundBrush", dark ? Color.FromArgb(0x80, 0x20, 0x22, 0x27) : Color.FromArgb(0xB3, 0xFA, 0xFA, 0xFB));
        SetBrush(resources, "FloatingBarBrush", dark ? Color.FromArgb(0xEE, 0x2B, 0x2D, 0x33) : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
        SetBrush(resources, "AccentBrandBrush", Accent);
        SetBrush(resources, "AccentSoftBrush", Color.FromArgb(dark ? (byte)0x33 : (byte)0x22, Accent.R, Accent.G, Accent.B));
        SetBrush(resources, "SelectionStrokeBrush", Accent);
        SetBrush(resources, "SelectionFillBrush", Color.FromArgb(0x2A, Accent.R, Accent.G, Accent.B));
        SetBrush(resources, "TextSelectionBrush", Color.FromArgb(0x55, 0x3D, 0x7E, 0xFF));
        SetBrush(resources, "SearchHitBrush", Color.FromArgb(0x66, 0xFF, 0xC1, 0x07));
        SetBrush(resources, "SearchCurrentBrush", Color.FromArgb(0x99, 0xFF, 0x6D, 0x00));
        SetBrush(resources, "RedactBrush", Color.FromArgb(0x55, 0xE5, 0x39, 0x35));
        SetBrush(resources, "DangerBrush", Color.FromRgb(0xEF, 0x44, 0x44));
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }
}
