using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using PDFEditorApp.Views.Panels;
using PDFEditorApp.Views.Tools;

namespace PDFEditorApp.Views;

/// <summary>Panel bên trái đang mở.</summary>
public enum SidePanelKind
{
    None,
    Pages,
    Bookmarks,
    Search,
    Comments,
}

/// <summary>
/// Một tab tài liệu: sở hữu <see cref="PdfService"/> riêng, vùng xem, các panel, công cụ, tìm kiếm.
/// MainWindow chuyển lệnh ribbon / phím tắt vào đây. Xem docs/tabs.md, docs/ui-layout.md.
/// </summary>
public partial class DocumentWorkspace : UserControl, IToolHost, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<ToolId, Tool> _tools;
    private readonly Dictionary<int, PageData> _pages = [];
    private readonly Dictionary<int, int> _pageVersions = [];
    private readonly Dictionary<int, PageTextLayout> _searchLayouts = [];
    private readonly List<SearchHit> _hits = [];
    private readonly DispatcherTimerHelper _busyTimer;

    private EditorSelection? _selection;
    private ToolId _activeTool = ToolId.Select;
    private SidePanelKind _sidePanel = SidePanelKind.Pages;
    private int _busyCount;
    private int _hitIndex = -1;
    private CancellationTokenSource? _searchCts;
    private PdfDocumentInfo? _docInfo;
    private bool _disposed;

    public DocumentWorkspace(SettingsService settingsService, AppSettings settings, FontService fonts)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settingsService = settingsService;
        Settings = settings;
        Pdf = new PdfService(fonts);
        Pdf.AnnotationAuthor = string.IsNullOrWhiteSpace(settings.AnnotationAuthor) ? SystemService.UserName : settings.AnnotationAuthor;
        Export = new ExportService(Pdf);
        LoadOptionsFromSettings();

        var redact = new RedactTool(this);
        redact.Changed += (_, _) =>
        {
            View.RedecorateAll();
            Props.Refresh();
        };
        var place = new PlaceTool(this);
        _tools = new Dictionary<ToolId, Tool>
        {
            [ToolId.Select] = new SelectTool(this),
            [ToolId.Hand] = new HandTool(this),
            [ToolId.EditContent] = new EditContentTool(this),
            [ToolId.AddText] = new AddTextTool(this),
            [ToolId.PlaceImage] = place,
            [ToolId.Stamp] = place,
            [ToolId.Highlight] = new MarkupTool(this, AnnotationKind.Highlight),
            [ToolId.Underline] = new MarkupTool(this, AnnotationKind.Underline),
            [ToolId.StrikeOut] = new MarkupTool(this, AnnotationKind.StrikeOut),
            [ToolId.Squiggly] = new MarkupTool(this, AnnotationKind.Squiggly),
            [ToolId.Note] = new NoteTool(this),
            [ToolId.Ink] = new InkTool(this),
            [ToolId.Rectangle] = new ShapeTool(this, ShapeKind.Rectangle),
            [ToolId.Ellipse] = new ShapeTool(this, ShapeKind.Ellipse),
            [ToolId.Line] = new ShapeTool(this, ShapeKind.Line),
            [ToolId.Arrow] = new ShapeTool(this, ShapeKind.Arrow),
            [ToolId.Eraser] = new EraserTool(this),
            [ToolId.Redact] = redact,
        };

        Thumbs.Attach(this);
        Bookmarks.Attach(this);
        Search.Attach(this);
        Comments.Attach(this);
        Props.Attach(this);
        Organize.Attach(this);

        View.PageDecorate += OnPageDecorate;
        View.CurrentPageChanged += (_, page) =>
        {
            Thumbs.SelectPage(page);
            UpdateFloatingBar();
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        View.ZoomChanged += (_, _) =>
        {
            UpdateFloatingBar();
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        _busyTimer = new DispatcherTimerHelper(TimeSpan.FromMilliseconds(180), () =>
        {
            if (_busyCount > 0)
            {
                BusyOverlay.Visibility = Visibility.Visible;
            }
        });
        BuildZoomMenu();
        ApplyPanelVisibility();
    }

    /// <summary>Tiêu đề / trạng thái sửa / trang / zoom / công cụ thay đổi.</summary>
    public event EventHandler? StateChanged;

    public event EventHandler<string>? StatusChanged;

    /// <summary>Thông báo ngắn (snackbar). Bool = lỗi.</summary>
    public event EventHandler<(string Message, bool IsError)>? Notified;

    public PdfService Pdf { get; }

    public ExportService Export { get; }

    public AppSettings Settings { get; }

    public ToolOptions Options { get; } = new();

    public EditorSelection? Selection => _selection;

    public Window? OwnerWindow => Window.GetWindow(this);

    public ToolId ActiveTool => _activeTool;

    public SidePanelKind SidePanelShown => _sidePanel;

    public bool IsBusy => _busyCount > 0;

    public bool IsOrganizing => Organize.Visibility == Visibility.Visible;

    public string? FilePath { get; private set; }

    public bool IsModified { get; private set; }

    public bool CanUndo { get; private set; }

    public bool CanRedo { get; private set; }

    public int PageCount { get; private set; }

    public int CurrentPage => View.CurrentPage;

    public string DisplayName => FilePath is null ? Loc.Get("Doc_Untitled") : System.IO.Path.GetFileName(FilePath);

    public string? LastQuery { get; private set; }

    // =====================================================================
    // Nạp tài liệu
    // =====================================================================

    /// <summary>Mở file (hỏi mật khẩu nếu cần). False nếu người dùng hủy.</summary>
    public async Task<bool> LoadFileAsync(string path)
    {
        var data = await FileService.ReadAllBytesAsync(path);
        if (!await TryWithPasswordAsync(System.IO.Path.GetFileName(path), password => Pdf.Open(data, password, path)))
        {
            return false;
        }

        await InitializeViewAsync();
        return true;
    }

    public async Task LoadNewAsync()
    {
        await Task.Run(() => Pdf.CreateNew());
        await InitializeViewAsync();
    }

    public async Task LoadImagesAsync(IReadOnlyList<ImageData> images)
    {
        await Task.Run(() => Pdf.CreateFromImages(images));
        await InitializeViewAsync();
    }

    public async Task LoadCombinedAsync(IReadOnlyList<(byte[] Data, string? Password)> files)
    {
        await Task.Run(() => Pdf.CreateCombined(files));
        await InitializeViewAsync();
    }

    /// <summary>Chạy thao tác mở; nếu cần mật khẩu thì hỏi và thử lại. False nếu hủy.</summary>
    public async Task<bool> TryWithPasswordAsync(string fileName, Action<string?> open)
    {
        string? password = null;
        while (true)
        {
            try
            {
                await Task.Run(() => open(password));
                return true;
            }
            catch (PdfException ex) when (ex.Kind == PdfErrorKind.Password)
            {
                BusyOverlay.Visibility = Visibility.Collapsed;
                var dialog = new PasswordDialog(fileName, password is not null) { Owner = OwnerWindow };
                if (dialog.ShowDialog() != true)
                {
                    return false;
                }

                password = dialog.Password;
            }
        }
    }

    private async Task InitializeViewAsync()
    {
        RefreshDocState();
        var sizes = await Task.Run(Pdf.GetPageSizes);
        View.SetDocument(Pdf, sizes);
        View.SetZoom(ZoomMode.FitWidth);
        await Thumbs.RebuildAsync(0);
        await Bookmarks.LoadAsync();
        _docInfo = await Task.Run(Pdf.GetDocumentInfo);
        SetTool(ToolId.Select);
        UpdateFloatingBar();
        if (Pdf.HasForms)
        {
            ShowBanner(Loc.Get("Banner_Forms"));
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        View.FocusView();
    }

    private void RefreshDocState()
    {
        FilePath = Pdf.FilePath;
        IsModified = Pdf.IsModified;
        CanUndo = Pdf.CanUndo;
        CanRedo = Pdf.CanRedo;
        PageCount = Pdf.PageCount;
    }

    // =====================================================================
    // Dữ liệu trang (cache) & lớp phủ
    // =====================================================================

    private sealed class PageData
    {
        public PageTextLayout? Layout { get; init; }

        public IReadOnlyList<LinkInfo>? Links { get; init; }

        public IReadOnlyList<AnnotationInfo>? Annotations { get; init; }

        public IReadOnlyList<FormFieldInfo>? Forms { get; init; }

        public IReadOnlyList<PageObjectInfo>? Objects { get; init; }

        public IReadOnlyList<TextBlockInfo>? TextBlocks { get; init; }

        public bool HasObjects { get; init; }
    }

    private readonly HashSet<int> _loading = [];

    public PageTextLayout? GetTextLayout(int page) => Data(page)?.Layout ?? _searchLayouts.GetValueOrDefault(page);

    public IReadOnlyList<LinkInfo>? GetLinks(int page) => Data(page)?.Links;

    public IReadOnlyList<AnnotationInfo>? GetAnnotations(int page) => Data(page)?.Annotations;

    public IReadOnlyList<PageObjectInfo>? GetObjects(int page) => Data(page, objects: true)?.Objects;

    public IReadOnlyList<TextBlockInfo>? GetTextBlocks(int page) => Data(page, objects: true)?.TextBlocks;

    private PageData? Data(int page, bool objects = false)
    {
        if (_pages.TryGetValue(page, out var data) && (!objects || data.HasObjects))
        {
            return data;
        }

        EnsurePageData(page, objects);
        return objects ? null : data;
    }

    private void EnsurePageData(int page, bool objects)
    {
        if (page < 0 || page >= PageCount || _loading.Contains(page) ||
            (_pages.TryGetValue(page, out var existing) && (!objects || existing.HasObjects)))
        {
            return;
        }

        _ = LoadPageDataAsync(page, objects);
    }

    private async Task LoadPageDataAsync(int page, bool objects)
    {
        _loading.Add(page);
        var version = _pageVersions.GetValueOrDefault(page);
        var pdf = Pdf;
        try
        {
            var data = await Task.Run(() => new PageData
            {
                Layout = pdf.GetTextLayout(page),
                Links = pdf.GetLinks(page),
                Annotations = pdf.GetAnnotations(page),
                Forms = pdf.HasForms ? pdf.GetFormFields(page) : null,
                Objects = objects ? pdf.GetPageObjects(page) : null,
                TextBlocks = objects ? pdf.GetTextBlocks(page) : null,
                HasObjects = objects,
            });
            if (_disposed || version != _pageVersions.GetValueOrDefault(page))
            {
                return;
            }

            _pages[page] = data;
            if (View.GetPageView(page) is { } pv)
            {
                DrawOverlays(pv, forms: true);
                pv.ToolLayer.Children.Clear();
                View.Tool?.OnPageDecorate(View, pv);
            }

            if (_selection is TextRangeSelection or null && _activeTool == ToolId.Select)
            {
                Props.Refresh();
            }
        }
        catch (PdfException)
        {
            // Trang vừa bị xóa / tài liệu đóng.
        }
        finally
        {
            _loading.Remove(page);
            if (!_disposed && View.GetPageView(page) is not null &&
                (!_pages.ContainsKey(page) || (_activeTool == ToolId.EditContent && !_pages[page].HasObjects)))
            {
                EnsurePageData(page, _activeTool == ToolId.EditContent);
            }
        }
    }

    /// <summary>Trang đã sửa: bỏ cache, render lại, cập nhật thumbnail.</summary>
    private void InvalidatePageData(int page)
    {
        _pageVersions[page] = _pageVersions.GetValueOrDefault(page) + 1;
        _pages.Remove(page);
        _searchLayouts.Remove(page);
        View.InvalidatePage(page);
        Thumbs.Invalidate(page);
    }

    private void InvalidateAllData()
    {
        foreach (var key in _pageVersions.Keys.ToList())
        {
            _pageVersions[key]++;
        }

        for (var i = 0; i < PageCount; i++)
        {
            _pageVersions[i] = _pageVersions.GetValueOrDefault(i) + 1;
        }

        _pages.Clear();
        _searchLayouts.Clear();
    }

    private void OnPageDecorate(object? sender, PageView pv)
    {
        EnsurePageData(pv.PageIndex, _activeTool == ToolId.EditContent);
        DrawOverlays(pv, forms: true);
    }

    /// <summary>Vẽ tô sáng (tìm kiếm, chữ chọn, nhận xét chọn, vùng che) và ô form của một trang.</summary>
    private void DrawOverlays(PageView pv, bool forms)
    {
        var page = pv.PageIndex;
        pv.HighlightLayer.Children.Clear();
        var layout = GetTextLayout(page);

        if (layout is not null)
        {
            var currentHit = _hitIndex >= 0 && _hitIndex < _hits.Count ? _hits[_hitIndex] : null;
            foreach (var hit in _hits.Where(h => h.PageIndex == page))
            {
                var brush = (Brush)FindResource(ReferenceEquals(hit, currentHit) ? "SearchCurrentBrush" : "SearchHitBrush");
                foreach (var r in layout.GetRangeRects(hit.Start, hit.Start + hit.Length))
                {
                    AddRect(pv, r.Inflate(0.5), brush, null);
                }
            }

            if (_selection is TextRangeSelection t && t.Page == page)
            {
                foreach (var r in layout.GetRangeRects(t.Start, t.End))
                {
                    AddRect(pv, r, (Brush)FindResource("TextSelectionBrush"), null);
                }
            }
        }

        if (_selection is AnnotationSelection a && a.Page == page)
        {
            AddRect(pv, a.Annotation.ViewRect.Inflate(3), Brushes.Transparent, (Brush)FindResource("SelectionStrokeBrush"));
        }

        if (Options.Redactions.TryGetValue(page, out var redactions))
        {
            foreach (var r in redactions)
            {
                AddRect(pv, r, (Brush)FindResource("RedactBrush"), (Brush)FindResource("DangerBrush"));
            }
        }

        if (forms)
        {
            DrawForms(pv);
        }
    }

    private static void AddRect(PageView pv, RectD view, Brush fill, Brush? stroke)
    {
        var rect = new Rectangle { Fill = fill, Stroke = stroke, StrokeThickness = stroke is null ? 0 : 1.5, RadiusX = 2, RadiusY = 2 };
        if (stroke is not null)
        {
            rect.StrokeDashArray = [4, 3];
        }

        var local = pv.ToLocal(view);
        Canvas.SetLeft(rect, local.X);
        Canvas.SetTop(rect, local.Y);
        rect.Width = Math.Max(1, local.Width);
        rect.Height = Math.Max(1, local.Height);
        pv.HighlightLayer.Children.Add(rect);
    }

    /// <summary>Đặt control WPF trùng vị trí từng ô form (ô chữ, ô đánh dấu, danh sách). Xem docs/forms.md.</summary>
    private void DrawForms(PageView pv)
    {
        pv.FormLayer.Children.Clear();
        if (Data(pv.PageIndex)?.Forms is not { Count: > 0 } fields)
        {
            return;
        }

        var page = pv.PageIndex;
        var fieldBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xEF, 0xFF));
        foreach (var field in fields.Where(f => !f.ReadOnly))
        {
            var r = pv.ToLocal(field.ViewRect);
            FrameworkElement? control = null;
            switch (field.Kind)
            {
                case FormFieldKind.Text:
                    var box = new TextBox
                    {
                        Style = null,
                        Text = field.Value,
                        Background = fieldBrush,
                        Foreground = Brushes.Black,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(2, 0, 2, 0),
                        FontSize = Math.Clamp(r.Height * (field.Multiline ? 0.35 : 0.62), 7, 28),
                        AcceptsReturn = field.Multiline,
                        TextWrapping = field.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                        VerticalContentAlignment = field.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                        ToolTip = field.Name,
                    };
                    box.LostKeyboardFocus += (_, _) =>
                    {
                        if (box.Text != field.Value)
                        {
                            var text = box.Text;
                            _ = EditAsync(page, () =>
                            {
                                Pdf.SetFormFieldText(page, field.AnnotIndex, text);
                                return -1;
                            }, "Status_FormFilled");
                        }
                    };
                    control = box;
                    break;
                case FormFieldKind.CheckBox or FormFieldKind.RadioButton:
                    var toggle = new Border { Background = new SolidColorBrush(Color.FromArgb(0x40, 0x4F, 0x8D, 0xFF)), Cursor = Cursors.Hand, ToolTip = field.Name };
                    toggle.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        _ = EditAsync(page, () =>
                        {
                            Pdf.ToggleFormField(page, field.AnnotIndex);
                            return -1;
                        }, "Status_FormFilled");
                    };
                    control = toggle;
                    break;
                case FormFieldKind.ComboBox or FormFieldKind.ListBox:
                    var combo = new ComboBox
                    {
                        ItemsSource = field.Options,
                        SelectedIndex = field.SelectedIndex,
                        FontSize = Math.Clamp(r.Height * 0.55, 8, 20),
                        Padding = new Thickness(4, 0, 4, 0),
                        ToolTip = field.Name,
                    };
                    combo.SelectionChanged += (_, _) =>
                    {
                        var index = combo.SelectedIndex;
                        if (index >= 0 && index != field.SelectedIndex)
                        {
                            _ = EditAsync(page, () =>
                            {
                                Pdf.SetFormFieldChoice(page, field.AnnotIndex, index);
                                return -1;
                            }, "Status_FormFilled");
                        }
                    };
                    control = combo;
                    break;
            }

            if (control is null)
            {
                continue;
            }

            Canvas.SetLeft(control, r.X);
            Canvas.SetTop(control, r.Y);
            control.Width = Math.Max(8, r.Width);
            control.Height = Math.Max(8, r.Height);
            pv.FormLayer.Children.Add(control);
        }
    }

    // =====================================================================
    // Công cụ & lựa chọn (IToolHost)
    // =====================================================================

    public void SetTool(ToolId tool)
    {
        if (tool != _activeTool || View.Tool is null)
        {
            if (_selection is not null && !(tool == ToolId.Select && _selection is TextRangeSelection))
            {
                _selection = null;
            }

            _activeTool = tool;
            View.Tool = _tools[tool];
            if (tool == ToolId.EditContent)
            {
                foreach (var pv in View.RealizedPages)
                {
                    EnsurePageData(pv.PageIndex, objects: true);
                }
            }

            View.RedecorateAll();
        }

        Props.Refresh();
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (tool is not ToolId.Select)
        {
            SetStatus(Loc.Get("Hint_Tool_" + tool));
        }
    }

    public void ActivateDefaultTool() => SetTool(ToolId.Select);

    public void SetSelection(EditorSelection? selection)
    {
        var oldPage = _selection?.Page ?? -1;
        _selection = selection;
        foreach (var pv in View.RealizedPages.Where(p => p.PageIndex == oldPage || p.PageIndex == selection?.Page))
        {
            DrawOverlays(pv, forms: false);
            pv.ToolLayer.Children.Clear();
            View.Tool?.OnPageDecorate(View, pv);
        }

        if (selection is ObjectSelection { Text: { } text })
        {
            Options.TextStyle = text.Style;
        }

        Props.Refresh();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectObjectAfterEdit(int page, int objectIndex)
    {
        if (objectIndex < 0)
        {
            SetSelection(null);
            return;
        }

        if (_activeTool != ToolId.EditContent)
        {
            SetTool(ToolId.EditContent);
        }

        _ = SelectObjectWhenLoadedAsync(page, objectIndex);
    }

    private async Task SelectObjectWhenLoadedAsync(int page, int objectIndex)
    {
        for (var i = 0; i < 40 && GetObjects(page) is null; i++)
        {
            await Task.Delay(25);
        }

        if (GetTextBlocks(page)?.FirstOrDefault(b => b.Contains(objectIndex)) is { } block)
        {
            SetSelection(ObjectSelection.ForText(page, block));
        }
        else if (GetObjects(page)?.FirstOrDefault(o => o.Index == objectIndex) is { } obj)
        {
            SetSelection(new ObjectSelection(page, obj, null));
        }
    }

    public async Task EditAsync(int page, Func<int> edit, string statusKey, Action<int>? after = null)
    {
        var result = -1;
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), async () => result = await Task.Run(edit));
        InvalidatePageData(page);
        RefreshDocState();
        if (ok)
        {
            SetStatus(Loc.Get(statusKey));
            after?.Invoke(result);
            if (Comments.IsVisible)
            {
                _ = Comments.RefreshAsync();
            }
        }

        Props.Refresh();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateToPage(int page)
    {
        if (page >= 0 && page < PageCount && page != View.CurrentPage)
        {
            View.GoToPage(page);
        }
    }

    public void OpenLink(string uri) => SystemService.OpenUrl(uri);

    public void SetStatus(string text) => StatusChanged?.Invoke(this, text);

    public void Notify(string message, bool isError = false) => Notified?.Invoke(this, (message, isError));

    // =====================================================================
    // Tìm kiếm
    // =====================================================================

    public async Task SearchAsync(string query, SearchOptions options)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        LastQuery = query;
        _hits.Clear();
        _hitIndex = -1;
        Search.Items.Clear();
        View.RedecorateAll();
        if (string.IsNullOrWhiteSpace(query))
        {
            Search.SetCount(0, false);
            return;
        }

        var pdf = Pdf;
        for (var page = 0; page < PageCount && !cts.IsCancellationRequested; page++)
        {
            Search.SetCount(_hits.Count, true);
            var p = page;
            PageTextLayout layout;
            try
            {
                layout = GetTextLayout(p) ?? await Task.Run(() => pdf.GetTextLayout(p), cts.Token);
            }
            catch (Exception ex) when (ex is PdfException or OperationCanceledException)
            {
                break;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _searchLayouts[p] = layout;
            var pageLabel = Loc.Format("Search_PageLabel", p + 1);
            foreach (var (start, length) in Services.TextSearch.FindAll(layout.Text, query, options))
            {
                var hit = new SearchHit(p, start, length, Services.TextSearch.Context(layout.Text, start, length));
                _hits.Add(hit);
                Search.Items.Add(new SearchResultItem(hit, pageLabel));
                if (_hits.Count == 1)
                {
                    ShowSearchResult(0);
                }
            }

            if (View.GetPageView(p) is { } pv)
            {
                DrawOverlays(pv, forms: false);
            }
        }

        Search.SetCount(_hits.Count, false);
    }

    public void SearchStep(int direction)
    {
        if (_hits.Count == 0)
        {
            return;
        }

        ShowSearchResult((((_hitIndex + direction) % _hits.Count) + _hits.Count) % _hits.Count);
    }

    public void ShowSearchResult(int index)
    {
        if (index < 0 || index >= _hits.Count)
        {
            return;
        }

        var oldPage = _hitIndex >= 0 && _hitIndex < _hits.Count ? _hits[_hitIndex].PageIndex : -1;
        _hitIndex = index;
        var hit = _hits[index];
        var rects = GetTextLayout(hit.PageIndex)?.GetRangeRects(hit.Start, hit.Start + hit.Length);
        if (rects is { Count: > 0 })
        {
            View.ScrollIntoView(hit.PageIndex, rects[0]);
        }
        else
        {
            View.GoToPage(hit.PageIndex);
        }

        Search.SelectResult(index);
        foreach (var pv in View.RealizedPages.Where(p => p.PageIndex == oldPage || p.PageIndex == hit.PageIndex))
        {
            DrawOverlays(pv, forms: false);
        }

        SetStatus(Loc.Format("Search_Position", index + 1, _hits.Count));
    }

    public void ShowSidePanel(SidePanelKind kind)
    {
        _sidePanel = _sidePanel == kind && kind != SidePanelKind.Search ? SidePanelKind.None : kind;
        Settings.ShowSidePanel = _sidePanel != SidePanelKind.None;
        ApplyPanelVisibility();
        if (_sidePanel == SidePanelKind.Search)
        {
            // Panel vừa hiện chưa layout xong → đặt focus sau một nhịp.
            Dispatcher.BeginInvoke(Search.FocusQuery, System.Windows.Threading.DispatcherPriority.Input);
        }
        else if (_sidePanel == SidePanelKind.Comments)
        {
            _ = Comments.RefreshAsync();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleProperties()
    {
        Settings.ShowProperties = !Settings.ShowProperties;
        SaveSettings();
        ApplyPanelVisibility();
    }

    private void ApplyPanelVisibility()
    {
        if (!Settings.ShowSidePanel && _sidePanel != SidePanelKind.None)
        {
            _sidePanel = SidePanelKind.None;
        }

        var side = _sidePanel != SidePanelKind.None;
        SideColumn.Width = side ? new GridLength(Math.Max(220, SideColumn.ActualWidth > 0 ? SideColumn.ActualWidth : 250)) : new GridLength(0);
        SidePanel.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
        SideSplitter.Visibility = SidePanel.Visibility;
        Thumbs.Visibility = _sidePanel == SidePanelKind.Pages ? Visibility.Visible : Visibility.Collapsed;
        Bookmarks.Visibility = _sidePanel == SidePanelKind.Bookmarks ? Visibility.Visible : Visibility.Collapsed;
        Search.Visibility = _sidePanel == SidePanelKind.Search ? Visibility.Visible : Visibility.Collapsed;
        Comments.Visibility = _sidePanel == SidePanelKind.Comments ? Visibility.Visible : Visibility.Collapsed;
        RailPages.IsChecked = _sidePanel == SidePanelKind.Pages;
        RailBookmarks.IsChecked = _sidePanel == SidePanelKind.Bookmarks;
        RailSearch.IsChecked = _sidePanel == SidePanelKind.Search;
        RailComments.IsChecked = _sidePanel == SidePanelKind.Comments;

        PropColumn.Width = Settings.ShowProperties ? new GridLength(300) : new GridLength(0);
        PropPanel.Visibility = Settings.ShowProperties ? Visibility.Visible : Visibility.Collapsed;
        PropSplitter.Visibility = PropPanel.Visibility;
    }

    private void OnRailClick(object sender, RoutedEventArgs e)
    {
        var kind = sender == RailPages ? SidePanelKind.Pages
            : sender == RailBookmarks ? SidePanelKind.Bookmarks
            : sender == RailSearch ? SidePanelKind.Search
            : SidePanelKind.Comments;
        Settings.ShowSidePanel = true;
        ShowSidePanel(kind);
    }

    // =====================================================================
    // Thanh nổi (trang / zoom)
    // =====================================================================

    private void UpdateFloatingBar()
    {
        PageBox.Text = (View.CurrentPage + 1).ToString(CultureInfo.InvariantCulture);
        PageCountText.Text = Loc.Format("Toolbar_PageCount", PageCount);
        ZoomButton.Content = (View.Zoom * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    private void OnPageBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (int.TryParse(PageBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page))
        {
            View.GoToPage(page - 1);
        }

        UpdateFloatingBar();
        View.FocusView();
    }

    private void BuildZoomMenu()
    {
        foreach (var percent in new[] { 50, 75, 100, 125, 150, 200, 300, 400 })
        {
            var item = new MenuItem { Header = percent.ToString(CultureInfo.InvariantCulture) + "%" };
            item.Click += (_, _) => View.SetZoom(ZoomMode.Custom, percent / 100.0);
            ZoomMenu.Items.Add(item);
        }
    }

    private void OnZoomButtonClick(object sender, RoutedEventArgs e)
    {
        ZoomMenu.PlacementTarget = ZoomButton;
        ZoomMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        ZoomMenu.IsOpen = true;
    }

    // =====================================================================
    // Bận / lỗi
    // =====================================================================

    /// <summary>Chạy thao tác dài: lớp chờ (sau 180 ms), bắt lỗi và báo. False nếu lỗi.</summary>
    public async Task<bool> RunBusyAsync(string message, Func<Task> action)
    {
        _busyCount++;
        BusyText.Text = message;
        _busyTimer.Restart();
        StateChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (Dialogs.IsReportable(ex))
        {
            await Dialogs.ShowErrorAsync(ex);
            return false;
        }
        finally
        {
            _busyCount--;
            if (_busyCount == 0)
            {
                _busyTimer.Stop();
                BusyOverlay.Visibility = Visibility.Collapsed;
            }

            RefreshDocState();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowBanner(string message)
    {
        Banner.Message = message;
        Banner.IsOpen = true;
    }

    private void SaveSettings() => _settingsService.Save(Settings);

    private void LoadOptionsFromSettings()
    {
        if (RgbColor.TryParseHex(Settings.AnnotationColor, out var a))
        {
            Options.MarkupColor = a;
        }

        if (RgbColor.TryParseHex(Settings.InkColor, out var ink))
        {
            Options.InkColor = ink;
        }

        Options.InkWidth = Math.Clamp(Settings.InkWidth, 0.5, 12);
        var family = Pdf.Fonts.Families.Contains(Settings.TextFontFamily) ? Settings.TextFontFamily : Pdf.Fonts.DefaultFamily;
        var color = RgbColor.TryParseHex(Settings.TextColor, out var tc) ? tc : RgbColor.Black;
        Options.TextStyle = new TextStyle(family, Math.Clamp(Settings.TextFontSize, 4, 200), false, false, color);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        View.Clear();
        Pdf.Dispose();
        GC.SuppressFinalize(this);
    }
}
