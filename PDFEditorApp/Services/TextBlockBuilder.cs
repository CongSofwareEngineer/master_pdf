using System.Text;
using PDFEditorApp.Models;

namespace PDFEditorApp.Services;

/// <summary>Một text object đã đọc, dùng để gộp khối (hệ view: point, gốc trên-trái, Y xuống).</summary>
/// <param name="Index">Chỉ số object trong trang.</param>
/// <param name="Text">Nội dung.</param>
/// <param name="View">Khung bao.</param>
/// <param name="OriginX">X của điểm bắt đầu chữ (gốc ma trận).</param>
/// <param name="Baseline">Y của baseline.</param>
/// <param name="Horizontal">Chữ nằm ngang, đọc trái → phải trên màn hình.</param>
internal sealed record TextFragment(
    int Index,
    string Text,
    RectD View,
    double OriginX,
    double Baseline,
    bool Horizontal,
    TextStyle Style,
    string FontName)
{
    public double Size => Style.FontSize;
}

/// <summary>
/// Gộp text object thành khối sửa (dòng → đoạn văn). PDF hay tách một dòng thành nhiều object (mỗi từ / cụm
/// một object, dấu cách chỉ là khoảng trống) → nếu sửa từng object thì ô sửa rất nhỏ và mất dấu cách.
/// Chỉ tách khi có khoảng trống rõ ràng (cột / ô bảng), ảnh / đường kẻ chen giữa, hoặc đổi kiểu chữ (đậm / thường /
/// nghiêng / màu / cỡ) để mỗi khối có một định dạng. Xem docs/text-edit.md.
/// Mọi ngưỡng tính theo cỡ chữ (em).
/// </summary>
internal static class TextBlockBuilder
{
    /// <summary>Chênh lệch baseline tối đa để coi là cùng dòng.</summary>
    public const double BaselineTolerance = 0.3;

    /// <summary>Tỉ lệ cỡ chữ tối đa giữa 2 mảnh cùng dòng.</summary>
    public const double MaxSizeRatio = 1.5;

    /// <summary>Khoảng trống ngang lớn hơn mức này = tách khối (cột, ô bảng, tab). Văn bản căn đều có thể cách từ ~1 em.</summary>
    public const double MaxWordGap = 1.5;

    /// <summary>Tỉ lệ cỡ chữ lớn hơn mức này = khác kiểu (tách khối trong cùng dòng).</summary>
    public const double MaxStyleSizeRatio = 1.15;

    /// <summary>Chênh lệch tối đa mỗi kênh màu (0–255) vẫn coi là cùng màu.</summary>
    public const int ColorTolerance = 24;

    /// <summary>Khoảng trống ngang lớn hơn mức này (và chưa có dấu cách) = chèn dấu cách khi nối.</summary>
    public const double SpaceGap = 0.15;

    /// <summary>Khoảng cách baseline giữa 2 dòng liên tiếp của một đoạn văn.</summary>
    public const double MinLineSpacing = 0.9;

    public const double MaxLineSpacing = 1.8;

    /// <summary>Sai lệch khoảng cách dòng cho phép so với các dòng trước của đoạn.</summary>
    public const double LineSpacingTolerance = 0.2;

    /// <summary>Lệch lề (trái / giữa / phải) tối đa giữa 2 dòng của một đoạn.</summary>
    public const double MaxAlignOffset = 3.0;

    /// <summary>Ước lượng bề rộng trung bình một ký tự (để đoán dòng có bị tự xuống dòng không).</summary>
    private const double CharWidth = 0.5;

    public static List<TextBlockInfo> Build(IReadOnlyList<TextFragment> fragments, IReadOnlyList<RectD> obstacles)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(obstacles);
        var lines = new List<Line>();
        foreach (var row in GroupRows(fragments.Where(f => f.Horizontal)))
        {
            lines.AddRange(SplitRow(row, obstacles));
        }

        var blocks = GroupParagraphs(lines, obstacles).Select(ToBlock).ToList();

