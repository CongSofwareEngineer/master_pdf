using System.Runtime.InteropServices;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;
using static PDFEditorApp.Services.Pdfium.PdfiumNativeExtra;

namespace PDFEditorApp.Services;

// Thao tác nhiều trang (sắp xếp, trích xuất, nhân bản) và tạo PDF (từ ảnh, kết hợp file).
// Xem docs/page-manage.md, create-pdf.md.
public sealed partial class PdfService
{
    /// <summary>Kích thước trang tối đa theo chuẩn PDF (200 inch).</summary>
    private const double MaxPageSize = 14400;

    public void RotatePages(IReadOnlyList<int> pages, int quarterTurns)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Mutate(() =>
        {
            InvalidateActivePage();
            foreach (var index in pages.Distinct())
            {
                var page = LoadPage(index);
                try
                {
                    FPDFPage_SetRotation(page, (((FPDFPage_GetRotation(page) + quarterTurns) % 4) + 4) % 4);
                }
                finally
                {
                    FPDF_ClosePage(page);
                }
            }

            return 0;
        });
    }

    public void DeletePages(IReadOnlyList<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Mutate(() =>
        {
            var distinct = pages.Distinct().OrderDescending().ToList();
            distinct.ForEach(CheckPageIndex);
            if (distinct.Count >= FPDF_GetPageCount(_doc))
            {
                throw new PdfException(PdfErrorKind.LastPage);
            }

            InvalidateActivePage();
            foreach (var index in distinct)
            {
                FPDFPage_Delete(_doc, index);
            }

            return 0;
        });
    }

    /// <summary>Chuyển các trang (giữ thứ tự) tới vị trí <paramref name="destIndex"/> trong tài liệu KẾT QUẢ.</summary>
    public void MovePages(IReadOnlyList<int> pages, int destIndex)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Mutate(() =>
        {
            var sorted = pages.Distinct().Order().ToArray();
            Array.ForEach(sorted, CheckPageIndex);
            var dest = Math.Clamp(destIndex, 0, FPDF_GetPageCount(_doc) - sorted.Length);
            InvalidateActivePage();
            if (sorted.Length > 0 && FPDF_MovePages(_doc, sorted, (uint)sorted.Length, dest) == 0)
            {
                throw new PdfException(PdfErrorKind.Page);
            }

            return 0;
        });
    }

    /// <summary>Nhân bản các trang, chèn bản sao ngay sau trang cuối cùng được chọn.</summary>
    public void DuplicatePages(IReadOnlyList<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Mutate(() =>
        {
            var sorted = pages.Distinct().Order().ToArray();
            Array.ForEach(sorted, CheckPageIndex);
            var (src, buffer) = LoadDocument(SaveToBytesCore(), _password);
            try
            {
                InvalidateActivePage();
                if (FPDF_ImportPagesByIndex(_doc, src, sorted, (uint)sorted.Length, sorted[^1] + 1) == 0)
                {
                    throw new PdfException(PdfErrorKind.Page);
                }
            }
            finally
            {
                FPDF_CloseDocument(src);
                Marshal.FreeHGlobal(buffer);
            }

            return 0;
        });
    }

    /// <summary>Tạo PDF mới chỉ gồm các trang đã chọn (bytes, không mã hóa). Không đổi tài liệu hiện tại.</summary>
    public byte[] ExtractPages(IReadOnlyList<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        lock (PdfiumLibrary.Sync)
        {
            var sorted = pages.Distinct().Order().ToArray();
            Array.ForEach(sorted, CheckPageIndex);
            var target = FPDF_CreateNewDocument();
            if (target == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Unknown);
            }

            try
            {
                if (FPDF_ImportPagesByIndex(target, _doc, sorted, (uint)sorted.Length, 0) == 0)
                {
                    throw new PdfException(PdfErrorKind.Page);
                }

                using var ms = new MemoryStream();
                SaveDocument(target, ms, FPDF_NO_INCREMENTAL);
                return ms.ToArray();
            }
            finally
            {
                FPDF_CloseDocument(target);
            }
        }
    }

    /// <summary>Tạo tài liệu mới từ ảnh: mỗi ảnh một trang, kích thước theo DPI của ảnh.</summary>
    public void CreateFromImages(IReadOnlyList<ImageData> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            throw new ArgumentException("No image.", nameof(images));
        }

        lock (PdfiumLibrary.Sync)
        {
            var doc = FPDF_CreateNewDocument();
            if (doc == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Unknown);
            }

            CloseCore();
            _doc = doc;
            _filePath = null;
            try
            {
                for (var i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    var w = image.PixelWidth * 72.0 / (image.DpiX > 1 ? image.DpiX : 96);
                    var h = image.PixelHeight * 72.0 / (image.DpiY > 1 ? image.DpiY : 96);
                    var scale = Math.Min(1, MaxPageSize / Math.Max(w, h));
                    w = Math.Max(1, w * scale);
                    h = Math.Max(1, h * scale);
                    var page = FPDFPage_New(_doc, i, w, h);
                    if (page == IntPtr.Zero)
                    {
                        throw new PdfException(PdfErrorKind.Page);
                    }

                    try
                    {
                        var info = BuildPageInfo(page, i);
                        var obj = CreateImageFromData(page, image, ImageMatrix(info, new RectD(0, 0, w, h)));
                        InsertOwnObject(page, obj);
                        GenerateContent(page);
                    }
                    finally
                    {
                        FPDF_ClosePage(page);
                    }
                }
            }
            catch
            {
                CloseCore();
                throw;
            }

            _stateId = ++_nextStateId;
            _savedStateId = -1; // tài liệu mới tạo: chưa lưu
        }
    }

    /// <summary>Kết hợp nhiều PDF thành tài liệu mới (thay tài liệu hiện tại).</summary>
    public void CreateCombined(IReadOnlyList<(byte[] Data, string? Password)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("No file.", nameof(files));
        }

        lock (PdfiumLibrary.Sync)
        {
            var doc = FPDF_CreateNewDocument();
            if (doc == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Unknown);
            }

            try
            {
                foreach (var (data, password) in files)
                {
                    var (src, buffer) = LoadDocument(data, ToCString(password));
                    try
                    {
                        if (FPDF_ImportPages(doc, src, null, FPDF_GetPageCount(doc)) == 0)
                        {
                            throw new PdfException(PdfErrorKind.Page);
                        }
                    }
                    finally
                    {
                        FPDF_CloseDocument(src);
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch
            {
                FPDF_CloseDocument(doc);
                throw;
            }

            CloseCore();
            _doc = doc;
            _filePath = null;
            InitForms();
            _stateId = ++_nextStateId;
            _savedStateId = -1;
        }
    }

    /// <summary>Đặt chế độ bảo mật dùng khi lưu (xem docs/protect.md). Tính là một thay đổi chưa lưu.</summary>
    public void SetSecurity(SecurityMode mode, SecurityOptions? options)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureOpen();
            if (mode == SecurityMode.Apply && options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _securityMode = mode;
            _securityOptions = mode == SecurityMode.Apply ? options : null;
            _stateId = ++_nextStateId;
        }
    }

    public SecurityMode SecurityMode
    {
        get { lock (PdfiumLibrary.Sync) { return _securityMode; } }
    }

    /// <summary>File gốc có mã hóa (mật khẩu) hay không.</summary>
    public bool IsEncrypted
    {
        get { lock (PdfiumLibrary.Sync) { return _doc != IntPtr.Zero && FPDF_GetSecurityHandlerRevision(_doc) >= 0; } }
    }
}
