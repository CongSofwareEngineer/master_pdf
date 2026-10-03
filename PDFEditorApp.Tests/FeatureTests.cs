using PDFEditorApp.Models;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.PdfFixtures;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Chọn chữ, tìm kiếm, liên kết, mục lục, form (docs/text-select.md, search.md, bookmarks.md, forms.md).</summary>
public class ReadingTests
{
    [Fact]
    public void TextLayout_HitTestRectsAndWords()
    {
        using var pdf = Open(CreatePdf(1, "Hello world"));
        var layout = pdf.GetTextLayout(0);
        var start = layout.Text.IndexOf("Hello", StringComparison.Ordinal);
        Assert.True(start >= 0);

        var box = layout.Boxes[start];
        Assert.Equal(start, layout.HitTest(box.X + 1, box.Y + (box.Height / 2), nearest: false));
        Assert.Equal((start, start + 5), layout.WordAt(start + 2));
        Assert.Equal("Hello world", layout.GetRangeText(start, start + 11));
        var rects = layout.GetRangeRects(start, start + 11);
        Assert.Single(rects);
        Assert.InRange(rects[0].X, 70, 75);
    }

    [Theory]
    [InlineData("tieng viet", false, true, false, 2, 0)]
    [InlineData("Tiếng", true, false, false, 1, 0)]
    [InlineData("TIẾNG", false, false, false, 2, 0)]
    [InlineData("viet", false, true, true, 2, 6)]
    [InlineData("vie", false, true, true, 0, 0)]
    public void TextSearch_Options(string query, bool matchCase, bool ignoreDiacritics, bool wholeWord, int expected, int firstStart)
    {
        const string text = "Tiếng Việt và tiếng việt.";
        var hits = TextSearch.FindAll(text, query, new SearchOptions(matchCase, ignoreDiacritics, wholeWord));
        Assert.Equal(expected, hits.Count);
        if (hits.Count > 0)
        {
            Assert.Equal(firstStart, hits[0].Start);
        }
    }

