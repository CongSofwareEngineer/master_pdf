using PDFEditorApp.Models;
using PDFEditorApp.Services;
using static PDFEditorApp.Tests.TestHelpers;

namespace PDFEditorApp.Tests;

/// <summary>Gộp text object thành khối sửa (dòng / đoạn văn) — docs/text-edit.md.</summary>
public class TextBlockTests
{
    // ---------- TextBlockBuilder (thuần, không cần PDFium) ----------

    /// <summary>Mảnh chữ ngang cỡ 10: mỗi ký tự rộng 5pt, baseline ở <paramref name="baseline"/>.</summary>
    private static TextFragment Frag(int index, string text, double x, double baseline, double size = 10, bool bold = false) =>
        new(index, text, new RectD(x, baseline - (0.8 * size), text.Length * size * 0.5, size), x, baseline, true,
            Style(size, bold: bold), "Arial");

    private static List<TextBlockInfo> Build(params TextFragment[] fragments) => TextBlockBuilder.Build(fragments, []);

    private static void Near(double expected, double actual, double tolerance = 2) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);

    [Fact]
    public void Builder_WordsOnSameLine_MergedWithSpace()
    {
        // "Hello" kết thúc ở x = 25, "world" bắt đầu ở 28 (khoảng trống 0.3 em) → cần dấu cách.
        var block = Assert.Single(Build(Frag(0, "Hello", 0, 100), Frag(1, "world", 28, 100)));
        Assert.Equal("Hello world", block.Text);
        Assert.Equal([0, 1], block.ObjectIndices);
        Assert.Equal(1, block.LineCount);
    }

    [Fact]
    public void Builder_AdjacentPieces_MergedWithoutSpace()
    {
        // Một từ bị tách đôi ("TRÙNG P" + "ARACETAMOL"), không có khoảng trống.
        var block = Assert.Single(Build(Frag(0, "TRÙNG P", 0, 100), Frag(1, "ARACETAMOL", 35, 100)));
        Assert.Equal("TRÙNG PARACETAMOL", block.Text);
    }

    [Fact]
    public void Builder_ExistingSpace_NotDoubled()
    {
        var block = Assert.Single(Build(Frag(0, "Hello ", 0, 100), Frag(1, "world", 33, 100)));
        Assert.Equal("Hello world", block.Text);
    }

    [Fact]
    public void Builder_LargeGap_SplitsIntoColumns()
    {
        // Khoảng trống 30pt = 3 em → hai cột / ô bảng.
        var blocks = Build(Frag(0, "Name", 0, 100), Frag(1, "Price", 50, 100));
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Builder_ObstacleBetweenWords_Splits()
    {
        // Đường kẻ dọc của bảng nằm giữa 2 từ.
        var blocks = TextBlockBuilder.Build([Frag(0, "A1", 0, 100), Frag(1, "B1", 14, 100)], [new RectD(12, 88, 0.5, 16)]);
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Builder_BackgroundBehindLine_DoesNotSplit()
    {
        // Nền tô màu bao cả dòng không phải vật cản.
        var block = Assert.Single(TextBlockBuilder.Build([Frag(0, "Hello", 0, 100), Frag(1, "world", 28, 100)],
            [new RectD(-5, 85, 100, 20)]));
        Assert.Equal("Hello world", block.Text);
    }

    [Fact]
    public void Builder_WrappedParagraph_JoinedIntoOneBlock()
    {
        // 3 dòng cách nhau 12pt (1.2 em), dòng 1-2 chạm lề phải → ngắt mềm.
        var blocks = Build(
            Frag(0, "Lorem ipsum dolor sit amet", 0, 100),
            Frag(1, "consectetur adipiscing elit", 0, 112),
            Frag(2, "sed do.", 0, 124));
        var block = Assert.Single(blocks);
        Assert.Equal("Lorem ipsum dolor sit amet consectetur adipiscing elit sed do.", block.Text);
        Assert.Equal(3, block.LineCount);
        Assert.Equal(12, block.LineHeight, 3);
        Assert.True(block.WrapWidth > 0);
        Assert.Equal([0, 1, 2], block.ObjectIndices);
    }

    [Fact]
    public void Builder_ShortLines_KeepHardBreaks()
    {
        // Dòng kết thúc bằng dấu chấm / mục danh sách → ngắt cứng.
        var block = Assert.Single(Build(
            Frag(0, "Ghi chú:", 0, 100),
            Frag(1, "- Mục một dài hơn nhiều lắm", 0, 112),
            Frag(2, "- Mục hai", 0, 124)));
        Assert.Equal("Ghi chú:\n- Mục một dài hơn nhiều lắm\n- Mục hai", block.Text);
        Assert.Equal(0, block.WrapWidth);
    }

    [Fact]
    public void Builder_LargeVerticalGap_SeparateParagraphs()
    {
        var blocks = Build(Frag(0, "Đoạn một", 0, 100), Frag(1, "Đoạn hai", 0, 130));
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Builder_DifferentStyle_SeparateBlocks()
    {
        // Tiêu đề đậm phía trên đoạn thường.
        var blocks = Build(Frag(0, "Tiêu đề", 0, 100, bold: true), Frag(1, "Nội dung", 0, 112));
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Builder_DifferentSize_SeparateBlocks()
    {
        var blocks = Build(Frag(0, "Heading", 0, 100, size: 24), Frag(1, "Body text", 0, 125));
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Builder_BoldAndNormalOnSameLine_SeparateBlocks()
    {
        // "Họ tên: " đậm + "Nguyễn Văn A" thường cùng dòng → mỗi kiểu một khối, mỗi khối một định dạng.
        var blocks = Build(Frag(0, "Họ tên: ", 0, 100, bold: true), Frag(1, "Nguyễn Văn A", 42, 100));
        Assert.Equal(2, blocks.Count);
        Assert.Contains(blocks, b => b.Text == "Họ tên:" && b.Style.Bold && b.ObjectIndices.SequenceEqual([0]));
        Assert.Contains(blocks, b => b.Text == "Nguyễn Văn A" && !b.Style.Bold && b.ObjectIndices.SequenceEqual([1]));
    }

    [Fact]
    public void Builder_SameStyleRuns_MergedAroundOtherStyle()
    {
        // thường + thường | đậm | thường + thường → 3 khối, các mảnh cùng kiểu liền nhau vẫn gộp.
        var blocks = Build(
            Frag(0, "Xin", 0, 100), Frag(1, "chào", 18, 100),
            Frag(2, "bạn", 41, 100, bold: true),
            Frag(3, "và", 59, 100), Frag(4, "tôi", 72, 100));
        Assert.Equal(3, blocks.Count);
        Assert.Contains(blocks, b => b.Text == "Xin chào");
        Assert.Contains(blocks, b => b.Text == "bạn" && b.Style.Bold);
        Assert.Contains(blocks, b => b.Text == "và tôi");
    }

    [Fact]
    public void Builder_StyleChangeInsideWord_NotSplit()
    {
        // Mảnh khác kiểu dính liền (không có khoảng trống) là cùng một từ → không tách.
        var block = Assert.Single(Build(Frag(0, "Nguy", 0, 100), Frag(1, "ễn", 20, 100, bold: true), Frag(2, "Văn", 33, 100)));
        Assert.Equal("Nguyễn Văn", block.Text);
    }

    [Fact]
    public void Builder_JustifiedWordGap_StillOneLine()
    {
        // Văn bản căn đều: khoảng cách từ 1.2 em vẫn là cùng dòng.
        var block = Assert.Single(Build(Frag(0, "Một", 0, 100), Frag(1, "dòng", 27, 100), Frag(2, "đều", 59, 100)));
        Assert.Equal("Một dòng đều", block.Text);
    }

    [Fact]
    public void Builder_BoldWordInParagraph_ParagraphNotJoinedAcrossIt()
    {
        // Dòng 2 có chữ đậm ở cuối → dòng 3 không nối vào đoạn qua chỗ đổi kiểu.
        var blocks = Build(
            Frag(0, "Lorem ipsum dolor sit amet", 0, 100),
            Frag(1, "consectetur", 0, 112), Frag(2, "adipiscing", 58, 112, bold: true),
            Frag(3, "sed do eiusmod tempor", 0, 124));
        Assert.Contains(blocks, b => b.ObjectIndices.SequenceEqual([2]) && b.Style.Bold);
        Assert.DoesNotContain(blocks, b => b.Contains(1) && b.Contains(3));
    }

    [Fact]
    public void Builder_TwoColumns_NotMergedVertically()
    {
        var blocks = Build(
            Frag(0, "Cột trái dòng một", 0, 100), Frag(1, "Cột phải dòng một", 200, 100),
            Frag(2, "Cột trái dòng hai", 0, 112), Frag(3, "Cột phải dòng hai", 200, 112));
        Assert.Equal(2, blocks.Count);
        Assert.Contains(blocks, b => b.ObjectIndices.SequenceEqual([0, 2]));
        Assert.Contains(blocks, b => b.ObjectIndices.SequenceEqual([1, 3]));
    }

    [Fact]
    public void Builder_RuleBetweenLines_SeparateBlocks()
    {
        var blocks = TextBlockBuilder.Build([Frag(0, "Dòng trên", 0, 100), Frag(1, "Dòng dưới", 0, 112)],
            [new RectD(0, 103, 80, 0.5)]);
        Assert.Equal(2, blocks.Count);
    }

    // ---------- PdfService (PDFium) ----------

    [Fact]
    public void GetTextBlocks_SeparateWordObjects_OneBlockWithSpace()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Hello", Style());
        var hello = Assert.Single(pdf.GetTextObjects(0));
        pdf.AddText(0, hello.ViewBounds.Right + 4, 100, "world", Style());

        var block = Assert.Single(pdf.GetTextBlocks(0));
        Assert.Equal("Hello world", block.Text);
        Assert.Equal(2, block.ObjectIndices.Count);
    }

    [Fact]
    public void GetTextBlocks_FarApartWords_SeparateBlocks()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Name", Style());
        pdf.AddText(0, 300, 100, "Price", Style());
        Assert.Equal(2, pdf.GetTextBlocks(0).Count);
    }

    [Fact]
    public void UpdateTextBlock_ReplacesAllPiecesOfLine()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Hello", Style());
        var hello = Assert.Single(pdf.GetTextObjects(0));
        pdf.AddText(0, hello.ViewBounds.Right + 4, 100, "world", Style());
        var block = Assert.Single(pdf.GetTextBlocks(0));

        var index = pdf.UpdateTextBlock(0, block, "Xin chào thế giới", block.Style);
        Assert.True(index >= 0);

        using var reopened = Open(pdf.SaveToBytes());
        var obj = Assert.Single(reopened.GetTextObjects(0));
        Assert.Equal("Xin chào thế giới", obj.Text);
        Near(hello.ViewBounds.X, obj.ViewBounds.X);
    }

    [Fact]
    public void UpdateTextBlock_Paragraph_RewrapsWithinOriginalWidth()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "The quick brown fox jumps over\nthe lazy dog and keeps running", Style());
        var block = Assert.Single(pdf.GetTextBlocks(0));
        Assert.Equal(2, block.LineCount);
        Assert.Equal("The quick brown fox jumps over the lazy dog and keeps running", block.Text);
        Assert.True(block.WrapWidth > 0);

        var text = "The quick brown fox jumps over the lazy dog and keeps running far away into the deep green forest";
        pdf.UpdateTextBlock(0, block, text, block.Style);

        using var reopened = Open(pdf.SaveToBytes());
        var lines = reopened.GetTextObjects(0).OrderBy(o => o.ViewBounds.Y).ToList();
        Assert.True(lines.Count >= 3);
        Assert.Equal(text, string.Join(' ', lines.Select(l => l.Text)));
        Assert.All(lines, l => Assert.True(l.ViewBounds.Width <= (block.WrapWidth * 1.02) + 2));
        Assert.All(lines, l => Near(block.ViewBounds.X, l.ViewBounds.X));
        Near(block.LineHeight, lines[2].ViewBounds.Y - lines[1].ViewBounds.Y, 3);
    }

    /// <summary>Kéo rộng ô sửa → đoạn văn chảy lại theo bề rộng mới (không phải bề rộng gốc).</summary>
    [Fact]
    public void UpdateTextBlock_NewWrapWidth_RewrapsToNewWidth()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "The quick brown fox jumps over\nthe lazy dog and keeps running", Style());
        var block = Assert.Single(pdf.GetTextBlocks(0));
        Assert.True(block.WrapWidth > 0);

        var text = "The quick brown fox jumps over the lazy dog and keeps running far away into the deep green forest";
        var wider = block.WrapWidth * 2;
        pdf.UpdateTextBlock(0, block, text, block.Style, wider);

        using var reopened = Open(pdf.SaveToBytes());
        var lines = reopened.GetTextObjects(0).OrderBy(o => o.ViewBounds.Y).ToList();
        Assert.Equal(text, string.Join(' ', lines.Select(l => l.Text)));
        Assert.All(lines, l => Assert.True(l.ViewBounds.Width <= (wider * 1.02) + 2));
        // Có dòng rộng hơn bề rộng gốc → đã dùng bề rộng mới.
        Assert.Contains(lines, l => l.ViewBounds.Width > block.WrapWidth * 1.1);
    }

    /// <summary>Khối một dòng: kéo hẹp ô sửa → chữ bắt đầu tự xuống dòng theo bề rộng đó.</summary>
    [Fact]
    public void UpdateTextBlock_SingleLine_NewWrapWidth_StartsWrapping()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Hello world", Style());
        var block = Assert.Single(pdf.GetTextBlocks(0));
        Assert.Equal(0, block.WrapWidth);

        var text = "The quick brown fox jumps over the lazy dog";
        pdf.UpdateTextBlock(0, block, text, block.Style, block.ViewBounds.Width);

        using var reopened = Open(pdf.SaveToBytes());
        var lines = reopened.GetTextObjects(0).OrderBy(o => o.ViewBounds.Y).ToList();
        Assert.True(lines.Count > 1);
        Assert.Equal(text, string.Join(' ', lines.Select(l => l.Text)));
        Assert.All(lines, l => Assert.True(l.ViewBounds.Width <= (block.ViewBounds.Width * 1.02) + 2));
        Assert.All(lines, l => Near(block.ViewBounds.X, l.ViewBounds.X));
    }

    [Fact]
    public void DeleteAndMoveTextBlock_AffectWholeBlock()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Hello", Style());
        var hello = Assert.Single(pdf.GetTextObjects(0));
        pdf.AddText(0, hello.ViewBounds.Right + 4, 100, "world", Style());
        pdf.AddText(0, 72, 400, "Other", Style());

        var block = pdf.GetTextBlocks(0).Single(b => b.Text == "Hello world");
        var index = pdf.MoveTextBlock(0, block, 10, 50);
        var moved = pdf.GetTextBlocks(0).Single(b => b.Contains(index));
        Assert.Equal("Hello world", moved.Text);
        Near(block.ViewBounds.X + 10, moved.ViewBounds.X, 0.5);
        Near(block.ViewBounds.Y + 50, moved.ViewBounds.Y, 0.5);

        pdf.DeleteTextBlock(0, moved);
        var rest = Assert.Single(pdf.GetTextBlocks(0));
        Assert.Equal("Other", rest.Text);
    }

    [Fact]
    public void UpdateTextBlock_DuplicateIndices_Rejected()
    {
        using var pdf = NewService();
        pdf.CreateNew();
        pdf.AddText(0, 72, 100, "Hello", Style());
        var block = Assert.Single(pdf.GetTextBlocks(0)) with { ObjectIndices = [0, 0] };
        Assert.Equal(PdfErrorKind.InvalidObject, Assert.Throws<PdfException>(() => pdf.UpdateTextBlock(0, block, "x", block.Style)).Kind);
        Assert.Equal("Hello", Assert.Single(pdf.GetTextObjects(0)).Text);
    }
}
