using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using PDFEditorApp.Views.Tools;

namespace PDFEditorApp.Views.Panels;

/// <summary>
/// Panel thuộc tính bên phải, đổi nội dung theo ngữ cảnh: thông tin tài liệu, chữ đang chọn, nhận xét,
/// màu / nét của công cụ, kiểu chữ, object đang chọn, chữ ký, che thông tin. Xem docs/ui-layout.md.
/// </summary>
public partial class PropertiesPanel : UserControl
{
    private static readonly string[] Palette =
    [
        "#FFD400", "#4ADE80", "#22D3EE", "#F472B6", "#FB923C", "#E53935",
        "#3B82F6", "#A855F7", "#111827", "#6B7280", "#FFFFFF", "#1E3A8A",
    ];

    private static readonly double[] Sizes = [6, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 60, 72];

    private DocumentWorkspace? _workspace;
    private bool _loading;
    private bool _inkMode;
    private double _fontSize = TextStyle.Default.FontSize;

    public PropertiesPanel()
    {
        InitializeComponent();
        SizeBox.ItemsSource = Sizes;
        BuildSwatches(Swatches, "ToolColor", c => _workspace?.SetToolColor(c, _inkMode));
        BuildSwatches(TextSwatches, "TextColor", _ => OnTextStyleEdited());
    }

    public void Attach(DocumentWorkspace workspace)
    {
        _workspace = workspace;
        FontBox.ItemsSource = workspace.Pdf.Fonts.Families;
    }

