using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>
/// Hộp thoại watermark và đầu / chân trang (dùng chung bố cục). Xem docs/watermark.md, header-footer.md.
/// </summary>
public partial class StampDialog : FluentWindow
{
    private static readonly string[] Colors = ["#E53935", "#6B7280", "#111827", "#1E3A8A", "#16A34A", "#A855F7"];
    private static readonly int[] Rotations = [45, 0, 90, -45];
    private static readonly StampPosition[] Positions = Enum.GetValues<StampPosition>();

    private readonly bool _watermark;
    private readonly int _pageCount;

    private StampDialog(bool watermark, IReadOnlyList<string> fonts, int pageCount)
    {
        InitializeComponent();
        _watermark = watermark;
        _pageCount = pageCount;
        var title = Loc.Get(watermark ? "Watermark_Title" : "HeaderFooter_Title");
        Title = title;
        TitleBarControl.Title = title;
        TextLabel.Text = Loc.Get(watermark ? "Watermark_Text" : "HeaderFooter_Template");
        TextInputBox.Text = Loc.Get(watermark ? "Watermark_Default" : "HeaderFooter_Default");
        TemplateHint.Visibility = watermark ? Visibility.Collapsed : Visibility.Visible;
        WatermarkPanel.Visibility = watermark ? Visibility.Visible : Visibility.Collapsed;
        PositionPanel.Visibility = watermark ? Visibility.Collapsed : Visibility.Visible;
        FontBox.ItemsSource = fonts;
        FontBox.SelectedItem = fonts.Contains("Arial") ? "Arial" : (fonts.Count > 0 ? fonts[0] : null);
        SizeBox.Value = watermark ? 64 : 10;
        RotationBox.ItemsSource = Rotations.Select(r => r + "°").ToList();
        RotationBox.SelectedIndex = 0;
        PositionBox.ItemsSource = Positions.Select(p => Loc.Get("Position_" + p)).ToList();
        PositionBox.SelectedIndex = Array.IndexOf(Positions, StampPosition.BottomCenter);
        foreach (var hex in Colors)
        {
            if (RgbColor.TryParseHex(hex, out var c))
            {
                Swatches.Children.Add(new ColorSwatch
                {
                    GroupName = "StampColor",
                    Background = new SolidColorBrush(BitmapHelper.ToColor(c)),
                    Tag = c,
                    IsChecked = hex == (watermark ? Colors[0] : Colors[2]),
                });
            }
        }

        OnOpacityChanged(this, new RoutedPropertyChangedEventArgs<double>(0, OpacitySlider.Value));
    }

    public WatermarkOptions? WatermarkResult { get; private set; }

    public HeaderFooterOptions? HeaderFooterResult { get; private set; }

    public static WatermarkOptions? AskWatermark(Window? owner, IReadOnlyList<string> fonts, int pageCount)
    {
        var dialog = new StampDialog(true, fonts, pageCount) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.WatermarkResult : null;
    }

    public static HeaderFooterOptions? AskHeaderFooter(Window? owner, IReadOnlyList<string> fonts, int pageCount)
    {
        var dialog = new StampDialog(false, fonts, pageCount) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.HeaderFooterResult : null;
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is not null)
        {
            OpacityLabel.Text = Loc.Format("Props_Opacity", Math.Round(OpacitySlider.Value * 100));
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<int>? pages = null;
        if (!string.IsNullOrWhiteSpace(RangeBox.Text))
        {
            if (!PageRangeParser.TryParse(RangeBox.Text, _pageCount, out var parsed))
            {
                ErrorText.Text = Loc.Get("Export_InvalidRange");
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            pages = parsed;
        }

        if (string.IsNullOrWhiteSpace(TextInputBox.Text))
        {
            ErrorText.Text = Loc.Get("Stamp_EmptyText");
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var font = FontBox.SelectedItem as string ?? "Arial";
        var size = Math.Clamp(SizeBox.Value ?? 12, 4, 300);
        var color = Swatches.Children.OfType<ColorSwatch>().FirstOrDefault(s => s.IsChecked == true)?.Tag as RgbColor? ?? RgbColor.Black;
        if (_watermark)
        {
            WatermarkResult = new WatermarkOptions(TextInputBox.Text.Trim(), font, size, color, OpacitySlider.Value,
                Rotations[Math.Max(0, RotationBox.SelectedIndex)], pages);
        }
        else
        {
            HeaderFooterResult = new HeaderFooterOptions(TextInputBox.Text.Trim(), Positions[Math.Max(0, PositionBox.SelectedIndex)],
                font, size, color, 28, pages);
        }

        DialogResult = true;
    }
}
