using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PDFEditorApp.Views;

/// <summary>Một file gần đây trên màn hình chính.</summary>
public sealed record RecentFileItem(string Path, string Name, string Folder, string Modified);

/// <summary>Màn hình chính: thao tác nhanh + file gần đây. Xem docs/tabs.md.</summary>
public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? NewRequested;

    public event EventHandler? CombineRequested;

    public event EventHandler? ImagesRequested;

    public event EventHandler<string>? RecentRequested;

    public event EventHandler? ClearRecentRequested;

    public void SetRecentFiles(IEnumerable<string> paths)
    {
        var items = paths.Select(p =>
        {
            var modified = File.Exists(p) ? File.GetLastWriteTime(p).ToString("g", CultureInfo.CurrentCulture) : "-";
            return new RecentFileItem(p, System.IO.Path.GetFileName(p), System.IO.Path.GetDirectoryName(p) ?? string.Empty, modified);
        }).ToList();
        RecentList.ItemsSource = items;
        NoRecentText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearRecentButton.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);

    private void OnNewClick(object sender, RoutedEventArgs e) => NewRequested?.Invoke(this, EventArgs.Empty);

    private void OnCombineClick(object sender, RoutedEventArgs e) => CombineRequested?.Invoke(this, EventArgs.Empty);

    private void OnImagesClick(object sender, RoutedEventArgs e) => ImagesRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearRecentClick(object sender, RoutedEventArgs e) => ClearRecentRequested?.Invoke(this, EventArgs.Empty);

    private void OnRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
        {
            RecentRequested?.Invoke(this, path);
        }
    }
}
