namespace PDFEditorApp.Models;

/// <summary>
/// Ký tự của một trang và khung của từng ký tự trong hệ view (point, gốc trên-trái).
/// <see cref="Text"/>[i] ứng với <see cref="Boxes"/>[i]; ký tự sinh thêm (xuống dòng…) có khung rỗng.
/// Dùng cho chọn chữ, tìm kiếm, tô sáng (xem docs/text-select.md, search.md).
/// </summary>
public sealed class PageTextLayout(int pageIndex, string text, RectD[] boxes)
{
    public int PageIndex { get; } = pageIndex;

    public string Text { get; } = text;

    public IReadOnlyList<RectD> Boxes { get; } = boxes;

    public int Length => Text.Length;

    private static bool IsEmpty(RectD r) => r.Width <= 0 || r.Height <= 0;

    /// <summary>
    /// Ký tự tại điểm (x, y). <paramref name="nearest"/> = true: nếu không trúng ký tự nào thì lấy ký tự gần
    /// nhất trên cùng dòng (dùng khi kéo chọn). Trả về -1 nếu không có.
    /// </summary>
    public int HitTest(double x, double y, bool nearest)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < Boxes.Count; i++)
        {
            var b = Boxes[i];
            if (IsEmpty(b))
            {
                continue;
            }

            if (b.Contains(x, y))
            {
                return x > b.X + (b.Width / 2) && nearest ? Math.Min(i + 1, Length) : i;
            }

            if (!nearest)
            {
                continue;
            }

            var dy = y < b.Y ? b.Y - y : y > b.Bottom ? y - b.Bottom : 0;
            var dx = x < b.X ? b.X - x : x > b.Right ? x - b.Right : 0;
            var distance = (dy * 4) + dx; // ưu tiên cùng dòng
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = x > b.X + (b.Width / 2) ? i + 1 : i;
            }
        }

        return Math.Min(best, Length);
    }

    /// <summary>Khung tô cho đoạn [start, end): gộp các ký tự liên tiếp trên cùng dòng.</summary>
    public IReadOnlyList<RectD> GetRangeRects(int start, int end)
    {
        var rects = new List<RectD>();
        (start, end) = (Math.Max(0, Math.Min(start, end)), Math.Min(Length, Math.Max(start, end)));
        RectD? current = null;
        for (var i = start; i < end; i++)
        {
            var b = Boxes[i];
            if (IsEmpty(b))
            {
                continue;
            }

            if (current is { } c && SameLine(c, b) && b.X >= c.X - (c.Height * 0.5))
            {
                var x = Math.Min(c.X, b.X);
                var y = Math.Min(c.Y, b.Y);
                current = new RectD(x, y, Math.Max(c.Right, b.Right) - x, Math.Max(c.Bottom, b.Bottom) - y);
            }
            else
            {
                if (current is { } done)
                {
                    rects.Add(done);
                }

                current = b;
            }
        }

        if (current is { } last)
        {
            rects.Add(last);
        }

        return rects;
    }

    public string GetRangeText(int start, int end)
    {
        (start, end) = (Math.Max(0, Math.Min(start, end)), Math.Min(Length, Math.Max(start, end)));
        return start >= end ? string.Empty : Text[start..end];
    }

    /// <summary>Khoảng [start, end) của từ chứa ký tự <paramref name="index"/> (nhấp đúp để chọn từ).</summary>
    public (int Start, int End) WordAt(int index)
    {
        if (index < 0 || index >= Length || !char.IsLetterOrDigit(Text[index]))
        {
            return (index, Math.Min(index + 1, Length));
        }

        var s = index;
        while (s > 0 && char.IsLetterOrDigit(Text[s - 1]))
        {
            s--;
        }

        var e = index;
        while (e < Length && char.IsLetterOrDigit(Text[e]))
        {
            e++;
        }

        return (s, e);
    }

    private static bool SameLine(RectD a, RectD b)
    {
        var overlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        return overlap > Math.Min(a.Height, b.Height) * 0.5;
    }
}

/// <summary>Liên kết trên trang: tới trang khác (<see cref="TargetPage"/> ≥ 0) hoặc địa chỉ web.</summary>
public sealed record LinkInfo(RectD ViewRect, int TargetPage, string? Uri);

/// <summary>Một mục trong mục lục (outline).</summary>
public sealed record BookmarkNode(string Title, int PageIndex, IReadOnlyList<BookmarkNode> Children);

/// <summary>Một kết quả tìm kiếm.</summary>
public sealed record SearchHit(int PageIndex, int Start, int Length, string Context);

/// <summary>Tùy chọn tìm kiếm.</summary>
public sealed record SearchOptions(bool MatchCase = false, bool IgnoreDiacritics = true, bool WholeWord = false);
