using System.Runtime.InteropServices;

namespace PDFEditorApp.Services.Pdfium;

// Khai báo P/Invoke tới pdfium.dll (package bblanchon.PDFium.Win32).
// Chữ ký lấy đúng theo header fpdfview.h / fpdf_edit.h / fpdf_text.h / fpdf_save.h / fpdf_ppo.h
// đi kèm package. Tên hàm giữ nguyên như C để dễ tra cứu. Xem docs/pdf-engine.md.

[StructLayout(LayoutKind.Sequential)]
internal struct FsMatrix
{
    public float A;
    public float B;
    public float C;
    public float D;
    public float E;
    public float F;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FsSizeF
{
    public float Width;
    public float Height;
}

/// <summary>FPDF_FILEWRITE: version + con trỏ hàm WriteBlock.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FpdfFileWrite
{
    public int Version;
    public IntPtr WriteBlock;
}

/// <summary>int WriteBlock(FPDF_FILEWRITE* pThis, const void* pData, unsigned long size).</summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int WriteBlockCallback(IntPtr fileWrite, IntPtr data, uint size);

internal static class PdfiumNative
{
    private const string Lib = "pdfium";

    // Render flags (fpdfview.h)
    public const int FPDF_ANNOT = 0x01;
    public const int FPDF_LCD_TEXT = 0x02;

    // Lỗi (FPDF_GetLastError)
    public const uint FPDF_ERR_SUCCESS = 0;
    public const uint FPDF_ERR_UNKNOWN = 1;
    public const uint FPDF_ERR_FILE = 2;
    public const uint FPDF_ERR_FORMAT = 3;
    public const uint FPDF_ERR_PASSWORD = 4;
    public const uint FPDF_ERR_SECURITY = 5;
    public const uint FPDF_ERR_PAGE = 6;

    // Loại page object (fpdf_edit.h)
    public const int FPDF_PAGEOBJ_TEXT = 1;
    public const int FPDF_PAGEOBJ_PATH = 2;

    // Kiểu tô cho path
    public const int FPDF_FILLMODE_WINDING = 2;

    // Loại font cho FPDFText_LoadFont
    public const int FPDF_FONT_TRUETYPE = 2;

    // Text render mode
    public const int FPDF_TEXTRENDERMODE_FILL = 0;

    // Cờ lưu (fpdf_save.h)
    public const uint FPDF_NO_INCREMENTAL = 1 << 1;

    // ---------- fpdfview.h ----------
    [DllImport(Lib)]
    public static extern void FPDF_InitLibrary();

    [DllImport(Lib)]
    public static extern IntPtr FPDF_LoadMemDocument64(IntPtr dataBuf, nuint size, byte[]? password);

    [DllImport(Lib)]
    public static extern uint FPDF_GetLastError();

    [DllImport(Lib)]
    public static extern int FPDF_GetFileVersion(IntPtr doc, out int fileVersion);

    /// <summary>-1 nếu tài liệu không mã hóa.</summary>
    [DllImport(Lib)]
    public static extern int FPDF_GetSecurityHandlerRevision(IntPtr document);

    [DllImport(Lib)]
    public static extern void FPDF_CloseDocument(IntPtr document);

