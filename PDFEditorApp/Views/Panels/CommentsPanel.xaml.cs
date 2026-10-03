using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views.Panels;

/// <summary>Một dòng trong danh sách nhận xét.</summary>
public sealed record CommentItem(AnnotationInfo Annotation, string Header, string Text, SymbolRegular Symbol, Brush Swatch);

/// <summary>Panel "Nhận xét": mọi nhận xét trong tài liệu, bấm để nhảy tới. Xem docs/annotations.md.</summary>
public partial class CommentsPanel : UserControl
{
    private DocumentWorkspace? _workspace;
    private int _generation;

    public CommentsPanel()
    {
        InitializeComponent();
    }

    public void Attach(DocumentWorkspace workspace) => _workspace = workspace;

    public async Task RefreshAsync()
    {
        if (_workspace is null || !IsVisible)
        {
            return;
        }

        var generation = ++_generation;
        var pdf = _workspace.Pdf;
        List<AnnotationInfo> all;
        try
        {
            all = await Task.Run(() =>
            {
                var list = new List<AnnotationInfo>();
                for (var i = 0; i < pdf.PageCount; i++)
                {
                    list.AddRange(pdf.GetAnnotations(i));
                }

                return list;
            });
        }
        catch (PdfException)
        {
            return;
        }

        if (generation != _generation)
        {
            return;
        }

        List.ItemsSource = all.Select(ToItem).ToList();
        EmptyText.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static CommentItem ToItem(AnnotationInfo a)
    {
        var kindName = Loc.Get("Annot_" + a.Kind);
        var header = Loc.Format("Comments_Header", kindName, a.PageIndex + 1, string.IsNullOrEmpty(a.Author) ? "-" : a.Author);
        var text = string.IsNullOrWhiteSpace(a.Contents) ? kindName : a.Contents;
        var symbol = a.Kind switch
        {
            AnnotationKind.Highlight => SymbolRegular.Highlight24,
            AnnotationKind.Underline or AnnotationKind.Squiggly => SymbolRegular.TextUnderline24,
            AnnotationKind.StrikeOut => SymbolRegular.TextStrikethrough24,
            AnnotationKind.Note => SymbolRegular.NoteEdit24,
            AnnotationKind.Ink => SymbolRegular.Pen24,
            AnnotationKind.Square => SymbolRegular.RectangleLandscape24,
            AnnotationKind.Circle => SymbolRegular.Oval24,
            _ => SymbolRegular.CommentEdit24,
        };
        var brush = new SolidColorBrush(Color.FromRgb(a.Color.R, a.Color.G, a.Color.B));
        brush.Freeze();
        return new CommentItem(a, header, text, symbol, brush);
    }

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is CommentItem item)
        {
            _workspace?.ShowAnnotation(item.Annotation);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CommentItem item })
        {
            _workspace?.DeleteAnnotation(item.Annotation);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = RefreshAsync();
}
