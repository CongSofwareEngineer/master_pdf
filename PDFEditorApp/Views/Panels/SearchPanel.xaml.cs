using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PDFEditorApp.Models;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views.Panels;

/// <summary>Một dòng kết quả tìm kiếm.</summary>
public sealed record SearchResultItem(SearchHit Hit, string PageLabel);

/// <summary>Panel "Tìm kiếm" (Ctrl+F). Xem docs/search.md.</summary>
public partial class SearchPanel : UserControl
{
    private DocumentWorkspace? _workspace;
    private bool _updating;

    public SearchPanel()
    {
        InitializeComponent();
        Results.ItemsSource = Items;
    }

    public ObservableCollection<SearchResultItem> Items { get; } = [];

    public void Attach(DocumentWorkspace workspace) => _workspace = workspace;

    public void FocusQuery()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    public SearchOptions Options => new(MatchCaseBox.IsChecked == true, IgnoreDiacriticsBox.IsChecked == true, WholeWordBox.IsChecked == true);

    /// <summary>Cập nhật dòng đếm kết quả (đang tìm / n kết quả).</summary>
    public void SetCount(int count, bool searching) =>
        CountText.Text = searching ? Loc.Format("Search_Searching", count) : Loc.Format("Search_Count", count);

    public void SelectResult(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return;
        }

        _updating = true;
        try
        {
            Results.SelectedIndex = index;
            Results.ScrollIntoView(Items[index]);
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _workspace is null)
        {
            return;
        }

        e.Handled = true;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _workspace.SearchStep(-1);
        }
        else if (Items.Count > 0 && _workspace.LastQuery == QueryBox.Text)
        {
            _workspace.SearchStep(1);
        }
        else
        {
            _ = _workspace.SearchAsync(QueryBox.Text, Options);
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e) => _workspace?.SearchStep(1);

    private void OnPreviousClick(object sender, RoutedEventArgs e) => _workspace?.SearchStep(-1);

    private void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating && Results.SelectedIndex >= 0)
        {
            _workspace?.ShowSearchResult(Results.SelectedIndex);
        }
    }
}