    /// <summary>Hiển thị đúng phần theo công cụ & đối tượng đang chọn của workspace.</summary>
    public void Refresh()
    {
        if (_workspace is not { } ws)
        {
            return;
        }

        _loading = true;
        try
        {
            HideAll();
            var tool = ws.ActiveTool;
            switch (ws.Selection)
            {
                case TextRangeSelection t:
                    ShowSelection(ws, t);
                    return;
                case AnnotationSelection a:
                    ShowAnnotation(a.Annotation);
                    return;
                case ObjectSelection { Text: { } text }:
                    ShowTextStyle(ws.Options.TextStyle, Loc.Get("Props_TextObject"), text, withContent: true);
                    return;
                case ObjectSelection o:
                    ShowObject(o.Item);
                    return;
            }

            switch (tool)
            {
                case ToolId.Highlight or ToolId.Underline or ToolId.StrikeOut or ToolId.Squiggly or ToolId.Note:
                    ShowColors(ws, ink: false, Loc.Get("Tool_" + tool));
                    break;
                case ToolId.Ink or ToolId.Rectangle or ToolId.Ellipse or ToolId.Line or ToolId.Arrow:
                    ShowColors(ws, ink: true, Loc.Get("Tool_" + tool));
                    FillBox.Visibility = tool is ToolId.Rectangle or ToolId.Ellipse ? Visibility.Visible : Visibility.Collapsed;
                    OpacitySlider.Visibility = OpacityLabel.Visibility = tool == ToolId.Ink ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case ToolId.AddText:
                    ShowTextStyle(ws.Options.TextStyle, Loc.Get("Tool_AddText"), null, withContent: false);
                    ShowHint("Hint_AddText");
                    break;
                case ToolId.Stamp:
                    ShowSign(ws);
                    break;
                case ToolId.Redact:
                    ShowRedact(ws);
                    break;
                case ToolId.PlaceImage:
                    HeaderText.Text = Loc.Get("Tool_AddImage");
                    ShowHint("Hint_PlaceImage");
                    break;
                case ToolId.EditContent:
                    HeaderText.Text = Loc.Get("Tool_EditContent");
                    ShowHint("Hint_EditContent");
                    break;
                case ToolId.Eraser:
                    HeaderText.Text = Loc.Get("Tool_Eraser");
                    ShowHint("Hint_Eraser");
                    break;
                default:
                    ShowDocument(ws);
                    break;
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ------------------------------------------------------------------
    // Các phần
    // ------------------------------------------------------------------

    private void HideAll()
    {
        foreach (var section in new FrameworkElement[] { DocSection, SelectionSection, AnnotSection, ColorSection, TextStyleSection, ObjectSection, SignSection, RedactSection, HintCard })
        {
            section.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowDocument(DocumentWorkspace ws)
    {
        HeaderText.Text = Loc.Get("Panel_Properties");
        DocSection.Visibility = Visibility.Visible;
        DocGrid.Children.Clear();
        DocGrid.RowDefinitions.Clear();
        var row = 0;
        foreach (var (label, value) in ws.DocumentSummary())
        {
            DocGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 4, 12, 4) };
            l.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            var v = new TextBlock { Text = value, Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            DocGrid.Children.Add(l);
            DocGrid.Children.Add(v);
            row++;
        }

        ShowHint("Hint_Document");
    }

    private void ShowSelection(DocumentWorkspace ws, TextRangeSelection t)
    {
        HeaderText.Text = Loc.Get("Props_SelectedText");
        SelectionSection.Visibility = Visibility.Visible;
        SelectionText.Text = ws.GetTextLayout(t.Page)?.GetRangeText(t.Start, t.End) ?? string.Empty;
        ShowHint("Hint_TextSelection");
    }

    private void ShowAnnotation(AnnotationInfo a)
    {
        HeaderText.Text = Loc.Get("Annot_" + a.Kind);
        AnnotSection.Visibility = Visibility.Visible;
        AnnotInfo.Text = Loc.Format("Props_AnnotInfo", a.PageIndex + 1, string.IsNullOrEmpty(a.Author) ? "-" : a.Author, FormatPdfDate(a.Modified));
        AnnotContents.Text = a.Contents;
    }

    private void ShowColors(DocumentWorkspace ws, bool ink, string header)
    {
        _inkMode = ink;
        HeaderText.Text = header;
        ColorSection.Visibility = Visibility.Visible;
        WidthSection.Visibility = ink ? Visibility.Visible : Visibility.Collapsed;
        FillBox.Visibility = Visibility.Collapsed;
        OpacitySlider.Visibility = OpacityLabel.Visibility = Visibility.Visible;
        var color = ink ? ws.Options.InkColor : ws.Options.MarkupColor;
        SelectSwatch(Swatches, color);
        WidthSlider.Value = ws.Options.InkWidth;
        OpacitySlider.Value = ws.Options.Opacity;
        FillBox.IsChecked = ws.Options.FillShape;
        UpdateSliderLabels();
    }

    private void ShowTextStyle(TextStyle style, string header, TextObjectInfo? text, bool withContent)
    {
        HeaderText.Text = header;
        TextStyleSection.Visibility = Visibility.Visible;
        TextContentPanel.Visibility = withContent ? Visibility.Visible : Visibility.Collapsed;
        TextButtons.Visibility = withContent ? Visibility.Visible : Visibility.Collapsed;
        OriginalFontText.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        if (text is not null)
        {
            TextContent.Text = text.Text;
            OriginalFontText.Text = Loc.Format("Panel_OriginalFont", text.FontName);
            ShowHint("Hint_TextObject");
        }

        var families = FontBox.ItemsSource as IReadOnlyList<string> ?? [];
        FontBox.SelectedItem = families.Contains(style.FontFamily) ? style.FontFamily : (families.Count > 0 ? families[0] : null);
        _fontSize = style.FontSize;
        SizeBox.Text = style.FontSize.ToString("0.#", CultureInfo.InvariantCulture);
        BoldToggle.IsChecked = style.Bold;
        ItalicToggle.IsChecked = style.Italic;
        SelectSwatch(TextSwatches, style.Color);
    }

    private void ShowObject(PageObjectInfo o)
    {
        HeaderText.Text = Loc.Get("Obj_" + o.Kind);
        ObjectSection.Visibility = Visibility.Visible;
        var r = o.ViewRect;
        ObjectInfo.Text = Loc.Format("Props_ObjectInfo", r.X * 25.4 / 72, r.Y * 25.4 / 72, r.Width * 25.4 / 72, r.Height * 25.4 / 72);
        ShowHint("Hint_Object");
    }

    private void ShowSign(DocumentWorkspace ws)
    {
        HeaderText.Text = Loc.Get("Ribbon_FillSign");
        SignSection.Visibility = Visibility.Visible;
        SignatureList.Children.Clear();
        foreach (var signature in ws.Settings.Signatures)
        {
            SignatureList.Children.Add(CreateSignatureRow(signature));
        }

        ShowHint("Hint_Sign");
    }

    private void ShowRedact(DocumentWorkspace ws)
    {
        HeaderText.Text = Loc.Get("Tool_Redact");
        RedactSection.Visibility = Visibility.Visible;
        RedactCount.Text = Loc.Format("Props_RedactCount", ws.Options.Redactions.Values.Sum(l => l.Count));
        ShowHint("Hint_Redact");
    }

    private void ShowHint(string key)
    {
        HintCard.Visibility = Visibility.Visible;
        HintText.Text = Loc.Get(key);
    }

    private DockPanel CreateSignatureRow(SavedSignature signature)
    {
        var preview = SignaturePreview.Create(signature, 170, 46);
        var use = new Wpf.Ui.Controls.Button
        {
            Content = preview,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 4, 8, 4),
            ToolTip = Loc.Get("Props_UseSignature"),
            Background = Brushes.White,
        };
        use.Click += (_, _) => _workspace?.UseSignature(signature);
        var delete = new IconButton { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24, ToolTip = Loc.Get("Panel_Delete"), Margin = new Thickness(6, 0, 0, 0) };
        delete.Click += (_, _) => _workspace?.DeleteSignature(signature);
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(delete, Dock.Right);
        row.Children.Add(delete);
        row.Children.Add(use);
        return row;
    }

    // ------------------------------------------------------------------
    // Màu
    // ------------------------------------------------------------------

    private static void BuildSwatches(WrapPanel panel, string group, Action<RgbColor> picked)
    {
        foreach (var hex in Palette)
        {
            if (!RgbColor.TryParseHex(hex, out var c))
            {
                continue;
            }

            var swatch = new ColorSwatch { GroupName = group + panel.GetHashCode(), Background = new SolidColorBrush(BitmapHelper.ToColor(c)), Tag = c, ToolTip = hex };
            swatch.Checked += (_, _) => picked(c);
            panel.Children.Add(swatch);
        }
    }

    private void SelectSwatch(WrapPanel panel, RgbColor color)
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            foreach (var s in panel.Children.OfType<ColorSwatch>())
            {
                s.IsChecked = s.Tag is RgbColor c && c == color;
            }
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private RgbColor SelectedTextColor =>
        TextSwatches.Children.OfType<ColorSwatch>().FirstOrDefault(s => s.IsChecked == true)?.Tag as RgbColor? ?? RgbColor.Black;

    // ------------------------------------------------------------------
    // Sự kiện
    // ------------------------------------------------------------------

    private void OnCopyClick(object sender, RoutedEventArgs e) => _workspace?.CopySelection();

    private void OnHighlightClick(object sender, RoutedEventArgs e) => _workspace?.ApplyMarkupToSelection(AnnotationKind.Highlight);

    private void OnUnderlineClick(object sender, RoutedEventArgs e) => _workspace?.ApplyMarkupToSelection(AnnotationKind.Underline);

    private void OnStrikeClick(object sender, RoutedEventArgs e) => _workspace?.ApplyMarkupToSelection(AnnotationKind.StrikeOut);

    private void OnSaveCommentClick(object sender, RoutedEventArgs e) => _workspace?.SaveSelectedAnnotationContents(AnnotContents.Text);

    private void OnDeleteAnnotationClick(object sender, RoutedEventArgs e) => _workspace?.DeleteSelectedAnnotation();

    private void OnWidthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderLabels();
        if (!_loading && _workspace is not null)
        {
            _workspace.Options.InkWidth = WidthSlider.Value;
            _workspace.SaveToolOptions();
        }
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderLabels();
        if (!_loading && _workspace is not null)
        {
            _workspace.Options.Opacity = OpacitySlider.Value;
        }
    }

    private void OnFillClick(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null)
        {
            _workspace.Options.FillShape = FillBox.IsChecked == true;
        }
    }

    private void UpdateSliderLabels()
    {
        if (WidthLabel is null || OpacityLabel is null)
        {
            return;
        }

        WidthLabel.Text = Loc.Format("Props_Width", WidthSlider.Value);
        OpacityLabel.Text = Loc.Format("Props_Opacity", Math.Round(OpacitySlider.Value * 100));
    }

    private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, SizeBox) && SizeBox.SelectedItem is double size)
        {
            _fontSize = size;
        }

        OnTextStyleEdited();
    }

    private void OnStyleClick(object sender, RoutedEventArgs e) => OnTextStyleEdited();

    private void OnSizeLostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(SizeBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size is >= 1 and <= 500)
        {
            _fontSize = size;
            OnTextStyleEdited();
        }
    }

    private void OnTextStyleEdited()
    {
        if (_loading || _workspace is null)
        {
            return;
        }

        _workspace.SetTextStyle(new TextStyle(
            FontBox.SelectedItem as string ?? TextStyle.Default.FontFamily,
            _fontSize,
            BoldToggle.IsChecked == true,
            ItalicToggle.IsChecked == true,
            SelectedTextColor));
    }

    private void OnTextContentKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OnApplyTextClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnApplyTextClick(object sender, RoutedEventArgs e)
    {
        OnSizeLostFocus(sender, e);
        _workspace?.ApplyTextEdit(TextContent.Text);
    }

    private void OnDeleteObjectClick(object sender, RoutedEventArgs e) => _workspace?.DeleteSelectedObject();

    private void OnCheckClick(object sender, RoutedEventArgs e) => _workspace?.SetStamp(StampKind.Check);

    private void OnCrossClick(object sender, RoutedEventArgs e) => _workspace?.SetStamp(StampKind.Cross);

    private void OnDateClick(object sender, RoutedEventArgs e) => _workspace?.SetStamp(StampKind.Date);

    private void OnNewSignatureClick(object sender, RoutedEventArgs e) => _workspace?.CreateSignature();

    private void OnApplyRedactClick(object sender, RoutedEventArgs e) => _workspace?.ApplyRedactions();

    private void OnClearRedactClick(object sender, RoutedEventArgs e) => _workspace?.ClearRedactions();

    private static string FormatPdfDate(string pdfDate)
    {
        var s = pdfDate.StartsWith("D:", StringComparison.Ordinal) ? pdfDate[2..] : pdfDate;
        return s.Length >= 12 && DateTime.TryParseExact(s[..12], "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("g", CultureInfo.CurrentCulture)
            : "-";
    }
}

/// <summary>Vẽ xem trước một chữ ký đã lưu.</summary>
public static class SignaturePreview
{
    public static FrameworkElement Create(SavedSignature signature, double width, double height)
    {
        var color = RgbColor.TryParseHex(signature.Color, out var c) ? c : new RgbColor(30, 58, 138);
        var brush = new SolidColorBrush(BitmapHelper.ToColor(color));
        if (signature.Strokes is { Count: > 0 } strokes)
        {
            var aspect = Math.Max(0.2, signature.AspectRatio);
            var w = Math.Min(width, height * aspect);
            var h = w / aspect;
            var canvas = new Canvas { Width = w, Height = h };
            foreach (var stroke in strokes)
            {
                var line = new Polyline { Stroke = brush, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
                foreach (var p in stroke)
                {
                    line.Points.Add(new Point(p.X * w, p.Y * h));
                }

                canvas.Children.Add(line);
            }

            return canvas;
        }

        return new TextBlock
        {
            Text = signature.Text ?? string.Empty,
            FontFamily = new FontFamily(signature.FontFamily ?? FontService.ScriptFamily),
            FontSize = 22,
            Foreground = brush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = width,
        };
    }
}
