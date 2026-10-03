using System.Globalization;

namespace PDFEditorApp.Services;

/// <summary>
/// Đọc chuỗi phạm vi trang kiểu "1-3, 5, 8-" (đánh số từ 1) thành danh sách chỉ số trang (từ 0),
/// đã sắp xếp và bỏ trùng. Chuỗi rỗng = tất cả các trang. Xem docs/export.md.
/// </summary>
public static class PageRangeParser
{
    public static bool TryParse(string? text, int pageCount, out IReadOnlyList<int> pages)
    {
        pages = [];
        if (pageCount <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            pages = Enumerable.Range(0, pageCount).ToList();
            return true;
        }

        var result = new SortedSet<int>();
        foreach (var rawPart in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = rawPart.IndexOf('-', StringComparison.Ordinal);
            int from, to;
            if (dash < 0)
            {
                if (!TryPage(rawPart, out from))
                {
                    return false;
                }

                to = from;
            }
            else
            {
                var left = rawPart[..dash].Trim();
                var right = rawPart[(dash + 1)..].Trim();
                from = 1;
                to = pageCount;
                if ((left.Length > 0 && !TryPage(left, out from)) || (right.Length > 0 && !TryPage(right, out to)))
                {
                    return false;
                }
            }

            if (from < 1 || to > pageCount || from > to)
            {
                return false;
            }

            for (var p = from; p <= to; p++)
            {
                result.Add(p - 1);
            }
        }

        if (result.Count == 0)
        {
            return false;
        }

        pages = result.ToList();
        return true;
    }

    private static bool TryPage(string s, out int page) =>
        int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out page);
}