    [DllImport(Lib)]
    public static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(Lib)]
    public static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [DllImport(Lib)]
    public static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(Lib)]
    public static extern float FPDF_GetPageWidthF(IntPtr page);

    [DllImport(Lib)]
    public static extern float FPDF_GetPageHeightF(IntPtr page);

    [DllImport(Lib)]
    public static extern int FPDF_GetPageSizeByIndexF(IntPtr document, int pageIndex, out FsSizeF size);

    [DllImport(Lib)]
    public static extern int FPDF_PageToDevice(
        IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate,
        double pageX, double pageY, out int deviceX, out int deviceY);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);

    [DllImport(Lib)]
    public static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [DllImport(Lib)]
    public static extern void FPDF_RenderPageBitmap(
        IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

    [DllImport(Lib)]
    public static extern int FPDFBitmap_GetStride(IntPtr bitmap);

    [DllImport(Lib)]
    public static extern void FPDFBitmap_Destroy(IntPtr bitmap);

    // ---------- fpdf_edit.h ----------
    [DllImport(Lib)]
    public static extern IntPtr FPDF_CreateNewDocument();

    [DllImport(Lib)]
    public static extern IntPtr FPDFPage_New(IntPtr document, int pageIndex, double width, double height);

    [DllImport(Lib)]
    public static extern void FPDFPage_Delete(IntPtr document, int pageIndex);

    [DllImport(Lib)]
    public static extern int FPDF_MovePages(IntPtr document, int[] pageIndices, uint pageIndicesLen, int destPageIndex);

    [DllImport(Lib)]
    public static extern int FPDFPage_GetRotation(IntPtr page);

    [DllImport(Lib)]
    public static extern void FPDFPage_SetRotation(IntPtr page, int rotate);

    [DllImport(Lib)]
    public static extern int FPDFPage_InsertObject(IntPtr page, IntPtr pageObject);

    [DllImport(Lib)]
    public static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr pageObject);

    [DllImport(Lib)]
    public static extern int FPDFPage_CountObjects(IntPtr page);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);

    [DllImport(Lib)]
    public static extern int FPDFPage_GenerateContent(IntPtr page);

    [DllImport(Lib)]
    public static extern void FPDFPageObj_Destroy(IntPtr pageObject);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_GetType(IntPtr pageObject);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_GetBounds(
        IntPtr pageObject, out float left, out float bottom, out float right, out float top);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_GetMatrix(IntPtr pageObject, out FsMatrix matrix);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_SetMatrix(IntPtr pageObject, ref FsMatrix matrix);

    [DllImport(Lib)]
    public static extern void FPDFPageObj_Transform(
        IntPtr pageObject, double a, double b, double c, double d, double e, double f);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_SetFillColor(IntPtr pageObject, uint r, uint g, uint b, uint a);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_GetFillColor(IntPtr pageObject, out uint r, out uint g, out uint b, out uint a);

    [DllImport(Lib, CharSet = CharSet.Unicode)]
    public static extern int FPDFText_SetText(IntPtr textObject, [MarshalAs(UnmanagedType.LPWStr)] string text);

    [DllImport(Lib)]
    public static extern IntPtr FPDFText_LoadFont(IntPtr document, IntPtr data, uint size, int fontType, int cid);

    [DllImport(Lib)]
    public static extern IntPtr FPDFText_LoadStandardFont(IntPtr document, byte[] font);

    [DllImport(Lib)]
    public static extern void FPDFFont_Close(IntPtr font);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr document, IntPtr font, float fontSize);

    [DllImport(Lib)]
    public static extern int FPDFTextObj_SetTextRenderMode(IntPtr text, int renderMode);

    [DllImport(Lib)]
    public static extern int FPDFTextObj_GetFontSize(IntPtr text, out float size);

    [DllImport(Lib)]
    public static extern uint FPDFTextObj_GetText(IntPtr textObject, IntPtr textPage, byte[]? buffer, uint length);

    [DllImport(Lib)]
    public static extern IntPtr FPDFTextObj_GetFont(IntPtr text);

    [DllImport(Lib)]
    public static extern nuint FPDFFont_GetBaseFontName(IntPtr font, byte[]? buffer, nuint length);

    [DllImport(Lib)]
    public static extern nuint FPDFFont_GetFamilyName(IntPtr font, byte[]? buffer, nuint length);

    [DllImport(Lib)]
    public static extern int FPDFFont_GetWeight(IntPtr font);

    [DllImport(Lib)]
    public static extern int FPDFFont_GetItalicAngle(IntPtr font, out int angle);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPageObj_CreateNewRect(float x, float y, float w, float h);

    [DllImport(Lib)]
    public static extern int FPDFPath_SetDrawMode(IntPtr path, int fillMode, int stroke);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPageObj_AddMark(IntPtr pageObject, byte[] name);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_CountMarks(IntPtr pageObject);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPageObj_GetMark(IntPtr pageObject, uint index);

    /// <remarks>Tên trả về dạng UTF-16LE; <paramref name="outBufferLength"/> = số byte cần (kể cả NUL).</remarks>
    [DllImport(Lib)]
    public static extern int FPDFPageObjMark_GetName(IntPtr mark, byte[]? buffer, uint bufferLength, out uint outBufferLength);

    // ---------- fpdf_text.h ----------
    [DllImport(Lib)]
    public static extern IntPtr FPDFText_LoadPage(IntPtr page);

    [DllImport(Lib)]
    public static extern void FPDFText_ClosePage(IntPtr textPage);

    [DllImport(Lib)]
    public static extern int FPDFText_CountChars(IntPtr textPage);

    /// <remarks><paramref name="result"/> nhận UTF-16LE, cần (count + 1) * 2 byte.</remarks>
    [DllImport(Lib)]
    public static extern int FPDFText_GetText(IntPtr textPage, int startIndex, int count, byte[] result);

    // ---------- fpdf_save.h ----------
    [DllImport(Lib)]
    public static extern int FPDF_SaveAsCopy(IntPtr document, ref FpdfFileWrite fileWrite, uint flags);

    // ---------- fpdf_ppo.h ----------
    [DllImport(Lib)]
    public static extern int FPDF_ImportPages(IntPtr destDoc, IntPtr srcDoc, byte[]? pageRange, int index);

    // ---------- fpdf_doc.h ----------
    [DllImport(Lib)]
    public static extern uint FPDF_GetMetaText(IntPtr document, byte[] tag, byte[]? buffer, uint bufferLength);
}
