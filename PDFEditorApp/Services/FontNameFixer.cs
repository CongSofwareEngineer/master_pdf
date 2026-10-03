using System.Diagnostics;
using System.Globalization;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFEditorApp.Services;

/// <summary>
/// Sửa tận gốc lỗi PDFium gộp nhầm font khi tái tạo nội dung trang (xem docs/text-edit.md):
/// PDFium coi các font cùng BaseFont + loại là một. PDF do Chrome / Skia tạo thường có nhiều subset
/// cùng tên → mất chữ. Lớp này đặt lại tên các font NHÚNG bị trùng bằng tiền tố subset duy nhất
/// ("PEAAAB+Tên") — hợp lệ theo chuẩn PDF và không ảnh hưởng hiển thị vì font đã nhúng.
/// </summary>
public static class FontNameFixer
{
    /// <summary>Trả về bytes PDF mới nếu đã đổi tên ít nhất một font; null nếu không cần / không làm được.</summary>
    public static byte[]? MakeFontNamesUnique(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        try
        {
            using var input = new MemoryStream(pdf);
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var groups = new Dictionary<string, List<PdfDictionary>>(StringComparer.Ordinal);
            foreach (var obj in document.Internals.GetAllObjects())
            {
                if (obj is not PdfDictionary dict || dict.Elements.GetName("/Type") != "/Font")
                {
                    continue;
                }

                var subtype = dict.Elements.GetName("/Subtype");
                var baseFont = dict.Elements.GetName("/BaseFont");
                if (string.IsNullOrEmpty(baseFont) || subtype is "/CIDFontType0" or "/CIDFontType2" or "/Type3")
                {
                    continue;
                }

                // PDFium chỉ gộp khi BaseFont trùng NGUYÊN VĂN (kể cả tiền tố subset).
                var key = subtype + baseFont;
                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = [];
                }

                list.Add(dict);
            }

            var counter = 0;
            foreach (var list in groups.Values.Where(l => l.Count > 1))
            {
                // Font đầu tiên giữ tên; các font sau (nếu đã nhúng) nhận tiền tố mới.
                foreach (var dict in list.Skip(1))
                {
                    if (!IsEmbedded(dict))
                    {
                        continue;
                    }

                    var newName = "/" + Tag(++counter) + "+" + StripSubsetTag(dict.Elements.GetName("/BaseFont")).TrimStart('/');
                    dict.Elements.SetName("/BaseFont", newName);
                    if (dict.Elements.GetArray("/DescendantFonts") is { } descendants)
                    {
                        for (var i = 0; i < descendants.Elements.Count; i++)
                        {
                            descendants.Elements.GetDictionary(i)?.Elements.SetName("/BaseFont", newName);
                        }
                    }
                }
            }

            if (counter == 0)
            {
                return null;
            }

            using var output = new MemoryStream();
            document.Save(output, false);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // PDFsharp không đọc được file (hiếm) → để PdfService dùng chế độ phủ.
            Debug.WriteLine(ex);
            return null;
        }
    }

    private static bool IsEmbedded(PdfDictionary font)
    {
        var descriptor = font.Elements.GetDictionary("/FontDescriptor");
        if (descriptor is null && font.Elements.GetArray("/DescendantFonts") is { Elements.Count: > 0 } descendants)
        {
            descriptor = descendants.Elements.GetDictionary(0)?.Elements.GetDictionary("/FontDescriptor");
        }

        return descriptor is not null &&
               (descriptor.Elements.ContainsKey("/FontFile") ||
                descriptor.Elements.ContainsKey("/FontFile2") ||
                descriptor.Elements.ContainsKey("/FontFile3"));
    }

    /// <summary>"/ABCDEF+Arial" → "/Arial".</summary>
    private static string StripSubsetTag(string name)
    {
        var n = name.TrimStart('/');
        return "/" + (n.Length > 7 && n[6] == '+' && n.Take(6).All(char.IsUpper) ? n[7..] : n);
    }

    /// <summary>Tiền tố 6 chữ in hoa duy nhất: PEAAAA, PEAAAB…</summary>
    private static string Tag(int n)
    {
        Span<char> letters = stackalloc char[4];
        for (var i = 3; i >= 0; i--)
        {
            letters[i] = (char)('A' + (n % 26));
            n /= 26;
        }

        return string.Create(CultureInfo.InvariantCulture, $"PE{letters}");
    }
}
