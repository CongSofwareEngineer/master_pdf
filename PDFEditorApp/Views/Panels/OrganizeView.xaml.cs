using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Panels;

/// <summary>
/// Màn hình "Sắp xếp trang": lưới thumbnail lớn, chọn nhiều (Ctrl / Shift), kéo thả để đổi thứ tự,
/// xoay / nhân bản / trích xuất / xóa / chèn. Xem docs/page-manage.md.
/// </summary>
public partial class OrganizeView : UserControl
{
    private const string DragFormat = "PDFEditor.Pages";

    private DocumentWorkspace? _workspace;
    private ThumbnailLoader? _loader;
    private Point? _dragStart;

    public OrganizeView()
    {
        InitializeComponent();
    }

    public void Attach(DocumentWorkspace workspace)
    {
        _workspace = workspace;
        _loader = new ThumbnailLoader(workspace.Pdf, this, 150);
    }

    public async Task RebuildAsync(IReadOnlyCollection<int>? select = null)
    {
        if (_loader is null)
        {
            return;
        }

        await _loader.RebuildAsync();
        Grid.ItemsSource = _loader.Items;
        if (select is not null)
        {
            foreach (var i in select.Where(i => i >= 0 && i < _loader.Items.Count))
            {
                Grid.SelectedItems.Add(_loader.Items[i]);
            }
        }

        UpdateCount();
    }

    private List<int> Selected => Grid.SelectedItems.OfType<ThumbnailItem>().Select(t => t.Index).Order().ToList();

    private void UpdateCount() =>
        CountText.Text = Loc.Format("Organize_Count", Grid.SelectedItems.Count, _loader?.Items.Count ?? 0);

    private void OnItemLoaded(object sender, RoutedEventArgs e)
    {
        if (ThumbnailLoader.FromElement(sender) is { } item)
        {
            _loader?.Request(item);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCount();

    private void OnDoneClick(object sender, RoutedEventArgs e) => _workspace?.SetOrganizeMode(false);

    private void OnRotateLeftClick(object sender, RoutedEventArgs e) => Run(p => _workspace!.RotatePagesAsync(p, 3));

    private void OnRotateRightClick(object sender, RoutedEventArgs e) => Run(p => _workspace!.RotatePagesAsync(p, 1));

    private void OnDuplicateClick(object sender, RoutedEventArgs e) => Run(p => _workspace!.DuplicatePagesAsync(p));

    private void OnExtractClick(object sender, RoutedEventArgs e) => Run(p => _workspace!.ExtractPagesAsync(p));

    private void OnDeleteClick(object sender, RoutedEventArgs e) => Run(p => _workspace!.DeletePagesAsync(p));

    private void OnInsertBlankClick(object sender, RoutedEventArgs e) =>
        _ = _workspace?.InsertBlankPageAsync(Selected.Count > 0 ? Selected[^1] + 1 : _loader?.Items.Count ?? 0);

    private void OnInsertFileClick(object sender, RoutedEventArgs e) =>
        _ = _workspace?.InsertPagesFromFileAsync(Selected.Count > 0 ? Selected[^1] + 1 : _loader?.Items.Count ?? 0);

    private void Run(Func<IReadOnlyList<int>, Task> action)
    {
        var pages = Selected;
        if (_workspace is null || pages.Count == 0)
        {
            _workspace?.SetStatus(Loc.Get("Organize_SelectFirst"));
            return;
        }

        _ = action(pages);
    }

    // ---- Kéo thả đổi thứ tự ----
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = FindItem(e.OriginalSource) is not null ? e.GetPosition(Grid) : null;

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(Grid) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        var pages = Selected;
        if (pages.Count > 0)
        {
            DragDrop.DoDragDrop(Grid, new DataObject(DragFormat, pages.ToArray()), DragDropEffects.Move);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (_workspace is null || e.Data.GetData(DragFormat) is not int[] pages || FindItem(e.OriginalSource) is not { } target)
        {
            return;
        }

        // Thả vào nửa phải của trang đích = chèn sau nó.
        var container = (ListBoxItem)Grid.ItemContainerGenerator.ContainerFromItem(target);
        var after = container is not null && e.GetPosition(container).X > container.ActualWidth / 2;
        var insertBefore = target.Index + (after ? 1 : 0);
        var dest = insertBefore - pages.Count(p => p < insertBefore);
        _ = _workspace.MovePagesAsync(pages, dest);
    }

    private static ThumbnailItem? FindItem(object source)
    {
        var d = source as DependencyObject;
        while (d is not null and not ListBoxItem)
        {
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }

        return (d as ListBoxItem)?.DataContext as ThumbnailItem;
    }
}
