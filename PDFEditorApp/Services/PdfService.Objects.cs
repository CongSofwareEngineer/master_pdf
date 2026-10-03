using System.Runtime.InteropServices;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;
using static PDFEditorApp.Services.Pdfium.PdfiumNativeExtra;

namespace PDFEditorApp.Services;

// Sửa object cấp trang (ảnh, hình vẽ, khối chữ), chèn ảnh, che thông tin (redact).
// Xem docs/page-objects.md, protect.md.
public sealed partial class PdfService
{
    private const int FPDF_PAGEOBJ_IMAGE = 3;
    private const int FPDF_PAGEOBJ_SHADING = 4;
    private const int FPDF_PAGEOBJ_FORM = 5;

    /// <summary>Mọi object cấp trang chọn được (bỏ lớp che và object đã bị che).</summary>
    public IReadOnlyList<PageObjectInfo> GetPageObjects(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var covers = GetCovers(page);
            var result = new List<PageObjectInfo>();
            var textPage = FPDFText_LoadPage(page);
            try
            {
                var count = FPDFPage_CountObjects(page);
                for (var i = 0; i < count; i++)
                {
                    var obj = FPDFPage_GetObject(page, i);
                    if (obj == IntPtr.Zero || HasMark(obj, CoverMark) ||
                        FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
                    {
                        continue;
                    }

                    var kind = FPDFPageObj_GetType(obj) switch
                    {
                        FPDF_PAGEOBJ_TEXT => PageObjectKind.Text,
                        FPDF_PAGEOBJ_PATH => PageObjectKind.Path,
                        FPDF_PAGEOBJ_IMAGE => PageObjectKind.Image,
                        FPDF_PAGEOBJ_SHADING => PageObjectKind.Shading,
                        FPDF_PAGEOBJ_FORM => PageObjectKind.Form,
                        _ => PageObjectKind.Unknown,
                    };
                    var bounds = new PdfBounds(l, b, r, t);
                    if (IsCovered(i, bounds, covers) ||
                        (kind == PageObjectKind.Text && string.IsNullOrWhiteSpace(GetObjectText(obj, textPage))))
                    {
                        continue;
                    }

                    var view = info.ToView(bounds);
                    if (view.Width < 0.5 && view.Height < 0.5)
                    {
                        continue;
                    }

                    result.Add(new PageObjectInfo(i, kind, view));
                }
            }
            finally
            {
                if (textPage != IntPtr.Zero)
                {
                    FPDFText_ClosePage(textPage);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Biến đổi object (di chuyển / co giãn) theo ma trận trong hệ view. Trả về chỉ số object sau khi sửa
    /// (khác chỉ số cũ nếu phải dùng chế độ phủ).
    /// </summary>
    public int TransformObject(int pageIndex, int objectIndex, Affine viewTransform) =>
        EditObject(pageIndex, objectIndex, textOnly: false, (page, obj, overlay) =>
        {
            var info = BuildPageInfo(page, pageIndex);
            var m = info.PageToView.Then(viewTransform).Then(info.ViewToPage);
            if (!overlay)
            {
                FPDFPageObj_Transform(obj, m.A, m.B, m.C, m.D, m.E, m.F);
                GenerateContent(page);
                return objectIndex;
            }

            if (FPDFPageObj_GetType(obj) == FPDF_PAGEOBJ_TEXT)
            {
                // Chữ: phủ + tạo lại ở vị trí mới (chỉ hỗ trợ di chuyển).
                var delta = info.ViewToPage.TransformVector(viewTransform.E, viewTransform.F);
                var (style, _) = ReadStyle(obj);
                return RewriteText(page, obj, ReadBackText(page, obj), style, delta, overlay: true);
            }

            if (FPDFPageObj_GetType(obj) != FPDF_PAGEOBJ_IMAGE)
            {
                throw new PdfException(PdfErrorKind.Regeneration);
            }

            // Ảnh: phủ ảnh cũ, chèn bản sao (bitmap đã render) với ma trận mới.
            if (FPDFPageObj_GetMatrix(obj, out var old) == 0 ||
                FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            var bitmap = FPDFImageObj_GetRenderedBitmap(_doc, page, obj);
            if (bitmap == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Regeneration);
            }

            try
            {
                AddCover(page, new PdfBounds(l, b, r, t));
                var oldMatrix = new Affine(old.A, old.B, old.C, old.D, old.E, old.F);
                var copy = CreateImageObject(page, bitmap, oldMatrix.Then(m));
                InsertOwnObject(page, copy);
            }
            finally
            {
                FPDFBitmap_Destroy(bitmap);
            }

            GenerateContent(page);
            return FPDFPage_CountObjects(page) - 1;
        });

    /// <summary>Xóa object (ảnh / hình / chữ). Ở chế độ phủ: che bằng màu nền.</summary>
    public void DeleteObject(int pageIndex, int objectIndex) =>
        EditObject(pageIndex, objectIndex, textOnly: false, (page, obj, overlay) =>
        {
            if (overlay)
            {
                if (FPDFPageObj_GetBounds(obj, out var l, out var b, out var r, out var t) == 0)
                {
                    throw new PdfException(PdfErrorKind.InvalidObject);
                }

                AddCover(page, new PdfBounds(l, b, r, t));
            }
            else
            {
                if (FPDFPage_RemoveObject(page, obj) == 0)
                {
                    throw new PdfException(PdfErrorKind.InvalidObject);
                }

                FPDFPageObj_Destroy(obj);
            }

            GenerateContent(page);
            return -1;
        });

    /// <summary>Chèn ảnh vào khung hệ view. JPEG giữ nguyên dữ liệu nén. Trả về chỉ số object.</summary>
    public int InsertImage(int pageIndex, RectD viewRect, ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Mutate(() =>
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var obj = CreateImageFromData(page, image, ImageMatrix(info, viewRect));
            InsertOwnObject(page, obj);
            GenerateContent(page);
            return FPDFPage_CountObjects(page) - 1;
        });
    }

    /// <summary>
    /// Che thông tin: render trang (không kèm nhận xét), tô đen các vùng, thay toàn bộ nội dung trang bằng
    /// ảnh đó → chữ / ảnh gốc bị xóa thật. Nhận xét giao với vùng che cũng bị xóa.
    /// </summary>
    public void ApplyRedactions(int pageIndex, IReadOnlyList<RectD> viewRects, int dpi = 150)
    {
        ArgumentNullException.ThrowIfNull(viewRects);
        if (viewRects.Count == 0)
        {
            return;
        }

        Mutate(() =>
        {
            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var image = RenderCore(page, dpi / 72.0, forScreen: false, annotations: false);
            var sx = image.Width / info.Width;
            var sy = image.Height / info.Height;
            foreach (var v in viewRects)
            {
                var x0 = Math.Clamp((int)Math.Floor(v.X * sx), 0, image.Width);
                var x1 = Math.Clamp((int)Math.Ceiling(v.Right * sx), 0, image.Width);
                var y0 = Math.Clamp((int)Math.Floor(v.Y * sy), 0, image.Height);
                var y1 = Math.Clamp((int)Math.Ceiling(v.Bottom * sy), 0, image.Height);
                for (var y = y0; y < y1; y++)
                {
                    var row = y * image.Stride;
                    for (var x = x0; x < x1; x++)
                    {
                        var i = row + (x * 4);
                        image.Pixels[i] = 0;
                        image.Pixels[i + 1] = 0;
                        image.Pixels[i + 2] = 0;
                    }
                }
            }

            for (var i = FPDFPage_CountObjects(page) - 1; i >= 0; i--)
            {
                var obj = FPDFPage_GetObject(page, i);
                if (obj != IntPtr.Zero && FPDFPage_RemoveObject(page, obj) != 0)
                {
                    FPDFPageObj_Destroy(obj);
                }
            }

            for (var i = FPDFPage_GetAnnotCount(page) - 1; i >= 0; i--)
            {
                var annot = FPDFPage_GetAnnot(page, i);
                if (annot == IntPtr.Zero)
                {
                    continue;
                }

                var hit = false;
                if (FPDFAnnot_GetRect(annot, out var r) != 0)
                {
                    var view = info.ToView(new PdfBounds(r.Left, Math.Min(r.Top, r.Bottom), r.Right, Math.Max(r.Top, r.Bottom)));
                    hit = viewRects.Any(v => v.X < view.Right && v.Right > view.X && v.Y < view.Bottom && v.Bottom > view.Y);
                }

                FPDFPage_CloseAnnot(annot);
                if (hit)
                {
                    _ = FPDFPage_RemoveAnnot(page, i);
                }
            }

            var obj2 = CreateImageFromData(page, new ImageData(image.Width, image.Height, dpi, dpi, image, null),
                ImageMatrix(info, new RectD(0, 0, info.Width, info.Height)));
            InsertOwnObject(page, obj2);
            GenerateContent(page);
            return 0;
        });
    }

    /// <summary>Ma trận đặt ảnh (hình vuông đơn vị) vào khung hệ view, ảnh luôn đứng thẳng trên màn hình.</summary>
    private static Affine ImageMatrix(PdfPageInfo info, RectD v)
    {
        // Ảnh: (0,0) góc dưới-trái, (1,1) góc trên-phải. view = (X + u·W, Bottom − w·H).
        var toView = new Affine(v.Width, 0, 0, -v.Height, v.X, v.Bottom);
        return toView.Then(info.ViewToPage);
    }

    private IntPtr CreateImageFromData(IntPtr page, ImageData image, Affine matrix)
    {
        if (image.Jpeg is { Length: > 0 } jpeg)
        {
            var obj = FPDFPageObj_NewImageObj(_doc);
            if (obj == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            if (LoadJpeg(page, obj, jpeg))
            {
                SetObjectMatrix(obj, matrix);
                return obj;
            }

            FPDFPageObj_Destroy(obj);
        }

        var bitmapData = image.Bitmap ?? throw new PdfException(PdfErrorKind.InvalidObject);
        var handle = GCHandle.Alloc(bitmapData.Pixels, GCHandleType.Pinned);
        try
        {
            var bitmap = FPDFBitmap_CreateEx(bitmapData.Width, bitmapData.Height, FPDFBitmap_BGRA,
                handle.AddrOfPinnedObject(), bitmapData.Stride);
            if (bitmap == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.Render);
            }

            try
            {
                return CreateImageObject(page, bitmap, matrix);
            }
            finally
            {
                FPDFBitmap_Destroy(bitmap);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private IntPtr CreateImageObject(IntPtr page, IntPtr bitmap, Affine matrix)
    {
        var obj = FPDFPageObj_NewImageObj(_doc);
        if (obj == IntPtr.Zero)
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        if (FPDFImageObj_SetBitmap([page], 1, obj, bitmap) == 0)
        {
            FPDFPageObj_Destroy(obj);
            throw new PdfException(PdfErrorKind.Render);
        }

        SetObjectMatrix(obj, matrix);
        return obj;
    }

    private static bool LoadJpeg(IntPtr page, IntPtr obj, byte[] jpeg)
    {
        GetBlockCallback callback = (_, position, buffer, size) =>
        {
            if (position + (long)size > jpeg.Length)
            {
                return 0;
            }

            Marshal.Copy(jpeg, (int)position, buffer, (int)size);
            return 1;
        };
        var access = new FpdfFileAccess
        {
            FileLength = (uint)jpeg.Length,
            GetBlock = Marshal.GetFunctionPointerForDelegate(callback),
            Param = IntPtr.Zero,
        };
        var ok = FPDFImageObj_LoadJpegFileInline([page], 1, obj, ref access) != 0;
        GC.KeepAlive(callback);
        return ok;
    }

    private static void SetObjectMatrix(IntPtr obj, Affine m)
    {
        var matrix = new FsMatrix
        {
            A = (float)m.A,
            B = (float)m.B,
            C = (float)m.C,
            D = (float)m.D,
            E = (float)m.E,
            F = (float)m.F,
        };
        if (FPDFPageObj_SetMatrix(obj, ref matrix) == 0)
        {
            FPDFPageObj_Destroy(obj);
            throw new PdfException(PdfErrorKind.InvalidObject);
        }
    }

    private static void InsertOwnObject(IntPtr page, IntPtr obj)
    {
        _ = FPDFPageObj_AddMark(obj, ToCString(OwnObjectMark)!);
        if (FPDFPage_InsertObject(page, obj) == 0)
        {
            FPDFPageObj_Destroy(obj);
            throw new PdfException(PdfErrorKind.InvalidObject);
        }
    }

    private static IntPtr GetObjectHandle(IntPtr page, int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= FPDFPage_CountObjects(page))
        {
            throw new PdfException(PdfErrorKind.InvalidObject);
        }

        var obj = FPDFPage_GetObject(page, objectIndex);
        return obj == IntPtr.Zero ? throw new PdfException(PdfErrorKind.InvalidObject) : obj;
    }
}
