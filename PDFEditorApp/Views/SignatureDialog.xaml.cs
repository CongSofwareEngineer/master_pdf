using System.Windows;
using System.Windows.Media;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>Tạo chữ ký: vẽ bằng chuột / bút, hoặc gõ tên với font viết tay. Xem docs/sign.md.</summary>
public partial class SignatureDialog : FluentWindow
{
    private static readonly string[] Colors = ["#111827", "#1E3A8A", "#2563EB"];

    public SignatureDialog(IReadOnlyList<string> handwritingFonts)
    {
        ArgumentNullException.ThrowIfNull(handwritingFonts);
        InitializeComponent();
        FontBox.ItemsSource = handwritingFonts.Count > 0 ? handwritingFonts : ["Segoe Script"];
        FontBox.SelectedIndex = 0;
        foreach (var hex in Colors)
        {
            if (RgbColor.TryParseHex(hex, out var c))
            {
                var swatch = new ColorSwatch { GroupName = "SignColor", Background = new SolidColorBrush(BitmapHelper.ToColor(c)), Tag = hex, IsChecked = hex == Colors[1] };
                swatch.Checked += (_, _) => ApplyColor();
                Swatches.Children.Add(swatch);
            }
        }

        Ink.DefaultDrawingAttributes.Width = 2.6;
        Ink.DefaultDrawingAttributes.Height = 2.6;
        Ink.DefaultDrawingAttributes.FitToCurve = true;
        ApplyColor();
    }

    public SavedSignature? Result { get; private set; }

    private string SelectedColor => Swatches.Children.OfType<ColorSwatch>().FirstOrDefault(s => s.IsChecked == true)?.Tag as string ?? Colors[1];

    private void ApplyColor()
    {
        if (RgbColor.TryParseHex(SelectedColor, out var c))
        {
            var color = BitmapHelper.ToColor(c);
            Ink.DefaultDrawingAttributes.Color = color;
            foreach (var stroke in Ink.Strokes)
            {
                stroke.DrawingAttributes.Color = color;
            }

            TypedPreview.Foreground = new SolidColorBrush(color);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => Ink.Strokes.Clear();

    private void OnTypedChanged(object sender, RoutedEventArgs e)
    {
        TypedPreview.Text = NameBox.Text;
        if (FontBox.SelectedItem is string font)
        {
            TypedPreview.FontFamily = new FontFamily(font);
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedIndex == 0)
        {
            var points = Ink.Strokes.SelectMany(s => s.StylusPoints).ToList();
            if (points.Count < 2)
            {
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            // Chuẩn hóa về khung 0..1 theo hộp bao của nét vẽ.
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var w = Math.Max(1, points.Max(p => p.X) - minX);
            var h = Math.Max(1, points.Max(p => p.Y) - minY);
            Result = new SavedSignature
            {
                Strokes = Ink.Strokes.Select(s => s.StylusPoints.Select(p => new PointD((p.X - minX) / w, (p.Y - minY) / h)).ToList()).ToList(),
                AspectRatio = w / h,
                Color = SelectedColor,
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            Result = new SavedSignature { Text = NameBox.Text.Trim(), FontFamily = FontBox.SelectedItem as string ?? FontService.ScriptFamily, Color = SelectedColor };
        }

        DialogResult = true;
    }
}
