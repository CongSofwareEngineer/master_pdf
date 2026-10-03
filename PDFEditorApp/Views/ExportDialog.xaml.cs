using System.Windows;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

public enum ExportFormat
{
    Png,
    Jpg,
    Docx,
    Text,
}

/// <summary>Chọn định dạng, phạm vi trang và DPI để export. Xem docs/export.md.</summary>
public partial class ExportDialog : FluentWindow
{
    private static readonly int[] DpiOptions = [72, 96, 150, 200, 300];

    private readonly int _pageCount;
    private readonly int _currentPage;

    public ExportDialog(int pageCount, int currentPage, int dpi, ExportFormat format)
    {
        InitializeComponent();
        _pageCount = pageCount;
        _currentPage = currentPage;
        CurrentPageRadio.Content = Loc.Format("Export_CurrentPage", currentPage + 1);
        DpiBox.ItemsSource = DpiOptions;
        DpiBox.SelectedItem = DpiOptions.Contains(dpi) ? dpi : 150;
        (format switch
        {
            ExportFormat.Jpg => JpgRadio,
            ExportFormat.Docx => DocxRadio,
            ExportFormat.Text => TextRadio,
            _ => PngRadio,
        }).IsChecked = true;
    }

    public ExportFormat Format =>
        JpgRadio.IsChecked == true ? ExportFormat.Jpg
        : DocxRadio.IsChecked == true ? ExportFormat.Docx
        : TextRadio.IsChecked == true ? ExportFormat.Text
        : ExportFormat.Png;

    public IReadOnlyList<int> Pages { get; private set; } = [];

    public int Dpi => DpiBox.SelectedItem is int dpi ? dpi : 150;

    private void OnFormatChanged(object sender, RoutedEventArgs e)
    {
        if (DpiPanel is not null)
        {
            DpiPanel.Visibility = Format is ExportFormat.Png or ExportFormat.Jpg ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnRangeFocus(object sender, RoutedEventArgs e) => CustomRadio.IsChecked = true;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (CurrentPageRadio.IsChecked == true)
        {
            Pages = [_currentPage];
        }
        else if (CustomRadio.IsChecked == true)
        {
            if (!PageRangeParser.TryParse(RangeBox.Text, _pageCount, out var pages))
            {
                ErrorText.Visibility = Visibility.Visible;
                RangeBox.Focus();
                return;
            }

            Pages = pages;
        }
        else
        {
            Pages = Enumerable.Range(0, _pageCount).ToList();
        }

        DialogResult = true;
    }
}
