using System.Globalization;
using Microsoft.Win32;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using PDFEditorApp.Views.Tools;

namespace PDFEditorApp.Views;

// Lệnh của tab tài liệu (MainWindow / panel gọi vào). Mỗi nhóm lệnh có file log trong docs/.
public partial class DocumentWorkspace
{
    // =====================================================================
    // Lưu / đóng  (docs/save.md)
    // =====================================================================

    public async Task<bool> SaveAsync(bool saveAs)
    {
        await WaitIdleAsync();
        RefreshDocState();
        var path = FilePath;
        if (saveAs || path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = Loc.Get("Filter_Pdf"),
                Title = Loc.Get("Dialog_SaveTitle"),
                FileName = path is null ? Loc.Get("Doc_UntitledFile") : System.IO.Path.GetFileName(path),
                InitialDirectory = path is null ? SystemService.DocumentsDirectory : System.IO.Path.GetDirectoryName(path),
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog(OwnerWindow) != true)
            {
                return false;
            }

            path = dialog.FileName;
        }
        else if (Settings.ConfirmBeforeOverwrite &&
                 !await Dialogs.ConfirmAsync(Loc.Get("Dialog_SaveTitle"), Loc.Format("Msg_ConfirmOverwrite", System.IO.Path.GetFileName(path)), Loc.Get("Common_Save")))
        {
            return false;
        }

        var target = path;
        var ok = await RunBusyAsync(Loc.Get("Status_Saving"), () => Task.Run(() => Pdf.SaveToFile(target)));
        if (ok)
        {
            Settings.AddRecentFile(System.IO.Path.GetFullPath(target));
            SaveSettings();
            Notify(Loc.Format("Status_Saved", System.IO.Path.GetFileName(target)));
            _docInfo = await Task.Run(Pdf.GetDocumentInfo);
            Props.Refresh();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return ok;
    }

    /// <summary>Có thay đổi chưa lưu → hỏi Lưu / Không lưu / Hủy. True nếu được phép đóng.</summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        await WaitIdleAsync();
        RefreshDocState();
        if (!IsModified)
        {
            return true;
        }

