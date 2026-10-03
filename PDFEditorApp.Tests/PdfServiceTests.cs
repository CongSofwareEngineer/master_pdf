using PDFEditorApp.Models;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Engine PDF: mở, render, sửa text, quản lý trang, undo/redo, lưu (docs/pdf-engine.md…).</summary>
public class PdfServiceTests
{
    [Fact]
    public void Open_InvalidBytes_ThrowsFormatError()
    {
        using var pdf = NewService();
        var ex = Assert.Throws<PdfException>(() => pdf.Open("not a pdf"u8.ToArray(), null, null));
        Assert.Equal(PdfErrorKind.Format, ex.Kind);
        Assert.False(pdf.IsOpen);
    }

    [Fact]
    public void Open_EmptyBytes_ThrowsFormatError()
    {
        using var pdf = NewService();
        Assert.Equal(PdfErrorKind.Format, Assert.Throws<PdfException>(() => pdf.Open([], null, null)).Kind);
    }

    [Fact]
    public void CreateNew_HasOneA4PageAndIsNotModified()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        Assert.Equal(1, pdf.PageCount);
        Assert.False(pdf.IsModified);
        var info = pdf.GetPageInfo(0);
        Assert.Equal(PdfService.A4Width, info.Width, 1);
        Assert.Equal(PdfService.A4Height, info.Height, 1);
    }

    [Fact]
    public void AddText_ThenSaveAndReopen_TextIsPreserved()
    {
        var data = CreatePdf(2);
        using var pdf = Open(data);
        Assert.Equal(2, pdf.PageCount);
        Assert.Contains("Page 1", pdf.GetPageText(0), StringComparison.Ordinal);
        Assert.Contains("Page 2", pdf.GetPageText(1), StringComparison.Ordinal);

        var objects = pdf.GetTextObjects(0);
        var obj = Assert.Single(objects);
        Assert.Equal("Page 1", obj.Text);
        Assert.Equal(14, obj.Style.FontSize, 1);
        Assert.Equal("Arial", obj.Style.FontFamily);
    }

    [Fact]
    public void AddText_IsPlacedBelowClickPoint()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 100, 200, "Hello", Style(20));
        var obj = Assert.Single(pdf.GetTextObjects(0));
        // Góc trên-trái của chữ nằm gần điểm click (hệ view, Y hướng xuống).
        Assert.InRange(obj.ViewBounds.X, 99, 104);
        Assert.InRange(obj.ViewBounds.Y, 195, 215);
    }

    [Fact]
    public void AddText_MultiLine_CreatesOneObjectPerLine()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 72, "Line one\nLine two", Style());
        var objects = pdf.GetTextObjects(0);
        Assert.Equal(2, objects.Count);
        Assert.True(objects[1].ViewBounds.Y > objects[0].ViewBounds.Y);
    }

    [Fact]
    public void AddText_Vietnamese_UsesUnicodeFontAndRoundTrips()
    {
        const string text = "Xin chào thế giới — Tiếng Việt có dấu";
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 72, text, Style());
        using var reopened = Open(pdf.SaveToBytes());
        Assert.Equal(text, Assert.Single(reopened.GetTextObjects(0)).Text);
    }

    [Fact]
    public void UpdateTextObject_ChangesTextAndStyle()
    {
        using var pdf = Open(CreatePdf());
        var obj = Assert.Single(pdf.GetTextObjects(0));
        var newIndex = pdf.UpdateTextObject(0, obj.ObjectIndex, "Edited text", Style(24, "Times New Roman", bold: true));
        Assert.True(newIndex >= 0);

        using var reopened = Open(pdf.SaveToBytes());
        var edited = Assert.Single(reopened.GetTextObjects(0));
        Assert.Equal("Edited text", edited.Text);
        Assert.Equal(24, edited.Style.FontSize, 1);
        Assert.Equal("Times New Roman", edited.Style.FontFamily);
        Assert.True(edited.Style.Bold);
        // Vẫn ở gần vị trí cũ.
        Assert.InRange(edited.ViewBounds.X, obj.ViewBounds.X - 2, obj.ViewBounds.X + 2);
    }

    [Fact]
    public void UpdateTextObject_EmptyText_DeletesObject()
    {
        using var pdf = Open(CreatePdf());
        var obj = Assert.Single(pdf.GetTextObjects(0));
        Assert.Equal(-1, pdf.UpdateTextObject(0, obj.ObjectIndex, string.Empty, obj.Style));
        Assert.Empty(pdf.GetTextObjects(0));
    }

    [Fact]
    public void DeleteTextObject_RemovesTextFromSavedFile()
    {
        using var pdf = Open(CreatePdf());
        var obj = Assert.Single(pdf.GetTextObjects(0));
        pdf.DeleteTextObject(0, obj.ObjectIndex);
        using var reopened = Open(pdf.SaveToBytes());
        Assert.Empty(reopened.GetTextObjects(0));
        Assert.DoesNotContain("Page 1", reopened.GetPageText(0), StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteTextObject_NonTextIndex_Throws()
    {
        using var pdf = Open(CreatePdf());
        Assert.Equal(PdfErrorKind.InvalidObject, Assert.Throws<PdfException>(() => pdf.DeleteTextObject(0, 99)).Kind);
        // Lỗi không làm hỏng tài liệu và không tạo bước undo.
        Assert.Single(pdf.GetTextObjects(0));
        Assert.False(pdf.CanUndo);
    }

    /// <summary>
    /// PDF có 2 font khác nhau nhưng trùng BaseFont (nhúng Arial ở 2 phiên làm việc khác nhau) —
    /// tình huống làm PDFium gộp nhầm font khi tái tạo nội dung (hay gặp ở PDF do Chrome tạo).
    /// </summary>
    private static byte[] CreateAmbiguousFontPdf()
    {
        using var first = NewService();
        first.CreateNew();
        first.AddText(0, 72, 72, "Dòng một tiếng Việt", Style());
        using var second = Open(first.SaveToBytes());
        second.AddText(0, 72, 120, "Dòng hai có dấu", Style());
        second.AddText(0, 72, 168, "Ba", Style());
        return second.SaveToBytes();
    }

    [Fact]
    public void Edit_OnPageWithAmbiguousFonts_KeepsOtherTextIntact()
    {
        using var pdf = Open(CreateAmbiguousFontPdf());
        var target = pdf.GetTextObjects(0).Single(o => o.Text == "Ba");
        pdf.UpdateTextObject(0, target.ObjectIndex, "Ba đã sửa", target.Style);

        using var reopened = Open(pdf.SaveToBytes());
        var texts = reopened.GetTextObjects(0).Select(o => o.Text).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["Ba đã sửa", "Dòng hai có dấu", "Dòng một tiếng Việt"], texts);
    }

    [Fact]
    public void DeleteAndMove_OnPageWithAmbiguousFonts_HideOriginal()
    {
        using var pdf = Open(CreateAmbiguousFontPdf());
        var first = pdf.GetTextObjects(0).Single(o => o.Text == "Dòng một tiếng Việt");
        pdf.DeleteTextObject(0, first.ObjectIndex);
        Assert.DoesNotContain(pdf.GetTextObjects(0), o => o.Text == "Dòng một tiếng Việt");

        var second = pdf.GetTextObjects(0).Single(o => o.Text == "Dòng hai có dấu");
        var newIndex = pdf.MoveTextObject(0, second.ObjectIndex, 0, 200);
        var moved = pdf.GetTextObjects(0).Single(o => o.ObjectIndex == newIndex);
        Assert.Equal("Dòng hai có dấu", moved.Text);
        Assert.Equal(second.ViewBounds.Y + 200, moved.ViewBounds.Y, 0);

        using var reopened = Open(pdf.SaveToBytes());
        Assert.Equal(2, reopened.GetTextObjects(0).Count);
        pdf.Undo();
        pdf.Undo();
        Assert.Equal(3, pdf.GetTextObjects(0).Count);
    }

    [Fact]
    public void MoveTextObject_MovesByViewDelta()
    {
        using var pdf = Open(CreatePdf());
        var obj = Assert.Single(pdf.GetTextObjects(0));
        pdf.MoveTextObject(0, obj.ObjectIndex, 50, 30);
        var moved = Assert.Single(pdf.GetTextObjects(0));
        Assert.Equal(obj.ViewBounds.X + 50, moved.ViewBounds.X, 1);
        Assert.Equal(obj.ViewBounds.Y + 30, moved.ViewBounds.Y, 1);
    }

    [Fact]
    public void AddText_OnRotatedPage_IsUprightOnScreen()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.RotatePage(0, 1);
        pdf.AddText(0, 72, 72, "Rotated page text", Style(20));
        var obj = Assert.Single(pdf.GetTextObjects(0));
        // Chữ nằm ngang trên màn hình: khung rộng hơn cao.
        Assert.True(obj.ViewBounds.Width > obj.ViewBounds.Height * 3);
        Assert.InRange(obj.ViewBounds.X, 70, 76);
    }

    [Fact]
    public void RotatePage_PersistsAndSwapsDisplaySize()
    {
        using var pdf = Open(CreatePdf());
        pdf.RotatePage(0, 1);
        var info = pdf.GetPageInfo(0);
        Assert.Equal(1, info.Rotation);
        Assert.Equal(PdfService.A4Height, info.Width, 1);

        pdf.RotatePage(0, 2);
        Assert.Equal(3, pdf.GetPageInfo(0).Rotation);
        pdf.RotatePage(0, 3);
        Assert.Equal(2, pdf.GetPageInfo(0).Rotation);

        using var reopened = Open(pdf.SaveToBytes());
        Assert.Equal(180, reopened.GetPageInfo(0).RotationDegrees);
    }

    [Fact]
    public void DeletePage_RemovesPage_LastPageCannotBeDeleted()
    {
        using var pdf = Open(CreatePdf(3));
        pdf.DeletePage(1);
        Assert.Equal(2, pdf.PageCount);
        Assert.Contains("Page 3", pdf.GetPageText(1), StringComparison.Ordinal);

        pdf.DeletePage(0);
        Assert.Equal(PdfErrorKind.LastPage, Assert.Throws<PdfException>(() => pdf.DeletePage(0)).Kind);
        Assert.Equal(1, pdf.PageCount);
    }

    [Fact]
    public void InsertBlankPage_InsertsAtIndexWithSize()
    {
        using var pdf = Open(CreatePdf(2));
        pdf.InsertBlankPage(1, 300, 400);
        Assert.Equal(3, pdf.PageCount);
        var info = pdf.GetPageInfo(1);
        Assert.Equal(300, info.Width, 1);
        Assert.Equal(400, info.Height, 1);
        Assert.Equal(string.Empty, pdf.GetPageText(1).Trim());
        Assert.Contains("Page 2", pdf.GetPageText(2), StringComparison.Ordinal);
    }

    [Fact]
    public void MovePage_ReordersPages()
    {
        using var pdf = Open(CreatePdf(3));
        pdf.MovePage(2, 0);
        Assert.Contains("Page 3", pdf.GetPageText(0), StringComparison.Ordinal);
        Assert.Contains("Page 1", pdf.GetPageText(1), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportPages_InsertsAllPagesOfOtherPdf()
    {
        using var pdf = Open(CreatePdf(2));
        var count = pdf.ImportPages(CreatePdf(2, "Imported"), null, 1);
        Assert.Equal(2, count);
        Assert.Equal(4, pdf.PageCount);
        Assert.Contains("Imported", pdf.GetPageText(1), StringComparison.Ordinal);
        Assert.Contains("Page 2", pdf.GetPageText(3), StringComparison.Ordinal);
    }

    [Fact]
    public void UndoRedo_RestoresStatesAndModifiedFlag()
    {
        using var pdf = Open(CreatePdf(2));
        Assert.False(pdf.IsModified);
        Assert.False(pdf.CanUndo);

        pdf.DeletePage(0);
        Assert.True(pdf.IsModified);
        Assert.Equal(1, pdf.PageCount);

        Assert.True(pdf.Undo());
        Assert.Equal(2, pdf.PageCount);
        Assert.False(pdf.IsModified);
        Assert.True(pdf.CanRedo);

        Assert.True(pdf.Redo());
        Assert.Equal(1, pdf.PageCount);
        Assert.True(pdf.IsModified);

        // Thao tác mới xóa nhánh redo.
        Assert.True(pdf.Undo());
        pdf.RotatePage(0, 1);
        Assert.False(pdf.CanRedo);
    }

    [Fact]
    public void Undo_TextEdit_RestoresOriginalText()
    {
        using var pdf = Open(CreatePdf());
        var obj = Assert.Single(pdf.GetTextObjects(0));
        pdf.UpdateTextObject(0, obj.ObjectIndex, "Changed", obj.Style);
        Assert.Equal("Changed", Assert.Single(pdf.GetTextObjects(0)).Text);
        pdf.Undo();
        Assert.Equal("Page 1", Assert.Single(pdf.GetTextObjects(0)).Text);
    }

    [Fact]
    public void SaveToFile_WritesFileAndClearsModified()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "out.pdf");
        using var pdf = Open(CreatePdf());
        pdf.RotatePage(0, 1);
        Assert.True(pdf.IsModified);

        pdf.SaveToFile(path);
        Assert.False(pdf.IsModified);
        Assert.Equal(Path.GetFullPath(path), pdf.FilePath);
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));

        using var reopened = Open(File.ReadAllBytes(path));
        Assert.Equal(1, reopened.GetPageInfo(0).Rotation);
    }

    [Fact]
    public void SaveToFile_OverwritesOpenedFile()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "same.pdf");
        File.WriteAllBytes(path, CreatePdf(2));
        using var pdf = NewService();
        pdf.Open(File.ReadAllBytes(path), null, path);
        pdf.DeletePage(0);
        pdf.SaveToFile(path);

        using var reopened = Open(File.ReadAllBytes(path));
        Assert.Equal(1, reopened.PageCount);
    }

    [Fact]
    public void RenderPage_ReturnsBitmapOfExpectedSize()
    {
        using var pdf = Open(CreatePdf());
        var image = pdf.RenderPage(0, 1.0);
        Assert.InRange(image.Width, 594, 596);
        Assert.InRange(image.Height, 841, 843);
        Assert.Equal(image.Stride * image.Height, image.Pixels.Length);
        // Có pixel tối (chữ) và pixel trắng (nền).
        Assert.Contains(image.Pixels.Where((_, i) => i % 4 == 0), b => b < 100);
        Assert.Equal(255, image.Pixels[0]);
    }

    [Fact]
    public void GetPageInfo_ViewToPageIsInverseOfPageToView()
    {
        using var pdf = Open(CreatePdf());
        pdf.RotatePage(0, 1);
        var info = pdf.GetPageInfo(0);
        var p = info.PageToView.Transform(100, 200);
        var back = info.ViewToPage.Transform(p);
        Assert.Equal(100, back.X, 3);
        Assert.Equal(200, back.Y, 3);
        // Trang nằm trọn trong khung view.
        var corner = info.PageToView.Transform(PdfService.A4Width, PdfService.A4Height);
        Assert.InRange(corner.X, -0.5, info.Width + 0.5);
        Assert.InRange(corner.Y, -0.5, info.Height + 0.5);
    }

    [Fact]
    public void GetDocumentInfo_ReturnsPageCountAndVersion()
    {
        using var pdf = Open(CreatePdf(3));
        var info = pdf.GetDocumentInfo();
        Assert.Equal(3, info.PageCount);
        Assert.Matches(@"^\d\.\d$", info.Version);
    }

    [Fact]
    public void Operations_WithoutDocument_ThrowNotOpen()
    {
        using var pdf = NewService();
        Assert.Equal(PdfErrorKind.NotOpen, Assert.Throws<PdfException>(() => pdf.RotatePage(0, 1)).Kind);
        Assert.Equal(PdfErrorKind.NotOpen, Assert.Throws<PdfException>(() => pdf.SaveToBytes()).Kind);
    }

    [Fact]
    public void RenderedSample_WrittenForVisualCheck_WhenRequested()
    {
        // Đặt biến môi trường PDFEDITOR_RENDER_DIR để xuất ảnh mẫu kiểm tra bằng mắt.
        var dir = Environment.GetEnvironmentVariable("PDFEDITOR_RENDER_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 72, "Hello PDF Editor", Style(28, bold: true));
        pdf.AddText(0, 72, 130, "Tiếng Việt: chỉnh sửa văn bản trong PDF", new TextStyle("Times New Roman", 18, false, true, new RgbColor(37, 99, 235)));
        var obj = pdf.GetTextObjects(0)[0];
        pdf.UpdateTextObject(0, obj.ObjectIndex, "Hello (edited)", obj.Style with { Color = new RgbColor(220, 38, 38) });
        pdf.InsertBlankPage(1, PdfService.A4Width, PdfService.A4Height);
        pdf.RotatePage(1, 1);
        pdf.AddText(1, 72, 72, "Upright text on rotated page", Style(24));
        Directory.CreateDirectory(dir);
        new ExportService(pdf).ExportPng([0, 1], dir, "sample", 72);
        pdf.SaveToFile(Path.Combine(dir, "sample.pdf"));
    }
}
