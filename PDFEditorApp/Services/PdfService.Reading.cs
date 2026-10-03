using System.Text;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;
using static PDFEditorApp.Services.Pdfium.PdfiumNativeExtra;

namespace PDFEditorApp.Services;

// Đọc: bố cục ký tự (chọn chữ, tìm kiếm), liên kết, mục lục. Xem docs/text-select.md, search.md, bookmarks.md.
public sealed partial class PdfService
{
    /// <summary>Ký tự + khung (hệ view) của trang.</summary>
    public PageTextLayout GetTextLayout(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page =>
            {
                var info = BuildPageInfo(page, pageIndex);
                var textPage = FPDFText_LoadPage(page);
                if (textPage == IntPtr.Zero)
                {
                    return new PageTextLayout(pageIndex, string.Empty, []);
                }

                try
                {
                    var count = Math.Max(0, FPDFText_CountChars(textPage));
                    var chars = new char[count];
                    var boxes = new RectD[count];
                    for (var i = 0; i < count; i++)
                    {
                        var u = FPDFText_GetUnicode(textPage, i);
                        chars[i] = u switch
                        {
                            0 => ' ',
                            0xA0 => ' ',
                            > 0xFFFF => '�',
                            _ => (char)u,
                        };
                        if (FPDFText_GetLooseCharBox(textPage, i, out var r) != 0 && r.Right > r.Left && Math.Abs(r.Top - r.Bottom) > 0)
                        {
                            boxes[i] = info.ToView(new PdfBounds(r.Left, Math.Min(r.Top, r.Bottom), r.Right, Math.Max(r.Top, r.Bottom)));
                        }
                    }

                    return new PageTextLayout(pageIndex, new string(chars), boxes);
                }
                finally
                {
                    FPDFText_ClosePage(textPage);
                }
            });
        }
    }

    /// <summary>Liên kết trên trang.</summary>
    public IReadOnlyList<LinkInfo> GetLinks(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            return WithPage(pageIndex, page =>
            {
                var info = BuildPageInfo(page, pageIndex);
                var links = new List<LinkInfo>();
                var position = 0;
                while (links.Count < 5000 && FPDFLink_Enumerate(page, ref position, out var link) != 0)
                {
                    if (link == IntPtr.Zero || FPDFLink_GetAnnotRect(link, out var r) == 0)
                    {
                        continue;
                    }

                    var target = DestPage(FPDFLink_GetDest(_doc, link));
                    string? uri = null;
                    if (target < 0)
                    {
                        (target, uri) = ReadAction(FPDFLink_GetAction(link));
                    }

                    if (target >= 0 || !string.IsNullOrEmpty(uri))
                    {
                        var bounds = new PdfBounds(r.Left, Math.Min(r.Top, r.Bottom), r.Right, Math.Max(r.Top, r.Bottom));
                        links.Add(new LinkInfo(info.ToView(bounds), target, uri));
                    }
                }

                return links;
            });
        }
    }

    /// <summary>Cây mục lục của tài liệu (rỗng nếu không có).</summary>
    public IReadOnlyList<BookmarkNode> GetBookmarks()
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            var visited = new HashSet<IntPtr>();
            return ReadBookmarks(IntPtr.Zero, 0, visited);
        }
    }

    private List<BookmarkNode> ReadBookmarks(IntPtr parent, int depth, HashSet<IntPtr> visited)
    {
        var nodes = new List<BookmarkNode>();
        var child = FPDFBookmark_GetFirstChild(_doc, parent);
        while (child != IntPtr.Zero && visited.Count < 20000 && visited.Add(child))
        {
            var title = ReadWide((b, n) => FPDFBookmark_GetTitle(child, b, n));
            var page = DestPage(FPDFBookmark_GetDest(_doc, child));
            if (page < 0)
            {
                page = ReadAction(FPDFBookmark_GetAction(child)).Page;
            }

            var children = depth < 32 ? ReadBookmarks(child, depth + 1, visited) : [];
            nodes.Add(new BookmarkNode(title, page, children));
            child = FPDFBookmark_GetNextSibling(_doc, child);
        }

        return nodes;
    }

    private int DestPage(IntPtr dest) => dest == IntPtr.Zero ? -1 : FPDFDest_GetDestPageIndex(_doc, dest);

    private (int Page, string? Uri) ReadAction(IntPtr action)
    {
        if (action == IntPtr.Zero)
        {
            return (-1, null);
        }

        switch (FPDFAction_GetType(action))
        {
            case PDFACTION_GOTO:
                return (DestPage(FPDFAction_GetDest(_doc, action)), null);
            case PDFACTION_URI:
                var size = FPDFAction_GetURIPath(_doc, action, null, 0);
                if (size <= 1)
                {
                    return (-1, null);
                }

                var buffer = new byte[size];
                _ = FPDFAction_GetURIPath(_doc, action, buffer, size);
                return (-1, Encoding.UTF8.GetString(buffer, 0, (int)size - 1));
            default:
                return (-1, null);
        }
    }
}
