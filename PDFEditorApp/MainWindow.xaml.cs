using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using PDFEditorApp.Views;
using PDFEditorApp.Views.Tools;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace PDFEditorApp;

/// <summary>Một tab: Trang chủ (Workspace = null) hoặc một tài liệu.</summary>
public sealed class DocTab : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _modified;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DocumentWorkspace? Workspace { get; init; }

    public SymbolRegular Symbol => Workspace is null ? SymbolRegular.Home24 : SymbolRegular.DocumentPdf24;

    public Visibility CloseVisibility => Workspace is null ? Visibility.Collapsed : Visibility.Visible;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string? ToolTip => Workspace?.FilePath ?? Title;

    public bool Modified
    {
        get => _modified;
        set
        {
            if (Set(ref _modified, value))
            {
                OnChanged(nameof(ModifiedVisibility));
            }
        }
    }

    public Visibility ModifiedVisibility => _modified ? Visibility.Visible : Visibility.Collapsed;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnChanged(name);
        return true;
    }

    private void OnChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Cửa sổ chính (Fluent): thanh tiêu đề, dải tab tài liệu, ribbon, nội dung (Trang chủ / tab tài liệu),
/// thanh trạng thái, thông báo nổi. Lệnh được chuyển cho tab đang mở. Xem docs/ui-layout.md, docs/tabs.md.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly FontService _fonts;
    private readonly ObservableCollection<DocTab> _tabs = [];
    private readonly DocTab _homeTab;
    private readonly string? _startupFile;
    private bool _closeConfirmed;

    public MainWindow(SettingsService settingsService, AppSettings settings, FontService fonts, string? startupFile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _fonts = fonts;
        _startupFile = startupFile;

        _homeTab = new DocTab { Title = Loc.Get("Home_Tab") };
        _tabs.Add(_homeTab);
        TabStrip.ItemsSource = _tabs;
        TabStrip.SelectedItem = _homeTab;

        Home.OpenRequested += (_, _) => Fire(OpenWithDialogAsync);
        Home.NewRequested += (_, _) => Fire(NewDocumentAsync);
        Home.CombineRequested += (_, _) => Fire(CombineAsync);
        Home.ImagesRequested += (_, _) => Fire(ImagesToPdfAsync);
        Home.RecentRequested += (_, path) => Fire(() => OpenFileAsync(path));
        Home.ClearRecentRequested += (_, _) =>
        {
            _settings.RecentFiles.Clear();
            SaveSettings();
            RefreshRecent();
        };

        LocalizationService.Instance.LanguageChanged += (_, _) => RefreshAll();
        ThemeService.ThemeChanged += (_, _) => ThemeButton.Symbol = ThemeService.IsDark ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24;
        ThemeButton.Symbol = ThemeService.IsDark ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24;

        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_startupFile))
            {
                Fire(() => OpenFileAsync(_startupFile));
            }
        };
        RefreshRecent();
        RefreshAll();
        SetStatus(Loc.Get("Status_Ready"));
    }

    private DocumentWorkspace? Active => (TabStrip.SelectedItem as DocTab)?.Workspace;

    // =====================================================================
    // Tab
    // =====================================================================

    private DocumentWorkspace CreateWorkspace()
    {
        var ws = new DocumentWorkspace(_settingsService, _settings, _fonts) { Visibility = Visibility.Collapsed };
        ws.StateChanged += (_, _) =>
        {
            if (ReferenceEquals(ws, Active))
            {
                RefreshAll();
            }

            UpdateTab(ws);
        };
        ws.StatusChanged += (_, text) =>
        {
            if (ReferenceEquals(ws, Active))
            {
                SetStatus(text);
            }
        };
        ws.Notified += (_, n) => ShowSnackbar(n.Message, n.IsError);
        ContentHost.Children.Add(ws);
        return ws;
    }

    private void AddTab(DocumentWorkspace ws)
    {
        var tab = new DocTab { Workspace = ws, Title = ws.DisplayName };
        _tabs.Add(tab);
        TabStrip.SelectedItem = tab;
    }

    private void UpdateTab(DocumentWorkspace ws)
    {
        if (_tabs.FirstOrDefault(t => ReferenceEquals(t.Workspace, ws)) is { } tab)
        {
            tab.Title = ws.DisplayName;
            tab.Modified = ws.IsModified;
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabStrip.SelectedItem is null)
        {
            TabStrip.SelectedItem = _homeTab;
            return;
        }

        var active = Active;
        Home.Visibility = active is null ? Visibility.Visible : Visibility.Collapsed;
        foreach (var ws in ContentHost.Children.OfType<DocumentWorkspace>())
        {
            ws.Visibility = ReferenceEquals(ws, active) ? Visibility.Visible : Visibility.Collapsed;
        }

        if (active is null)
        {
            RefreshRecent();
        }

        RefreshAll();
        active?.View.FocusView();
    }

    private void OnTabCloseClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DocTab { Workspace: { } ws } })
        {
            Fire(() => CloseWorkspaceAsync(ws));
        }
    }

    private async Task<bool> CloseWorkspaceAsync(DocumentWorkspace ws)
    {
        if (_tabs.FirstOrDefault(t => ReferenceEquals(t.Workspace, ws)) is not { } tab)
        {
            return true;
        }

        TabStrip.SelectedItem = tab;
        if (!await ws.ConfirmCloseAsync())
        {
            return false;
        }

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        ContentHost.Children.Remove(ws);
        ws.Dispose();
        TabStrip.SelectedItem = _tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)];
        return true;
    }

    private void OnNextTab(object sender, ExecutedRoutedEventArgs e) =>
        TabStrip.SelectedIndex = (TabStrip.SelectedIndex + 1) % _tabs.Count;

    private void OnPreviousTab(object sender, ExecutedRoutedEventArgs e) =>
        TabStrip.SelectedIndex = (TabStrip.SelectedIndex - 1 + _tabs.Count) % _tabs.Count;

    // =====================================================================
    // Mở / tạo  (docs/open-file.md, docs/create-pdf.md)
    // =====================================================================

    private void OnNew(object sender, ExecutedRoutedEventArgs e) => Fire(NewDocumentAsync);

    private void OnOpen(object sender, ExecutedRoutedEventArgs e) => Fire(OpenWithDialogAsync);

    private async Task OpenWithDialogAsync()
    {
        var dialog = new OpenFileDialog { Filter = Loc.Get("Filter_Pdf"), Title = Loc.Get("Dialog_OpenTitle"), Multiselect = true };
        if (dialog.ShowDialog(this) == true)
        {
            foreach (var file in dialog.FileNames)
            {
                await OpenFileAsync(file);
            }
        }
    }

    public async Task OpenFileAsync(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (_tabs.FirstOrDefault(t => string.Equals(t.Workspace?.FilePath, full, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            TabStrip.SelectedItem = existing;
            return;
        }

        if (!File.Exists(full))
        {
            await Dialogs.ShowAsync(Loc.Get("Error_Title"), Loc.Format("Error_FileNotFound", full));
            _settings.RecentFiles.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
            SaveSettings();
            RefreshRecent();
            return;
        }

        await LoadIntoNewTabAsync(ws => ws.LoadFileAsync(full), Loc.Get("Status_Opening"));
        if (Active?.FilePath is not null)
        {
            _settings.AddRecentFile(full);
            SaveSettings();
            RefreshRecent();
            SetStatus(Loc.Format("Status_Opened", System.IO.Path.GetFileName(full)));
        }
    }

    private Task NewDocumentAsync() => LoadIntoNewTabAsync(async ws =>
    {
        await ws.LoadNewAsync();
        return true;
    }, Loc.Get("Status_Working"));

    private async Task CombineAsync()
    {
        var dialog = new OpenFileDialog { Filter = Loc.Get("Filter_Pdf"), Title = Loc.Get("Home_Combine"), Multiselect = true };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        var names = dialog.FileNames.ToList();
        await LoadIntoNewTabAsync(async ws =>
        {
            var files = new List<(byte[], string?)>();
            foreach (var file in names)
            {
                var data = await FileService.ReadAllBytesAsync(file);
                string? password = null;
                var ok = await ws.TryWithPasswordAsync(System.IO.Path.GetFileName(file), pw =>
                {
                    using var probe = new PdfService(_fonts);
                    probe.Open(data, pw, null);
                    password = pw;
                });
                if (!ok)
                {
                    return false;
                }

                files.Add((data, password));
            }

            await ws.LoadCombinedAsync(files);
            return true;
        }, Loc.Get("Status_Working"));
    }

    private async Task ImagesToPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = Loc.Get("Filter_Images") + "|" + ImageCodec.OpenFilter,
            Title = Loc.Get("Home_Images"),
            Multiselect = true,
            InitialDirectory = SystemService.PicturesDirectory,
        };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        var names = dialog.FileNames.ToList();
        await LoadIntoNewTabAsync(async ws =>
        {
            var images = await Task.Run(() => names.Select(ImageCodec.Load).ToList());
            await ws.LoadImagesAsync(images);
            return true;
        }, Loc.Get("Status_Working"));
    }

    /// <summary>Tạo tab mới, nạp tài liệu; lỗi hoặc hủy thì gỡ tab.</summary>
    private async Task LoadIntoNewTabAsync(Func<DocumentWorkspace, Task<bool>> load, string message)
    {
        var ws = CreateWorkspace();
        AddTab(ws);
        var loaded = false;
        await ws.RunBusyAsync(message, async () => loaded = await load(ws));
        if (!loaded)
        {
            var tab = _tabs.First(t => ReferenceEquals(t.Workspace, ws));
            _tabs.Remove(tab);
            ContentHost.Children.Remove(ws);
            ws.Dispose();
            TabStrip.SelectedItem = _tabs[^1];
            return;
        }

        UpdateTab(ws);
        RefreshAll();
    }

    // ---- Kéo thả file ----
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        Fire(async () =>
        {
            foreach (var f in files)
            {
                await OpenFileAsync(f);
            }
        });
    }

    private static List<string> GetDroppedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(FileService.IsPdfPath).ToList()
            : [];

    // ---- File gần đây ----
    private void RefreshRecent()
    {
        Home.SetRecentFiles(_settings.RecentFiles);
        if (FindResource("FileMenu") is ContextMenu menu && menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "RecentMenu") is { } recent)
        {
            recent.Items.Clear();
            foreach (var path in _settings.RecentFiles)
            {
                var item = new MenuItem { Header = new System.Windows.Controls.TextBlock { Text = path } };
                item.Click += (_, _) => Fire(() => OpenFileAsync(path));
                recent.Items.Add(item);
            }

            if (_settings.RecentFiles.Count == 0)
            {
                recent.Items.Add(new MenuItem { Header = Loc.Get("Menu_RecentEmpty"), IsEnabled = false });
            }
        }
    }

    // =====================================================================
    // Lệnh chuyển tới tab đang mở
    // =====================================================================

    private void OnSave(object sender, ExecutedRoutedEventArgs e) => Fire(async () => await Active!.SaveAsync(false));

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e) => Fire(async () => await Active!.SaveAsync(true));

    private void OnExport(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.ExportAsync(ExportFormat.Png));

    private void OnPrint(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.PrintAsync());

    private void OnCloseTab(object sender, ExecutedRoutedEventArgs e) => Fire(async () => await CloseWorkspaceAsync(Active!));

    private void OnUndo(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.UndoRedoAsync(true));

    private void OnRedo(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.UndoRedoAsync(false));

    private void OnFind(object sender, ExecutedRoutedEventArgs e) => Active!.ShowSidePanel(SidePanelKind.Search);

    private void OnFindNext(object sender, ExecutedRoutedEventArgs e) => Active!.SearchStep(1);

    private void OnFindPrevious(object sender, ExecutedRoutedEventArgs e) => Active!.SearchStep(-1);

    private void OnCancel(object sender, ExecutedRoutedEventArgs e)
    {
        var ws = Active!;
        if (ws.Selection is not null)
        {
            ws.SetSelection(null);
        }
        else if (ws.IsOrganizing)
        {
            ws.SetOrganizeMode(false);
        }
        else if (ws.ActiveTool != ToolId.Select)
        {
            ws.SetTool(ToolId.Select);
        }
    }

    private void OnTool(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string name && Enum.TryParse<ToolId>(name, out var tool))
        {
            var ws = Active!;
            if (tool is ToolId.Highlight or ToolId.Underline or ToolId.StrikeOut or ToolId.Squiggly && ws.Selection is TextRangeSelection)
            {
                ws.ApplyMarkupToSelection(tool switch
                {
                    ToolId.Underline => AnnotationKind.Underline,
                    ToolId.StrikeOut => AnnotationKind.StrikeOut,
                    ToolId.Squiggly => AnnotationKind.Squiggly,
                    _ => AnnotationKind.Highlight,
                });
                RefreshAll();
                return;
            }

            if (tool == ToolId.Stamp && ws.Options.Stamp != StampKind.Signature)
            {
                if (_settings.Signatures.Count == 0)
                {
                    ws.CreateSignature();
                    RefreshAll();
                    return;
                }

                ws.Options.Signature ??= _settings.Signatures[^1];
                ws.Options.Stamp = StampKind.Signature;
            }

            ws.SetOrganizeMode(false);
            ws.SetTool(ws.ActiveTool == tool && tool != ToolId.Select ? ToolId.Select : tool);
        }

        RefreshAll();
    }

    private void OnSelectToolKey(object sender, ExecutedRoutedEventArgs e) => Active!.SetTool(ToolId.Select);

    private void OnEditToolKey(object sender, ExecutedRoutedEventArgs e) => Active!.SetTool(ToolId.EditContent);

    private void OnAddTextToolKey(object sender, ExecutedRoutedEventArgs e) => Active!.SetTool(ToolId.AddText);

    private void OnHighlightToolKey(object sender, ExecutedRoutedEventArgs e) => Active!.ApplyMarkupToSelection(AnnotationKind.Highlight);

    private void OnZoomIn(object sender, ExecutedRoutedEventArgs e) => Active!.View.ZoomStep(1);

    private void OnZoomOut(object sender, ExecutedRoutedEventArgs e) => Active!.View.ZoomStep(-1);

    private void OnActualSize(object sender, ExecutedRoutedEventArgs e) => Active!.View.SetZoom(ZoomMode.Custom, 1);

    private void OnFitPage(object sender, ExecutedRoutedEventArgs e) => Active!.View.SetZoom(ZoomMode.FitPage);

    private void OnFitWidth(object sender, ExecutedRoutedEventArgs e) => Active!.View.SetZoom(ZoomMode.FitWidth);

    private void OnFirstPage(object sender, ExecutedRoutedEventArgs e) => Active!.View.GoToPage(0);

    private void OnPreviousPage(object sender, ExecutedRoutedEventArgs e) => Active!.View.GoToPage(Active.View.CurrentPage - 1);

    private void OnNextPage(object sender, ExecutedRoutedEventArgs e) => Active!.View.GoToPage(Active.View.CurrentPage + 1);

    private void OnLastPage(object sender, ExecutedRoutedEventArgs e) => Active!.View.GoToPage(Active.PageCount - 1);

    private void OnRotateRight(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.RotatePagesAsync([Active.CurrentPage], 1));

    private void OnRotateLeft(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.RotatePagesAsync([Active.CurrentPage], 3));

    private void OnInsertBlankPage(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.InsertBlankPageAsync(Active.CurrentPage + 1));

    private void OnDeletePage(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.DeletePagesAsync([Active.CurrentPage]));

    private void OnDuplicatePage(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.DuplicatePagesAsync([Active.CurrentPage]));

    private void OnExtractPages(object sender, ExecutedRoutedEventArgs e) => Fire(ExtractRangeAsync);

    private void OnMovePageUp(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.MovePagesAsync([Active.CurrentPage], Active.CurrentPage - 1));

    private void OnMovePageDown(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.MovePagesAsync([Active.CurrentPage], Active.CurrentPage + 1));

    private void OnImportPages(object sender, ExecutedRoutedEventArgs e) => Fire(() => Active!.InsertPagesFromFileAsync(Active.CurrentPage + 1));

    private async Task ExtractRangeAsync()
    {
        var ws = Active!;
        IReadOnlyList<int> pages = [];
        var input = TextInputDialog.Ask(this, Loc.Get("Menu_ExtractPages"), Loc.Get("Extract_Prompt"),
            (ws.CurrentPage + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            validate: s => PageRangeParser.TryParse(s, ws.PageCount, out pages) ? null : Loc.Get("Export_InvalidRange"));
        if (input is not null)
        {
            await ws.ExtractPagesAsync(pages);
        }
    }

    /// <summary>Lệnh theo tên (ribbon / menu Tệp).</summary>
    private void OnAction(object sender, ExecutedRoutedEventArgs e)
    {
        var ws = Active;
        switch (e.Parameter as string)
        {
            case "Combine":
                Fire(CombineAsync);
                break;
            case "Images":
                Fire(ImagesToPdfAsync);
                break;
            case "About":
                new AboutDialog { Owner = this }.ShowDialog();
                break;
            case "Exit":
                Close();
                break;
            case "LayoutContinuous":
                ws?.View.SetLayout(ViewLayout.Continuous);
                break;
            case "LayoutSingle":
                ws?.View.SetLayout(ViewLayout.SinglePage);
                break;
            case "LayoutTwo":
                ws?.View.SetLayout(ViewLayout.TwoPage);
                break;
            case "Night" when ws is not null:
                ws.View.NightMode = !ws.View.NightMode;
                break;
            case "SidePanel" when ws is not null:
                ws.ShowSidePanel(ws.SidePanelShown == SidePanelKind.None ? SidePanelKind.Pages : ws.SidePanelShown);
                break;
            case "Properties":
                ws?.ToggleProperties();
                break;
            case "InsertImage":
                ws?.BeginInsertImage();
                break;
            case "Watermark":
                Fire(() => ws!.AddWatermarkAsync());
                break;
            case "RemoveWatermark":
                Fire(() => ws!.RemoveWatermarksAsync());
                break;
            case "HeaderFooter":
                Fire(() => ws!.AddHeaderFooterAsync());
                break;
            case "RemoveHeaderFooter":
                Fire(() => ws!.RemoveHeaderFootersAsync());
                break;
            case "Comments":
                ws?.ShowSidePanel(SidePanelKind.Comments);
                break;
            case "Organize" when ws is not null:
                ws.SetOrganizeMode(!ws.IsOrganizing);
                break;
            case "Split":
                Fire(() => ws!.SplitAsync());
                break;
            case "StampCheck":
                ws?.SetStamp(StampKind.Check);
                break;
            case "StampCross":
                ws?.SetStamp(StampKind.Cross);
                break;
            case "StampDate":
                ws?.SetStamp(StampKind.Date);
                break;
            case "NewSignature":
                ws?.CreateSignature();
                break;
            case "FormInfo" when ws is not null:
                ShowSnackbar(Loc.Get(ws.Pdf.HasForms ? "Banner_Forms" : "Forms_None"), false);
                break;
            case "Protect":
                ws?.Protect();
                break;
            case "RemovePassword":
                ws?.RemovePassword();
                break;
            case "ApplyRedactions":
                ws?.ApplyRedactions();
                break;
            case "ExportPng":
                Fire(() => ws!.ExportAsync(ExportFormat.Png));
                break;
            case "ExportJpg":
                Fire(() => ws!.ExportAsync(ExportFormat.Jpg));
                break;
            case "ExportDocx":
                Fire(() => ws!.ExportAsync(ExportFormat.Docx));
                break;
            case "ExportText":
                Fire(() => ws!.ExportAsync(ExportFormat.Text));
                break;
        }

        RefreshAll();
    }

    private void OnFileMenuClick(object sender, RoutedEventArgs e)
    {
        if (FindResource("FileMenu") is ContextMenu menu)
        {
            menu.PlacementTarget = FileButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void OnRibbonTabChecked(object sender, RoutedEventArgs e)
    {
        if (PanelView is null)
        {
            return;
        }

        PanelView.Visibility = TabViewRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelEdit.Visibility = TabEditRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelComment.Visibility = TabCommentRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelPages.Visibility = TabPagesRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelSign.Visibility = TabSignRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelProtect.Visibility = TabProtectRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelConvert.Visibility = TabConvertRibbon.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // =====================================================================
    // Cài đặt, giao diện
    // =====================================================================

    private void OnSettings(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ThemeService.Apply(_settings.Theme);
            LocalizationService.Instance.SetLanguage(_settings.Language);
            foreach (var ws in ContentHost.Children.OfType<DocumentWorkspace>())
            {
                ws.Pdf.AnnotationAuthor = string.IsNullOrWhiteSpace(_settings.AnnotationAuthor) ? SystemService.UserName : _settings.AnnotationAuthor;
            }

            SaveSettings();
        }
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        _settings.Theme = ThemeService.IsDark ? AppTheme.Light : AppTheme.Dark;
        ThemeService.Apply(_settings.Theme);
        SaveSettings();
    }

    private void RefreshAll()
    {
        var ws = Active;
        _homeTab.Title = Loc.Get("Home_Tab");
        Ribbon.Visibility = ws is null ? Visibility.Collapsed : Visibility.Visible;
        TitleDocText.Text = ws is null ? string.Empty : "— " + ws.DisplayName + (ws.IsModified ? " •" : string.Empty);
        Title = ws is null ? Loc.Get("App_Title") : $"{ws.DisplayName} - {Loc.Get("App_Title")}";
        ModifiedText.Visibility = ws?.IsModified == true ? Visibility.Visible : Visibility.Collapsed;
        PageStatusText.Text = ws is null ? string.Empty : Loc.Format("Status_Page", ws.CurrentPage + 1, ws.PageCount);
        ZoomStatusText.Text = ws is null ? string.Empty : Math.Round(ws.View.Zoom * 100) + "%";
        if (ws is not null)
        {
            var tool = ws.ActiveTool.ToString();
            foreach (var toggle in FindToggles(RibbonBody))
            {
                if (toggle.Command == AppCommands.Tool)
                {
                    toggle.IsChecked = Equals(toggle.CommandParameter, tool);
                }
            }

            LayoutContinuousToggle.IsChecked = ws.View.Layout == ViewLayout.Continuous;
            LayoutSingleToggle.IsChecked = ws.View.Layout == ViewLayout.SinglePage;
            LayoutTwoToggle.IsChecked = ws.View.Layout == ViewLayout.TwoPage;
            NightToggle.IsChecked = ws.View.NightMode;
            SidePanelToggle.IsChecked = ws.SidePanelShown != SidePanelKind.None;
            PropertiesToggle.IsChecked = _settings.ShowProperties;
            OrganizeToggle.IsChecked = ws.IsOrganizing;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private static IEnumerable<RibbonToggle> FindToggles(Panel root) =>
        root.Children.OfType<Panel>().SelectMany(p => p.Children.OfType<RibbonToggle>());

    private void SetStatus(string text) => StatusText.Text = text;

    private void ShowSnackbar(string message, bool isError)
    {
        var snackbar = new Snackbar(Snackbars)
        {
            Title = Loc.Get(isError ? "Error_Title" : "App_Title"),
            Content = message,
            Appearance = isError ? ControlAppearance.Danger : ControlAppearance.Secondary,
            Icon = new SymbolIcon(isError ? SymbolRegular.ErrorCircle24 : SymbolRegular.CheckmarkCircle24),
            Timeout = TimeSpan.FromSeconds(3),
        };
        snackbar.Show();
        SetStatus(message);
    }

    private void SaveSettings() => _settingsService.Save(_settings);

    // =====================================================================
    // CanExecute
    // =====================================================================

    private void CanAlways(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

    private void CanWithDocument(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Active is { IsBusy: false };

    private void CanAction(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = e.Parameter is "Combine" or "Images" or "About" or "Exit" || Active is { IsBusy: false };

    private void CanUndo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Active is { IsBusy: false, CanUndo: true };

    private void CanRedo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Active is { IsBusy: false, CanRedo: true };

    private void CanGoPrevious(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Active is { IsBusy: false, CurrentPage: > 0 };

    private void CanGoNext(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = Active is { IsBusy: false } ws && ws.CurrentPage < ws.PageCount - 1;

    private void CanDeletePage(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Active is { IsBusy: false, PageCount: > 1 };

    // =====================================================================
    // Đóng cửa sổ
    // =====================================================================

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed)
        {
            SaveWindowSettings();
            return;
        }

        e.Cancel = true;
        Dispatcher.BeginInvoke(async () =>
        {
            foreach (var ws in _tabs.Select(t => t.Workspace).OfType<DocumentWorkspace>().ToList())
            {
                if (!await CloseWorkspaceAsync(ws))
                {
                    return;
                }
            }

            _closeConfirmed = true;
            Close();
        });
    }

    private void SaveWindowSettings()
    {
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
        }

        SaveSettings();
    }

    /// <summary>Chạy tác vụ async từ event handler, báo lỗi thay vì để văng app.</summary>
    private static async void Fire(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (Dialogs.IsReportable(ex))
        {
            await Dialogs.ShowErrorAsync(ex);
        }
    }
}