        // Chữ xoay / dọc: mỗi object một khối.
        blocks.AddRange(fragments.Where(f => !f.Horizontal).Select(f => ToBlock([new Line([f], f.Text)])));
        return blocks
            .OrderBy(b => b.ViewBounds.Y)
            .ThenBy(b => b.ViewBounds.X)
            .ToList();
    }

    /// <summary>Gom mảnh cùng baseline (cỡ chữ gần nhau) thành hàng, mỗi hàng sắp theo X.</summary>
    private static List<List<TextFragment>> GroupRows(IEnumerable<TextFragment> fragments)
    {
        var rows = new List<List<TextFragment>>();
        foreach (var f in fragments.OrderBy(f => f.Baseline).ThenBy(f => f.View.X))
        {
            List<TextFragment>? target = null;
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var head = rows[i][0];
                if (head.Baseline < f.Baseline - (2 * Math.Max(head.Size, f.Size)))
                {
                    break;
                }

                if (Math.Abs(head.Baseline - f.Baseline) <= BaselineTolerance * Math.Min(head.Size, f.Size) &&
                    Ratio(head.Size, f.Size) <= MaxSizeRatio)
                {
                    target = rows[i];
                    break;
                }
            }

            if (target is null)
            {
                rows.Add([f]);
            }
            else
            {
                target.Add(f);
            }
        }

        foreach (var row in rows)
        {
            row.Sort((a, b) => a.View.X.CompareTo(b.View.X));
        }

        return rows;
    }

    /// <summary>
    /// Tách hàng thành các dòng ở chỗ có khoảng trống lớn, vật cản, hoặc đổi kiểu chữ (đậm / nghiêng / màu / cỡ)
    /// tại ranh giới từ; nối chữ + chèn dấu cách.
    /// </summary>
    private static IEnumerable<Line> SplitRow(List<TextFragment> row, IReadOnlyList<RectD> obstacles)
    {
        var current = new List<TextFragment> { row[0] };
        var text = new StringBuilder(row[0].Text);
        var right = row[0].View.Right;
        var prev = row[0];
        var joinedLeft = false;
        for (var i = 1; i < row.Count; i++)
        {
            var f = row[i];
            var size = Math.Min(prev.Size, f.Size);
            var gap = f.View.X - right;
            var wordBoundary = gap > SpaceGap * size || (text.Length > 0 && char.IsWhiteSpace(text[^1])) ||
                               (f.Text.Length > 0 && char.IsWhiteSpace(f.Text[0]));
            // So với đầu đoạn (không phải mảnh trước): mảnh khác kiểu dính giữa từ không làm lệch kiểu cả đoạn.
            var styleBreak = wordBoundary && !SameLook(current[0].Style, f.Style);
            if (styleBreak || gap > MaxWordGap * size || HasObstacleBetween(prev.View, f.View, obstacles))
            {
                yield return new Line(current, styleBreak ? text.ToString().TrimEnd() : text.ToString(), joinedLeft, styleBreak);
                joinedLeft = styleBreak;
                current = [f];
                text.Clear().Append(f.Text);
            }
            else
            {
                if (gap > SpaceGap * size && text.Length > 0 && !char.IsWhiteSpace(text[^1]) &&
                    f.Text.Length > 0 && !char.IsWhiteSpace(f.Text[0]))
                {
                    text.Append(' ');
                }

                current.Add(f);
                text.Append(f.Text);
            }

            right = current.Count == 1 ? f.View.Right : Math.Max(right, f.View.Right);
            prev = f;
        }

        yield return new Line(current, text.ToString(), joinedLeft, false);
    }

    /// <summary>
    /// Hai mảnh trông cùng kiểu (đậm, nghiêng, màu, cỡ). Không so tên font: PDF hay dùng font dự phòng khác tên
    /// cho vài ký tự (vd. dấu tiếng Việt) dù nhìn giống hệt.
    /// </summary>
    private static bool SameLook(TextStyle a, TextStyle b) =>
        a.Bold == b.Bold && a.Italic == b.Italic && Ratio(a.FontSize, b.FontSize) <= MaxStyleSizeRatio &&
        Math.Abs(a.Color.R - b.Color.R) <= ColorTolerance && Math.Abs(a.Color.G - b.Color.G) <= ColorTolerance &&
        Math.Abs(a.Color.B - b.Color.B) <= ColorTolerance;

    /// <summary>Ảnh / hình nằm giữa 2 mảnh cùng hàng (không tính nền bao trùm cả 2 mảnh).</summary>
    private static bool HasObstacleBetween(RectD left, RectD right, IReadOnlyList<RectD> obstacles)
    {
        var top = Math.Min(left.Y, right.Y);
        var bottom = Math.Max(left.Bottom, right.Bottom);
        foreach (var o in obstacles)
        {
            var overlapY = Math.Min(bottom, o.Bottom) - Math.Max(top, o.Y);
            if (overlapY < 0.5 * (bottom - top) || o.Right < left.Right - 0.5 || o.X > right.X + 0.5)
            {
                continue;
            }

            if (o.X > left.X + (left.Width / 2) && o.Right < right.Right - (right.Width / 2))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gom các dòng liền mạch (cùng kiểu chữ, khoảng cách dòng đều, thẳng lề) thành đoạn văn.</summary>
    private static List<List<Line>> GroupParagraphs(List<Line> lines, IReadOnlyList<RectD> obstacles)
    {
        var paragraphs = new List<List<Line>>();
        foreach (var line in lines.OrderBy(l => l.Baseline).ThenBy(l => l.Left))
        {
            List<Line>? best = null;
            var bestDistance = double.MaxValue;
            foreach (var p in paragraphs)
            {
                var last = p[^1];
                var d = line.Baseline - last.Baseline;
                if (d < bestDistance && Continues(p, line, obstacles))
                {
                    best = p;
                    bestDistance = d;
                }
            }

            if (best is null)
            {
                paragraphs.Add([line]);
            }
            else
            {
                best.Add(line);
            }
        }

        return paragraphs;
    }

    private static bool Continues(List<Line> paragraph, Line next, IReadOnlyList<RectD> obstacles)
    {
        var last = paragraph[^1];

        // Dòng bị cắt vì đổi kiểu chữ (vd. "Họ tên:" đậm + tên thường) không nối thành đoạn qua chỗ cắt.
        if (last.JoinedRight || next.JoinedLeft)
        {
            return false;
        }

        var size = last.Size;
        var d = next.Baseline - last.Baseline;
        if (Ratio(size, next.Size) > 1.2 || d < MinLineSpacing * size || d > MaxLineSpacing * size)
        {
            return false;
        }

        if (paragraph.Count >= 2 && Math.Abs(d - (last.Baseline - paragraph[^2].Baseline)) > LineSpacingTolerance * size)
        {
            return false;
        }

        var a = last.Style;
        var b = next.Style;
        if (!string.Equals(a.FontFamily, b.FontFamily, StringComparison.Ordinal) || a.Bold != b.Bold ||
            a.Italic != b.Italic || a.Color != b.Color)
        {
            return false;
        }

        var overlap = Math.Min(last.Bounds.Right, next.Bounds.Right) - Math.Max(last.Left, next.Left);
        if (overlap < 0.5 * Math.Min(last.Bounds.Right - last.Left, next.Bounds.Right - next.Left))
        {
            return false;
        }

        var tolerance = MaxAlignOffset * size;
        var aligned = Math.Abs(last.Left - next.Left) <= tolerance ||
                      Math.Abs(last.Bounds.Right - next.Bounds.Right) <= tolerance ||
                      Math.Abs((last.Left + last.Bounds.Right - next.Left - next.Bounds.Right) / 2) <= tolerance;
        return aligned && !HasObstacleBelow(last.Bounds, next.Bounds, obstacles);
    }

    /// <summary>Đường kẻ / ảnh nằm giữa 2 dòng (không tính nền bao trùm cả 2 dòng).</summary>
    private static bool HasObstacleBelow(RectD upper, RectD lower, IReadOnlyList<RectD> obstacles)
    {
        var left = Math.Max(upper.X, lower.X);
        var right = Math.Min(upper.Right, lower.Right);
        foreach (var o in obstacles)
        {
            if (o.Right <= left || o.X >= right || o.Bottom < upper.Bottom - 0.5 || o.Y > lower.Y + 0.5)
            {
                continue;
            }

            if (o.Y > upper.Y + (upper.Height / 2) && o.Bottom < lower.Bottom - (lower.Height / 2))
            {
                return true;
            }
        }

        return false;
    }

    private static TextBlockInfo ToBlock(List<Line> lines)
    {
        var first = lines[0];
        var bounds = lines.Select(l => l.Bounds).Aggregate(Union);
        var dominant = lines.SelectMany(l => l.Items).GroupBy(f => f.Style)
            .MaxBy(g => g.Sum(f => f.Text.Length))!.First();

        if (lines.Count == 1)
        {
            return new TextBlockInfo(first.Items.Select(f => f.Index).ToList(), first.Text, bounds, dominant.Style,
                dominant.FontName, 1, 0, 0, 0);
        }

        var lineHeight = (lines[^1].Baseline - first.Baseline) / (lines.Count - 1);
        var nextLeft = lines[1].Left;
        var maxRight = lines.Max(l => l.Bounds.Right);

        // Ngắt mềm (tự xuống dòng) khi từ đầu dòng sau không vừa chỗ trống cuối dòng trước.
        var text = new StringBuilder(first.Text);
        var soft = false;
        for (var i = 1; i < lines.Count; i++)
        {
            var prev = lines[i - 1];
            var cur = lines[i];
            var firstWord = cur.Text.TrimStart().Split(' ')[0];
            var isSoft = prev.Bounds.Right + ((firstWord.Length + 1) * CharWidth * cur.Size) > maxRight &&
                         !EndsParagraph(prev.Text) && !StartsListItem(cur.Text);
            if (isSoft)
            {
                soft = true;
                var trimmed = text.ToString().TrimEnd();
                text.Clear().Append(trimmed);
                if (!trimmed.EndsWith('-'))
                {
                    text.Append(' ');
                }

                text.Append(cur.Text.TrimStart());
            }
            else
            {
                text.Append('\n').Append(cur.Text);
            }
        }

        return new TextBlockInfo(
            lines.SelectMany(l => l.Items.Select(f => f.Index)).ToList(),
            text.ToString(),
            bounds,
            dominant.Style,
            dominant.FontName,
            lines.Count,
            lineHeight,
            soft ? maxRight - nextLeft : 0,
            first.Left - nextLeft);
    }

    private static bool EndsParagraph(string text)
    {
        var t = text.TrimEnd();
        return t.Length > 0 && t[^1] is '.' or ':' or '!' or '?' or ';';
    }

    private static bool StartsListItem(string text)
    {
        var t = text.TrimStart();
        if (t.Length == 0)
        {
            return false;
        }

        if (t[0] is '•' or '◦' or '▪' or '-' or '–' or '*' or '+')
        {
            return true;
        }

        var digits = 0;
        while (digits < t.Length && char.IsDigit(t[digits]))
        {
            digits++;
        }

        return digits > 0 && digits < t.Length && t[digits] is '.' or ')';
    }

    private static double Ratio(double a, double b) => Math.Max(a, b) / Math.Max(1e-6, Math.Min(a, b));

    private static RectD Union(RectD a, RectD b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new RectD(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    /// <summary>Một dòng: các mảnh liền nhau trên cùng baseline.</summary>
    private sealed class Line
    {
        public Line(List<TextFragment> items, string text, bool joinedLeft = false, bool joinedRight = false)
        {
            Items = items;
            Text = text;
            JoinedLeft = joinedLeft;
            JoinedRight = joinedRight;
            Bounds = items.Select(f => f.View).Aggregate(Union);
            Baseline = items.Average(f => f.Baseline);
            Size = items.Max(f => f.Size);
            Left = items[0].OriginX;
            Style = items.GroupBy(f => f.Style).MaxBy(g => g.Sum(f => f.Text.Length))!.Key;
        }

        public List<TextFragment> Items { get; }

        public string Text { get; }

        public RectD Bounds { get; }

        public double Baseline { get; }

        public double Size { get; }

        /// <summary>X bắt đầu dòng (gốc chữ của mảnh đầu).</summary>
        public double Left { get; }

        public TextStyle Style { get; }

        /// <summary>Bên trái sát một đoạn khác kiểu chữ cùng dòng (dòng bị tách vì đổi kiểu).</summary>
        public bool JoinedLeft { get; }

        /// <summary>Bên phải sát một đoạn khác kiểu chữ cùng dòng.</summary>
        public bool JoinedRight { get; }
    }
}