        return await Dialogs.AskSaveAsync(DisplayName) switch
        {
            SaveChoice.Save => await SaveAsync(saveAs: false),
            SaveChoice.DontSave => true,
            _ => false,
        };
    }

    private async Task WaitIdleAsync()
    {
        View.FocusView(); // kết thúc ô sửa đang mở (commit khi mất focus)
        await Task.Delay(30);
        while (_busyCount > 0)
        {
            await Task.Delay(40);
        }
    }

    // =====================================================================
    // Hoàn tác / làm lại  (docs/undo-redo.md)
    // =====================================================================

    public async Task UndoRedoAsync(bool undo)
    {
        var changed = false;
        await RunBusyAsync(Loc.Get("Status_Working"), async () => changed = await Task.Run(() => undo ? Pdf.Undo() : Pdf.Redo()));
        if (changed)
        {
            await AfterStructureChangeAsync(View.CurrentPage);
            SetStatus(Loc.Get(undo ? "Status_Undone" : "Status_Redone"));
        }
    }

    /// <summary>Số trang / nội dung nhiều trang đổi: dựng lại vùng xem, thumbnail, panel.</summary>
    private async Task AfterStructureChangeAsync(int page, IReadOnlyCollection<int>? organizeSelection = null)
    {
        RefreshDocState();
        InvalidateAllData();
        _selection = null;
        _hits.Clear();
        _hitIndex = -1;
        var sizes = await Task.Run(Pdf.GetPageSizes);
        View.UpdatePages(sizes, Math.Clamp(page, 0, Math.Max(0, sizes.Count - 1)));
        View.InvalidateAll();
        await Thumbs.RebuildAsync(View.CurrentPage);
        if (IsOrganizing)
        {
            await Organize.RebuildAsync(organizeSelection);
        }

        await Bookmarks.LoadAsync();
        _docInfo = await Task.Run(Pdf.GetDocumentInfo);
        if (Comments.IsVisible)
        {
            await Comments.RefreshAsync();
        }

        UpdateFloatingBar();
        Props.Refresh();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // =====================================================================
    // Trang  (docs/page-manage.md)
    // =====================================================================

    public void SetOrganizeMode(bool on)
    {
        if (on == IsOrganizing)
        {
            return;
        }

        Organize.Visibility = on ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        FloatingBar.Visibility = on ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        if (on)
        {
            _ = Organize.RebuildAsync([View.CurrentPage]);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task PageOperationAsync(Func<int> operation, string status, IReadOnlyCollection<int>? organizeSelection = null)
    {
        var page = View.CurrentPage;
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), async () => page = await Task.Run(operation));
        await AfterStructureChangeAsync(page, organizeSelection);
        if (ok)
        {
            Notify(status);
        }
    }

    public Task RotatePagesAsync(IReadOnlyList<int> pages, int quarterTurns) =>
        PageOperationAsync(() =>
        {
            Pdf.RotatePages(pages, quarterTurns);
            return View.CurrentPage;
        }, Loc.Format("Status_PagesRotated", pages.Count, quarterTurns * 90 % 360), pages.ToList());

    public async Task DeletePagesAsync(IReadOnlyList<int> pages)
    {
        if (!await Dialogs.ConfirmAsync(Loc.Get("Menu_DeletePage"), Loc.Format("Msg_ConfirmDeletePages", pages.Count), Loc.Get("Panel_Delete"), danger: true))
        {
            return;
        }

        await PageOperationAsync(() =>
        {
            Pdf.DeletePages(pages);
            return Math.Min(pages.Min(), Pdf.PageCount - 1);
        }, Loc.Format("Status_PagesDeleted", pages.Count));
    }

    public Task DuplicatePagesAsync(IReadOnlyList<int> pages) =>
        PageOperationAsync(() =>
        {
            Pdf.DuplicatePages(pages);
            return pages.Max() + 1;
        }, Loc.Format("Status_PagesDuplicated", pages.Count));

    public Task MovePagesAsync(IReadOnlyList<int> pages, int dest) =>
        PageOperationAsync(() =>
        {
            Pdf.MovePages(pages, dest);
            return dest;
        }, Loc.Get("Status_PagesMoved"), Enumerable.Range(dest, pages.Count).ToList());

    public Task InsertBlankPageAsync(int index)
    {
        var size = Pdf.IsOpen && PageCount > 0 ? Pdf.GetPageInfo(Math.Clamp(View.CurrentPage, 0, PageCount - 1)) : null;
        var (w, h) = size is null ? (PdfService.A4Width, PdfService.A4Height) : (size.Width, size.Height);
        return PageOperationAsync(() =>
        {
            Pdf.InsertBlankPage(index, w, h);
            return index;
        }, Loc.Format("Status_PageInserted", index + 1));
    }

    public async Task InsertPagesFromFileAsync(int index)
    {
        var dialog = new OpenFileDialog { Filter = Loc.Get("Filter_Pdf"), Title = Loc.Get("Dialog_ImportTitle"), Multiselect = true };
        if (dialog.ShowDialog(OwnerWindow) != true)
        {
            return;
        }

        var total = 0;
        var insertAt = index;
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), async () =>
        {
            foreach (var file in dialog.FileNames)
            {
                var data = await FileService.ReadAllBytesAsync(file);
                var count = 0;
                if (await TryWithPasswordAsync(System.IO.Path.GetFileName(file), pw => count = Pdf.ImportPages(data, pw, insertAt)))
                {
                    insertAt += count;
                    total += count;
                }
            }
        });
        await AfterStructureChangeAsync(index);
        if (ok && total > 0)
        {
            Notify(Loc.Format("Status_PagesImported", total));
        }
    }

    public async Task ExtractPagesAsync(IReadOnlyList<int> pages)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Loc.Get("Filter_Pdf"),
            Title = Loc.Get("Menu_ExtractPages"),
            FileName = BaseName + "_" + Loc.Get("Extract_Suffix"),
            DefaultExt = ".pdf",
            AddExtension = true,
        };
        if (dialog.ShowDialog(OwnerWindow) != true)
        {
            return;
        }

        var path = dialog.FileName;
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), () => Task.Run(() =>
        {
            var bytes = Pdf.ExtractPages(pages);
            FileService.WriteAtomic(path, s => s.Write(bytes));
        }));
        if (ok)
        {
            Notify(Loc.Format("Status_PagesExtracted", pages.Count, System.IO.Path.GetFileName(path)));
        }
    }

    /// <summary>Tách tài liệu thành nhiều file, mỗi file N trang.</summary>
    public async Task SplitAsync()
    {
        var input = TextInputDialog.Ask(OwnerWindow, Loc.Get("Menu_Split"), Loc.Format("Split_Prompt", PageCount), "1",
            validate: s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 1 && n < PageCount ? null : Loc.Get("Split_Invalid"));
        if (input is null)
        {
            return;
        }

        var size = int.Parse(input, CultureInfo.InvariantCulture);
        var folderDialog = new OpenFolderDialog { Title = Loc.Get("Dialog_ExportFolderTitle") };
        if (folderDialog.ShowDialog(OwnerWindow) != true)
        {
            return;
        }

        var folder = folderDialog.FolderName;
        var count = 0;
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), () => Task.Run(() =>
        {
            var digits = PageCount.ToString(CultureInfo.InvariantCulture).Length;
            for (var start = 0; start < PageCount; start += size)
            {
                var pages = Enumerable.Range(start, Math.Min(size, PageCount - start)).ToList();
                var bytes = Pdf.ExtractPages(pages);
                var name = $"{BaseName}_{(start + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}-{(pages[^1] + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}.pdf";
                FileService.WriteAtomic(System.IO.Path.Combine(folder, name), s => s.Write(bytes));
                count++;
            }
        }));
        if (ok)
        {
            Notify(Loc.Format("Status_Split", count));
            SystemService.RevealInExplorer(folder);
        }
    }

    private string BaseName => FilePath is null ? Loc.Get("Doc_UntitledFile") : System.IO.Path.GetFileNameWithoutExtension(FilePath);

    // =====================================================================
    // Chữ chọn, nhận xét, object  (docs/text-select.md, annotations.md, page-objects.md)
    // =====================================================================

    public void CopySelection()
    {
        if (_selection is TextRangeSelection t && GetTextLayout(t.Page) is { } layout)
        {
            SelectTool.TryCopy(layout.GetRangeText(t.Start, t.End));
            Notify(Loc.Get("Status_Copied"));
        }
    }

    /// <summary>Áp dụng tô sáng / gạch chân / gạch ngang cho chữ đang chọn; không có chữ chọn → bật công cụ.</summary>
    public void ApplyMarkupToSelection(AnnotationKind kind)
    {
        var tool = kind switch
        {
            AnnotationKind.Underline => ToolId.Underline,
            AnnotationKind.StrikeOut => ToolId.StrikeOut,
            AnnotationKind.Squiggly => ToolId.Squiggly,
            _ => ToolId.Highlight,
        };
        if (_selection is TextRangeSelection { Start: var s, End: var e } selection && e > s)
        {
            ((MarkupTool)_tools[tool]).Apply(selection);
        }
        else
        {
            SetTool(tool);
        }
    }

    public void SaveSelectedAnnotationContents(string text)
    {
        if (_selection is AnnotationSelection a)
        {
            _ = EditAsync(a.Page, () =>
            {
                Pdf.SetAnnotationContents(a.Page, a.Annotation.Index, text);
                return -1;
            }, "Status_CommentSaved");
        }
    }

    public void DeleteSelectedAnnotation()
    {
        if (_selection is AnnotationSelection a)
        {
            DeleteAnnotation(a.Annotation);
        }
    }

    public void DeleteAnnotation(AnnotationInfo annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        SetSelection(null);
        _ = EditAsync(annotation.PageIndex, () =>
        {
            Pdf.DeleteAnnotation(annotation.PageIndex, annotation.Index);
            return -1;
        }, "Status_AnnotationDeleted");
    }

    public void ShowAnnotation(AnnotationInfo annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (_activeTool != ToolId.Select)
        {
            SetTool(ToolId.Select);
        }

        View.ScrollIntoView(annotation.PageIndex, annotation.ViewRect);
        SetSelection(new AnnotationSelection(annotation.PageIndex, annotation));
    }

    public void ApplyTextEdit(string text)
    {
        if (_selection is ObjectSelection { Text: { } t } sel)
        {
            var style = Options.TextStyle;
            var page = sel.Page;
            _ = EditAsync(page, () => Pdf.UpdateTextBlock(page, t, text, style), "Status_TextUpdated",
                index => SelectObjectAfterEdit(page, index));
        }
    }

    public void DeleteSelectedObject()
    {
        if (_selection is ObjectSelection sel)
        {
            ((EditContentTool)_tools[ToolId.EditContent]).DeleteSelected(sel);
        }
    }

    public void SetToolColor(RgbColor color, bool ink)
    {
        if (ink)
        {
            Options.InkColor = color;
        }
        else
        {
            Options.MarkupColor = color;
        }

        SaveToolOptions();
    }

    public void SaveToolOptions()
    {
        Settings.AnnotationColor = Options.MarkupColor.ToHex();
        Settings.InkColor = Options.InkColor.ToHex();
        Settings.InkWidth = Options.InkWidth;
        SaveSettings();
    }

    public void SetTextStyle(TextStyle style)
    {
        Options.TextStyle = style;
        Settings.TextFontFamily = style.FontFamily;
        Settings.TextFontSize = style.FontSize;
        Settings.TextColor = style.Color.ToHex();
        SaveSettings();
    }

    /// <summary>Chọn ảnh từ máy rồi bật công cụ đặt ảnh.</summary>
    public void BeginInsertImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = Loc.Get("Filter_Images") + "|" + ImageCodec.OpenFilter,
            Title = Loc.Get("Dialog_InsertImageTitle"),
            InitialDirectory = SystemService.PicturesDirectory,
        };
        if (dialog.ShowDialog(OwnerWindow) != true)
        {
            return;
        }

        try
        {
            Options.PendingImage = ImageCodec.Load(dialog.FileName);
            SetTool(ToolId.PlaceImage);
        }
        catch (PdfException ex)
        {
            _ = Dialogs.ShowErrorAsync(ex);
        }
    }

    // =====================================================================
    // Điền & Ký  (docs/sign.md)
    // =====================================================================

    public void SetStamp(StampKind kind)
    {
        Options.Stamp = kind;
        Options.PendingImage = null;
        SetTool(ToolId.Stamp);
    }

    public void UseSignature(SavedSignature signature)
    {
        Options.Signature = signature;
        SetStamp(StampKind.Signature);
    }

    public void CreateSignature()
    {
        var dialog = new SignatureDialog(Pdf.Fonts.HandwritingFamilies) { Owner = OwnerWindow };
        if (dialog.ShowDialog() == true && dialog.Result is { } signature)
        {
            Settings.Signatures.Add(signature);
            SaveSettings();
            UseSignature(signature);
        }
    }

    public void DeleteSignature(SavedSignature signature)
    {
        Settings.Signatures.Remove(signature);
        SaveSettings();
        Props.Refresh();
    }

    // =====================================================================
    // Che thông tin, watermark, đầu/chân trang, mật khẩu  (docs/protect.md, watermark.md, header-footer.md)
    // =====================================================================

    public async void ApplyRedactions()
    {
        var pending = Options.Redactions.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        if (pending.Count == 0)
        {
            Notify(Loc.Get("Redact_None"));
            return;
        }

        if (!await Dialogs.ConfirmAsync(Loc.Get("Tool_Redact"), Loc.Format("Redact_Confirm", pending.Count), Loc.Get("Props_ApplyRedactions"), danger: true))
        {
            return;
        }

        var ok = await RunBusyAsync(Loc.Get("Status_Working"), () => Task.Run(() =>
        {
            foreach (var (page, rects) in pending)
            {
                Pdf.ApplyRedactions(page, rects);
            }
        }));
        if (ok)
        {
            Options.Redactions.Clear();
            Notify(Loc.Format("Status_Redacted", pending.Count));
        }

        await AfterStructureChangeAsync(View.CurrentPage);
    }

    public void ClearRedactions()
    {
        Options.Redactions.Clear();
        View.RedecorateAll();
        Props.Refresh();
    }

    public async Task AddWatermarkAsync()
    {
        if (StampDialog.AskWatermark(OwnerWindow, Pdf.Fonts.Families, PageCount) is not { } options)
        {
            return;
        }

        await StampOperationAsync(() => Pdf.AddWatermark(options), Loc.Get("Status_WatermarkAdded"));
    }

    public Task RemoveWatermarksAsync() =>
        StampOperationAsync(() => Pdf.RemoveWatermarks(), Loc.Get("Status_WatermarkRemoved"));

    public async Task AddHeaderFooterAsync()
    {
        if (StampDialog.AskHeaderFooter(OwnerWindow, Pdf.Fonts.Families, PageCount) is not { } options)
        {
            return;
        }

        await StampOperationAsync(() => Pdf.AddHeaderFooter(options), Loc.Get("Status_HeaderFooterAdded"));
    }

    public Task RemoveHeaderFootersAsync() =>
        StampOperationAsync(() => Pdf.RemoveHeaderFooters(), Loc.Get("Status_HeaderFooterRemoved"));

    private async Task StampOperationAsync(Action action, string status)
    {
        var ok = await RunBusyAsync(Loc.Get("Status_Working"), () => Task.Run(action));
        await AfterStructureChangeAsync(View.CurrentPage);
        if (ok)
        {
            Notify(status);
        }
    }

    public void Protect()
    {
        var dialog = new ProtectDialog { Owner = OwnerWindow };
        if (dialog.ShowDialog() == true && dialog.Result is { } options)
        {
            Pdf.SetSecurity(SecurityMode.Apply, options);
            RefreshDocState();
            Props.Refresh();
            StateChanged?.Invoke(this, EventArgs.Empty);
            Notify(Loc.Get("Status_PasswordSet"));
        }
    }

    public void RemovePassword()
    {
        if (!Pdf.IsEncrypted && Pdf.SecurityMode != SecurityMode.Apply)
        {
            Notify(Loc.Get("Status_NoPassword"));
            return;
        }

        Pdf.SetSecurity(SecurityMode.Remove, null);
        RefreshDocState();
        Props.Refresh();
        StateChanged?.Invoke(this, EventArgs.Empty);
        Notify(Loc.Get("Status_PasswordRemoved"));
    }

    // =====================================================================
    // Export / in  (docs/export.md, print.md)
    // =====================================================================

    public async Task ExportAsync(ExportFormat format)
    {
        await WaitIdleAsync();
        var dialog = new ExportDialog(PageCount, View.CurrentPage, Settings.ExportDpi, format) { Owner = OwnerWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var pages = dialog.Pages;
        format = dialog.Format;
        if (format is ExportFormat.Png or ExportFormat.Jpg)
        {
            Settings.ExportDpi = dialog.Dpi;
            SaveSettings();
            var folderDialog = new OpenFolderDialog { Title = Loc.Get("Dialog_ExportFolderTitle") };
            if (FilePath is not null)
            {
                folderDialog.InitialDirectory = System.IO.Path.GetDirectoryName(FilePath);
            }

            if (folderDialog.ShowDialog(OwnerWindow) != true)
            {
                return;
            }

            var folder = folderDialog.FolderName;
            var dpi = dialog.Dpi;
            IReadOnlyList<string> files = [];
            var progress = new Progress<int>(done => BusyText.Text = Loc.Format("Status_ExportingPage", done, pages.Count));
            var ok = await RunBusyAsync(Loc.Format("Status_ExportingPage", 0, pages.Count), async () =>
            {
                files = format == ExportFormat.Png
                    ? await Task.Run(() => Export.ExportPng(pages, folder, BaseName, dpi, progress))
                    : await Task.Run(() => Export.ExportImages(pages, folder, BaseName, dpi, ".jpg", ImageCodec.WriteJpeg, progress));
            });
            if (ok)
            {
                Notify(Loc.Format("Msg_ExportedImages", files.Count, folder));
                SystemService.RevealInExplorer(files.Count > 0 ? files[0] : folder);
            }

            return;
        }

        var (filter, ext) = format == ExportFormat.Docx ? ("Filter_Word", ".docx") : ("Filter_Text", ".txt");
        var saveDialog = new SaveFileDialog
        {
            Filter = Loc.Get(filter),
            Title = Loc.Get("Dialog_ExportTitle"),
            FileName = BaseName + ext,
            DefaultExt = ext,
            AddExtension = true,
        };
        if (saveDialog.ShowDialog(OwnerWindow) != true)
        {
            return;
        }

        var target = saveDialog.FileName;
        var saved = await RunBusyAsync(Loc.Get("Status_Exporting"), () => Task.Run(() =>
        {
            if (format == ExportFormat.Docx)
            {
                Export.ExportDocx(pages, target);
            }
            else
            {
                Export.ExportText(pages, target);
            }
        }));
        if (saved)
        {
            Notify(Loc.Format("Msg_ExportedText", target));
            SystemService.RevealInExplorer(target);
        }
    }

    public async Task PrintAsync()
    {
        await WaitIdleAsync();
        try
        {
            if (PrintHelper.Print(OwnerWindow, Pdf, DisplayName))
            {
                Notify(Loc.Get("Status_Printed"));
            }
        }
        catch (Exception ex) when (Dialogs.IsReportable(ex) || ex is System.Printing.PrintSystemException)
        {
            await Dialogs.ShowErrorAsync(ex);
        }
    }

    // =====================================================================
    // Thông tin tài liệu cho panel thuộc tính
    // =====================================================================

    public IEnumerable<(string Label, string Value)> DocumentSummary()
    {
        yield return (Loc.Get("Info_File"), DisplayName);
        yield return (Loc.Get("Info_Pages"), PageCount.ToString(CultureInfo.CurrentCulture));
        if (PageCount > 0)
        {
            PdfPageInfo? info = null;
            try
            {
                info = Pdf.GetPageInfo(Math.Clamp(View.CurrentPage, 0, PageCount - 1));
            }
            catch (PdfException)
            {
                // bỏ qua
            }

            if (info is not null)
            {
                yield return (Loc.Get("Info_PageSize"), Loc.Format("Info_PageSizeValue", info.Width * 25.4 / 72, info.Height * 25.4 / 72));
            }
        }

        if (_docInfo is { } d)
        {
            yield return (Loc.Get("Info_Version"), d.Version);
            yield return (Loc.Get("Info_Title"), string.IsNullOrEmpty(d.Title) ? "-" : d.Title);
            yield return (Loc.Get("Info_Author"), string.IsNullOrEmpty(d.Author) ? "-" : d.Author);
        }

        var security = Pdf.SecurityMode switch
        {
            SecurityMode.Apply => Loc.Get("Security_WillProtect"),
            SecurityMode.Remove => Loc.Get("Security_WillRemove"),
            _ => Loc.Get(Pdf.IsEncrypted ? "Security_Protected" : "Security_None"),
        };
        yield return (Loc.Get("Info_Security"), security);
        yield return (Loc.Get("Info_Forms"), Loc.Get(Pdf.HasForms ? "Common_Yes" : "Common_No"));
    }
}