    [Fact]
    public void TextSearch_MapsBackToOriginalIndices()
    {
        const string text = "abc Đường đi";
        var hit = Assert.Single(TextSearch.FindAll(text, "duong", new SearchOptions()));
        Assert.Equal("Đường", text.Substring(hit.Start, hit.Length));
        Assert.Contains("Đường", TextSearch.Context(text, hit.Start, hit.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void Bookmarks_And_Links()
    {
        using var pdf = Open(FormAndLinks());
        var bookmark = Assert.Single(pdf.GetBookmarks());
        Assert.Equal("Chapter 2", bookmark.Title);
        Assert.Equal(1, bookmark.PageIndex);

        var links = pdf.GetLinks(0);
        Assert.Contains(links, l => l.TargetPage == 1);
        Assert.Contains(links, l => l.Uri == "https://example.com/");
        var internalLink = links.First(l => l.TargetPage == 1);
        Assert.InRange(internalLink.ViewRect.X, 99, 101);
        Assert.InRange(internalLink.ViewRect.Y, 221, 223); // 842 - 620
    }

    [Fact]
    public void Forms_FillTextAndCheckbox_PersistAfterSave()
    {
        using var pdf = Open(FormAndLinks());
        Assert.True(pdf.HasForms);
        var fields = pdf.GetFormFields(0);
        var text = fields.Single(f => f.Kind == FormFieldKind.Text);
        var check = fields.Single(f => f.Kind == FormFieldKind.CheckBox);
        Assert.Equal("name", text.Name);
        Assert.False(check.IsChecked);

        pdf.SetFormFieldText(0, text.AnnotIndex, "Nguyễn Văn A");
        pdf.ToggleFormField(0, check.AnnotIndex);
        Assert.True(pdf.CanUndo);

        // Ô đánh dấu đã chọn phải hiện trên ảnh render (ô 100..120 × 650..670 → view y = 842-670..842-650).
        var image = pdf.RenderPage(0, 1.0, forScreen: false);
        var (r, g, b) = PixelAt(image, 110, 842 - 660);
        Assert.True(r < 80 && g < 80 && b < 80, $"checkbox pixel {r},{g},{b}");

        using var reopened = Open(pdf.SaveToBytes());
        var after = reopened.GetFormFields(0);
        Assert.Equal("Nguyễn Văn A", after.Single(f => f.Kind == FormFieldKind.Text).Value);
        Assert.True(after.Single(f => f.Kind == FormFieldKind.CheckBox).IsChecked);
    }
}

/// <summary>Nhận xét (docs/annotations.md).</summary>
public class AnnotationTests
{
    [Fact]
    public void Highlight_IsRenderedAndPersisted()
    {
        using var pdf = Open(CreatePdf(1, "Highlight me"));
        var layout = pdf.GetTextLayout(0);
        var start = layout.Text.IndexOf("Highlight", StringComparison.Ordinal);
        var rects = layout.GetRangeRects(start, start + 9);
        pdf.AddTextMarkup(0, AnnotationKind.Highlight, rects, new RgbColor(255, 230, 0));

        var annot = Assert.Single(pdf.GetAnnotations(0));
        Assert.Equal(AnnotationKind.Highlight, annot.Kind);
        Assert.Equal(SystemService.UserName, annot.Author);

        // Vùng tô (ngoài nét chữ) có màu vàng.
        var image = pdf.RenderPage(0, 1.0, forScreen: false);
        var (r, g, b) = PixelAt(image, rects[0].X + 1, rects[0].Y + 1);
        Assert.True(r > 200 && g > 180 && b < 120, $"pixel {r},{g},{b}");

        using var reopened = Open(pdf.SaveToBytes());
        Assert.Equal(AnnotationKind.Highlight, Assert.Single(reopened.GetAnnotations(0)).Kind);
    }

    [Fact]
    public void Ink_Shapes_Note_Line_Delete()
    {
        using var pdf = Open(CreatePdf());
        var red = new RgbColor(220, 0, 0);
        pdf.AddInk(0, [[new PointD(100, 300), new PointD(200, 320), new PointD(250, 400)]], red, 3);
        pdf.AddShape(0, AnnotationKind.Square, new RectD(300, 300, 100, 80), red, null, 2);
        pdf.AddShape(0, AnnotationKind.Circle, new RectD(300, 450, 100, 80), red, new RgbColor(0, 0, 255), 2);
        pdf.AddLine(0, new PointD(100, 600), new PointD(300, 600), arrow: true, red, 2);
        pdf.AddNote(0, new PointD(450, 100), "Ghi chú tiếng Việt", new RgbColor(255, 200, 0));

        var annots = pdf.GetAnnotations(0);
        Assert.Equal(
            [AnnotationKind.Ink, AnnotationKind.Square, AnnotationKind.Circle, AnnotationKind.Ink, AnnotationKind.Note],
            annots.Select(a => a.Kind).ToArray());
        Assert.Equal("Ghi chú tiếng Việt", annots[4].Contents);

        // Ellipse tô xanh ở giữa.
        var image = pdf.RenderPage(0, 1.0, forScreen: false);
        var (_, _, blue) = PixelAt(image, 350, 490);
        Assert.True(blue > 200);

        pdf.SetAnnotationContents(0, 4, "Đã sửa");
        pdf.DeleteAnnotation(0, 0);
        using var reopened = Open(pdf.SaveToBytes());
        var after = reopened.GetAnnotations(0);
        Assert.Equal(4, after.Count);
        Assert.Equal("Đã sửa", after.Single(a => a.Kind == AnnotationKind.Note).Contents);
    }
}

/// <summary>Sửa object, chèn ảnh, che thông tin, font trùng (docs/page-objects.md, protect.md, text-edit.md).</summary>
public class PageObjectTests
{
    [Fact]
    public void InsertImage_MoveResizeDelete()
    {
        using var pdf = Open(CreatePdf());
        var index = pdf.InsertImage(0, new RectD(100, 400, 100, 50), SolidImage(20, 10, 255, 0, 0));
        var obj = pdf.GetPageObjects(0).Single(o => o.Index == index);
        Assert.Equal(PageObjectKind.Image, obj.Kind);
        Assert.Equal(100, obj.ViewRect.X, 0);
        Assert.Equal(400, obj.ViewRect.Y, 0);
        Assert.Equal(100, obj.ViewRect.Width, 0);
        Assert.Equal((255, 0, 0), PixelAt(pdf.RenderPage(0, 1.0, false), 150, 425));

        var moved = pdf.TransformObject(0, index, Affine.Translation(50, 100).Then(Affine.ScaleAt(2, 2, 150, 500)));
        var after = pdf.GetPageObjects(0).Single(o => o.Index == moved);
        Assert.Equal(200, after.ViewRect.Width, 0);
        Assert.Equal(150, after.ViewRect.X, 0); // dời tới (150,500) rồi co giãn quanh chính điểm đó

        pdf.DeleteObject(0, moved);
        Assert.DoesNotContain(pdf.GetPageObjects(0), o => o.Kind == PageObjectKind.Image);
    }

    [Fact]
    public void Redaction_RemovesTextForReal()
    {
        using var pdf = Open(CreatePdf(1, "Secret 123"));
        var text = Assert.Single(pdf.GetTextObjects(0));
        pdf.ApplyRedactions(0, [text.ViewBounds.Inflate(2)]);

        using var reopened = Open(pdf.SaveToBytes());
        Assert.DoesNotContain("Secret", reopened.GetPageText(0), StringComparison.Ordinal);
        var objects = reopened.GetPageObjects(0);
        Assert.Equal(PageObjectKind.Image, Assert.Single(objects).Kind);
        var image = reopened.RenderPage(0, 1.0, false);
        var center = text.ViewBounds;
        Assert.Equal((0, 0, 0), PixelAt(image, center.X + 3, center.Y + (center.Height / 2)));
    }

    [Fact]
    public void FontNameFixer_AllowsTrueDeletionOnAmbiguousPdf()
    {
        // Hai phiên nhúng Arial → hai font cùng BaseFont (giống PDF do Chrome tạo).
        byte[] data;
        using (var first = NewService())
        {
            first.CreateNew();
            first.AddText(0, 72, 72, "Một", Style());
            using var second = Open(first.SaveToBytes());
            second.AddText(0, 72, 120, "Hai dấu", Style());
            data = second.SaveToBytes();
        }

        Assert.NotNull(FontNameFixer.MakeFontNamesUnique(data));

        using var pdf = Open(data);
        var target = pdf.GetTextObjects(0).Single(o => o.Text == "Một");
        pdf.DeleteTextObject(0, target.ObjectIndex);
        using var reopened = Open(pdf.SaveToBytes());
        var pageText = reopened.GetPageText(0);
        Assert.DoesNotContain("Một", pageText, StringComparison.Ordinal); // xóa thật, không chỉ che
        Assert.Contains("Hai dấu", pageText, StringComparison.Ordinal);
    }
}

/// <summary>Trang, tạo PDF, watermark, đầu/chân trang, mật khẩu.</summary>
public class DocumentFeatureTests
{
    [Fact]
    public void BatchPageOperations()
    {
        using var pdf = Open(CreatePdf(4));
        pdf.RotatePages([0, 2], 1);
        Assert.Equal(1, pdf.GetPageInfo(2).Rotation);

        pdf.MovePages([2, 3], 0);
        Assert.Contains("Page 3", pdf.GetPageText(0), StringComparison.Ordinal);
        Assert.Contains("Page 1", pdf.GetPageText(2), StringComparison.Ordinal);

        pdf.DuplicatePages([0]);
        Assert.Equal(5, pdf.PageCount);
        Assert.Contains("Page 3", pdf.GetPageText(1), StringComparison.Ordinal);

        pdf.DeletePages([0, 1]);
        Assert.Equal(3, pdf.PageCount);
        Assert.Equal(PdfErrorKind.LastPage, Assert.Throws<PdfException>(() => pdf.DeletePages([0, 1, 2])).Kind);

        using var extracted = Open(pdf.ExtractPages([1, 2]));
        Assert.Equal(2, extracted.PageCount);
    }

    [Fact]
    public void CreateFromImages_And_Combined()
    {
        using var pdf = NewService();
        pdf.CreateFromImages([SolidImage(144, 72, 0, 128, 0), SolidImage(72, 144, 0, 0, 255)]);
        Assert.Equal(2, pdf.PageCount);
        Assert.Equal(144, pdf.GetPageInfo(0).Width, 0);
        Assert.True(pdf.IsModified);
        Assert.Equal((0, 0, 255), PixelAt(pdf.RenderPage(1, 1.0, false), 30, 30));

        pdf.CreateCombined([(CreatePdf(2), null), (CreatePdf(3, "Other"), null)]);
        Assert.Equal(5, pdf.PageCount);
        Assert.Contains("Other", pdf.GetPageText(4), StringComparison.Ordinal);
    }

    [Fact]
    public void Watermark_And_HeaderFooter_AddRemove()
    {
        using var pdf = Open(CreatePdf(3));
        pdf.AddWatermark(new WatermarkOptions("BẢN NHÁP", "Arial", 60, new RgbColor(200, 0, 0), 0.3, 45, null));
        pdf.AddHeaderFooter(new HeaderFooterOptions("Trang {n}/{N}", StampPosition.BottomCenter, "Arial", 10, RgbColor.Black, 30, null));
        Assert.Contains("BẢN NHÁP", pdf.GetPageText(1), StringComparison.Ordinal);
        Assert.Contains("Trang 2/3", pdf.GetPageText(1), StringComparison.Ordinal);

        var footer = pdf.GetTextObjects(1).Single(o => o.Text == "Trang 2/3");
        Assert.InRange(footer.ViewBounds.Bottom, 800, 815);
        Assert.InRange(footer.ViewBounds.X + (footer.ViewBounds.Width / 2), 290, 306);

        Assert.Equal(3, pdf.RemoveWatermarks());
        Assert.Equal(3, pdf.RemoveHeaderFooters());
        Assert.DoesNotContain("BẢN NHÁP", pdf.GetPageText(1), StringComparison.Ordinal);
        Assert.Contains("Page 2", pdf.GetPageText(1), StringComparison.Ordinal);
    }

    [Fact]
    public void Security_ApplyAndRemovePassword()
    {
        var dir = NewTempDir();
        var protectedPath = Path.Combine(dir, "protected.pdf");
        using (var pdf = Open(CreatePdf()))
        {
            pdf.SetSecurity(SecurityMode.Apply, new SecurityOptions("123", "owner", true, true, false));
            pdf.SaveToFile(protectedPath);
        }

        var bytes = File.ReadAllBytes(protectedPath);
        using (var noPassword = NewService())
        {
            Assert.Equal(PdfErrorKind.Password, Assert.Throws<PdfException>(() => noPassword.Open(bytes, null, null)).Kind);
        }

        var plainPath = Path.Combine(dir, "plain.pdf");
        using (var pdf = NewService())
        {
            pdf.Open(bytes, "123", protectedPath);
            Assert.True(pdf.IsEncrypted);
            Assert.Contains("Page 1", pdf.GetPageText(0), StringComparison.Ordinal);
            pdf.SetSecurity(SecurityMode.Remove, null);
            pdf.SaveToFile(plainPath);
        }

        using var reopened = Open(File.ReadAllBytes(plainPath));
        Assert.False(reopened.IsEncrypted);
    }
}
