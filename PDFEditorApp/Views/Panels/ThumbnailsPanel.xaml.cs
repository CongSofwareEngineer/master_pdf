using System.Windows;
using System.Windows.Controls;

namespace PDFEditorApp.Views.Panels;

/// <summary>Panel "Trang": thumbnail mọi trang, bấm để chuyển trang, menu chuột phải thao tác trang.</summary>
public partial class ThumbnailsPanel : UserControl
{
    private DocumentWorkspace? _workspace;
    private ThumbnailLoader? _loader;
    private bool _updating;

    public ThumbnailsPanel()
    {
        InitializeComponent();
    }

    public void Attach(DocumentWorkspace workspace)
    {
        _workspace = workspace;
        _loader = new ThumbnailLoader(workspace.Pdf, this, 132);
    }

    public async Task RebuildAsync(int currentPage)
    {
        if (_loader is null)
        {
            return;
        }

        await _loader.RebuildAsync();
        List.ItemsSource = _loader.Items;
        SelectPage(currentPage);
    }

    public void Invalidate(int page) => _loader?.Invalidate(page);

    public void SelectPage(int page)
    {
        if (_loader is null || page < 0 || page >= _loader.Items.Count)
        {
            return;
        }

        _updating = true;
        try
        {
            List.SelectedIndex = page;
            List.ScrollIntoView(_loader.Items[page]);
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnItemLoaded(object sender, RoutedEventArgs e)
    {
        if (ThumbnailLoader.FromElement(sender) is { } item)
        {
            _loader?.Request(item);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating && List.SelectedIndex >= 0)
        {
            _workspace?.NavigateToPage(List.SelectedIndex);
        }
    }
}
