using System.Windows;
using System.Windows.Controls;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Panels;

/// <summary>Panel "Mục lục": cây bookmark, bấm để nhảy tới trang. Xem docs/bookmarks.md.</summary>
public partial class BookmarksPanel : UserControl
{
    private DocumentWorkspace? _workspace;

    public BookmarksPanel()
    {
        InitializeComponent();
    }

    public void Attach(DocumentWorkspace workspace) => _workspace = workspace;

    public async Task LoadAsync()
    {
        if (_workspace is null)
        {
            return;
        }

        IReadOnlyList<BookmarkNode> nodes;
        try
        {
            var pdf = _workspace.Pdf;
            nodes = await Task.Run(pdf.GetBookmarks);
        }
        catch (PdfException)
        {
            nodes = [];
        }

        Tree.ItemsSource = nodes;
        EmptyText.Visibility = nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BookmarkNode { PageIndex: >= 0 } node)
        {
            _workspace?.NavigateToPage(node.PageIndex);
        }
    }
}
