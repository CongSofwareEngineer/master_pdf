using System.Globalization;
using System.Text;
using PDFEditorApp.Models;

namespace PDFEditorApp.Services;

/// <summary>
/// Tìm chuỗi trong text của trang (xem docs/search.md). Hỗ trợ không phân biệt hoa thường, bỏ dấu
/// tiếng Việt ("tieng viet" khớp "Tiếng Việt") và khớp cả từ. Chỉ số trả về luôn theo chuỗi GỐC.
/// </summary>
public static class TextSearch
{
    /// <summary>Tất cả vị trí khớp (không chồng nhau) dạng (start, length) trên chuỗi gốc.</summary>
    public static IReadOnlyList<(int Start, int Length)> FindAll(string text, string query, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var results = new List<(int, int)>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var (haystack, map) = Fold(text, options);
        var (needle, _) = Fold(query.Trim(), options);
        if (needle.Length == 0)
        {
            return results;
        }

        var from = 0;
        while (from <= haystack.Length - needle.Length)
        {
            var found = haystack.IndexOf(needle, from, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            var start = map[found];
            var end = map[found + needle.Length - 1] + 1;
            if (!options.WholeWord || IsWholeWord(text, start, end))
            {
                results.Add((start, end - start));
                from = found + needle.Length;
            }
            else
            {
                from = found + 1;
            }
        }

        return results;
    }

    /// <summary>Đoạn văn bản quanh kết quả (để hiện trong danh sách).</summary>
    public static string Context(string text, int start, int length, int radius = 32)
    {
        ArgumentNullException.ThrowIfNull(text);
        var s = Math.Max(0, start - radius);
        var e = Math.Min(text.Length, start + length + radius);
        var snippet = text[s..e].Replace('\r', ' ').Replace('\n', ' ').Trim();
        return (s > 0 ? "…" : string.Empty) + snippet + (e < text.Length ? "…" : string.Empty);
    }

    /// <summary>Bỏ dấu tiếng Việt (và các dấu kết hợp khác), giữ nguyên độ dài nếu mỗi ký tự gốc ra một ký tự.</summary>
    public static string RemoveDiacritics(string text) => Fold(text, new SearchOptions(MatchCase: true, IgnoreDiacritics: true)).Folded;

    /// <summary>Chuẩn hóa chuỗi để so khớp; map[i] = chỉ số ký tự gốc của ký tự thứ i trong chuỗi chuẩn hóa.</summary>
    private static (string Folded, int[] Map) Fold(string text, SearchOptions options)
    {
        var sb = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n' or '\t' or ' ')
            {
                c = ' ';
            }

            string piece;
            if (options.IgnoreDiacritics)
            {
                piece = c switch
                {
                    'đ' => "d",
                    'Đ' => "D",
                    _ => StripMarks(c),
                };
            }
            else
            {
                piece = c.ToString();
            }

            foreach (var p in piece)
            {
                sb.Append(options.MatchCase ? p : char.ToLowerInvariant(p));
                map.Add(i);
            }
        }

        return (sb.ToString(), map.ToArray());
    }

    private static string StripMarks(char c)
    {
        if (c < 0x80)
        {
            return c.ToString();
        }

        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var d in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(d);
            }
        }

        return sb.ToString();
    }

    private static bool IsWholeWord(string text, int start, int end) =>
        (start == 0 || !char.IsLetterOrDigit(text[start - 1])) &&
        (end >= text.Length || !char.IsLetterOrDigit(text[end]));
}
