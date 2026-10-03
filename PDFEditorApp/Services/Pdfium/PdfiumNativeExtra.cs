using System.Runtime.InteropServices;

namespace PDFEditorApp.Services.Pdfium;

// P/Invoke bổ sung cho các tính năng kiểu Acrobat: nhận xét (fpdf_annot.h), form (fpdf_formfill.h),
// mục lục & liên kết (fpdf_doc.h), ký tự (fpdf_text.h), ảnh (fpdf_edit.h). Chữ ký lấy đúng theo header.

[StructLayout(LayoutKind.Sequential)]
internal struct FsRectF
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FsPointF
{
    public float X;
    public float Y;
}

/// <summary>FS_QUADPOINTSF: x1,y1 (trên-trái) x2,y2 (trên-phải) x3,y3 (dưới-trái) x4,y4 (dưới-phải).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FsQuadPointsF
{
    public float X1;
    public float Y1;
    public float X2;
    public float Y2;
    public float X3;
    public float Y3;
    public float X4;
    public float Y4;
}

/// <summary>FPDF_FILEACCESS: độ dài file + hàm đọc khối + tham số.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FpdfFileAccess
{
    public uint FileLength;
    public IntPtr GetBlock;
    public IntPtr Param;
}

/// <summary>int GetBlock(void* param, unsigned long position, unsigned char* pBuf, unsigned long size).</summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetBlockCallback(IntPtr param, uint position, IntPtr buffer, uint size);

internal static class PdfiumNativeExtra
{
    private const string Lib = "pdfium";

    // Loại annotation (fpdf_annot.h)
    public const int FPDF_ANNOT_TEXT = 1;
    public const int FPDF_ANNOT_LINK = 2;
    public const int FPDF_ANNOT_FREETEXT = 3;
    public const int FPDF_ANNOT_LINE = 4;
    public const int FPDF_ANNOT_SQUARE = 5;
    public const int FPDF_ANNOT_CIRCLE = 6;
    public const int FPDF_ANNOT_POLYGON = 7;
    public const int FPDF_ANNOT_POLYLINE = 8;
    public const int FPDF_ANNOT_HIGHLIGHT = 9;
    public const int FPDF_ANNOT_UNDERLINE = 10;
    public const int FPDF_ANNOT_SQUIGGLY = 11;
    public const int FPDF_ANNOT_STRIKEOUT = 12;
    public const int FPDF_ANNOT_STAMP = 13;
    public const int FPDF_ANNOT_INK = 15;
    public const int FPDF_ANNOT_POPUP = 16;
    public const int FPDF_ANNOT_WIDGET = 20;

    public const int FPDFANNOT_COLORTYPE_Color = 0;
    public const int FPDFANNOT_COLORTYPE_InteriorColor = 1;

    // Loại form field (fpdf_formfill.h)
    public const int FPDF_FORMFIELD_UNKNOWN = 0;
    public const int FPDF_FORMFIELD_PUSHBUTTON = 1;
    public const int FPDF_FORMFIELD_CHECKBOX = 2;
    public const int FPDF_FORMFIELD_RADIOBUTTON = 3;
    public const int FPDF_FORMFIELD_COMBOBOX = 4;
    public const int FPDF_FORMFIELD_LISTBOX = 5;
    public const int FPDF_FORMFIELD_TEXTFIELD = 6;

    public const int FPDF_FORMFLAG_READONLY = 1 << 0;
    public const int FPDF_FORMFLAG_TEXT_MULTILINE = 1 << 12;
    public const int FPDF_FORMFLAG_TEXT_PASSWORD = 1 << 13;

    public const int FORMTYPE_NONE = 0;

    // Loại action (fpdf_doc.h)
    public const uint PDFACTION_GOTO = 1;
    public const uint PDFACTION_URI = 3;

    // Định dạng bitmap
    public const int FPDFBitmap_BGRA = 4;

    // Cờ lưu: bỏ bảo mật
    public const uint FPDF_REMOVE_SECURITY = 1 << 2;

    // ---------- Annotation ----------
    [DllImport(Lib)]
    public static extern IntPtr FPDFPage_CreateAnnot(IntPtr page, int subtype);

    [DllImport(Lib)]
    public static extern int FPDFPage_GetAnnotCount(IntPtr page);

    [DllImport(Lib)]
    public static extern IntPtr FPDFPage_GetAnnot(IntPtr page, int index);

    [DllImport(Lib)]
    public static extern void FPDFPage_CloseAnnot(IntPtr annot);

    [DllImport(Lib)]
    public static extern int FPDFPage_RemoveAnnot(IntPtr page, int index);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetSubtype(IntPtr annot);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_SetColor(IntPtr annot, int type, uint r, uint g, uint b, uint a);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetColor(IntPtr annot, int type, out uint r, out uint g, out uint b, out uint a);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_AppendAttachmentPoints(IntPtr annot, ref FsQuadPointsF quadPoints);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_SetRect(IntPtr annot, ref FsRectF rect);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetRect(IntPtr annot, out FsRectF rect);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_AddInkStroke(IntPtr annot, FsPointF[] points, nuint pointCount);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_SetBorder(IntPtr annot, float horizontalRadius, float verticalRadius, float borderWidth);

    [DllImport(Lib, CharSet = CharSet.Unicode)]
    public static extern int FPDFAnnot_SetStringValue(IntPtr annot, byte[] key, [MarshalAs(UnmanagedType.LPWStr)] string value);

    [DllImport(Lib)]
    public static extern uint FPDFAnnot_GetStringValue(IntPtr annot, byte[] key, byte[]? buffer, uint bufferLength);

    // ---------- Form ----------
    [DllImport(Lib)]
    public static extern int FPDF_GetFormType(IntPtr document);

    [DllImport(Lib)]
    public static extern IntPtr FPDFDOC_InitFormFillEnvironment(IntPtr document, IntPtr formInfo);

    [DllImport(Lib)]
    public static extern void FPDFDOC_ExitFormFillEnvironment(IntPtr form);

    /// <summary>Vẽ ô form lên bitmap (FPDF_RenderPageBitmap không vẽ widget khi có môi trường form).</summary>
    [DllImport(Lib)]
    public static extern void FPDF_FFLDraw(IntPtr form, IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [DllImport(Lib)]
    public static extern void FORM_OnAfterLoadPage(IntPtr page, IntPtr form);

    [DllImport(Lib)]
    public static extern void FORM_OnBeforeClosePage(IntPtr page, IntPtr form);

    [DllImport(Lib)]
    public static extern int FORM_SetFocusedAnnot(IntPtr form, IntPtr annot);

    [DllImport(Lib)]
    public static extern int FORM_SelectAllText(IntPtr form, IntPtr page);

    [DllImport(Lib, CharSet = CharSet.Unicode)]
    public static extern void FORM_ReplaceSelection(IntPtr form, IntPtr page, [MarshalAs(UnmanagedType.LPWStr)] string text);

    [DllImport(Lib)]
    public static extern int FORM_ForceToKillFocus(IntPtr form);

    [DllImport(Lib)]
    public static extern int FORM_OnLButtonDown(IntPtr form, IntPtr page, int modifier, double pageX, double pageY);

    [DllImport(Lib)]
    public static extern int FORM_OnLButtonUp(IntPtr form, IntPtr page, int modifier, double pageX, double pageY);

    [DllImport(Lib)]
    public static extern int FORM_SetIndexSelected(IntPtr form, IntPtr page, int index, int selected);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetFormFieldType(IntPtr form, IntPtr annot);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetFormFieldFlags(IntPtr form, IntPtr annot);

    [DllImport(Lib)]
    public static extern uint FPDFAnnot_GetFormFieldName(IntPtr form, IntPtr annot, byte[]? buffer, uint bufferLength);

    [DllImport(Lib)]
    public static extern uint FPDFAnnot_GetFormFieldValue(IntPtr form, IntPtr annot, byte[]? buffer, uint bufferLength);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_IsChecked(IntPtr form, IntPtr annot);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_GetOptionCount(IntPtr form, IntPtr annot);

    [DllImport(Lib)]
    public static extern uint FPDFAnnot_GetOptionLabel(IntPtr form, IntPtr annot, int index, byte[]? buffer, uint bufferLength);

    [DllImport(Lib)]
    public static extern int FPDFAnnot_IsOptionSelected(IntPtr form, IntPtr annot, int index);

    // ---------- Mục lục & liên kết ----------
    [DllImport(Lib)]
    public static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr document, IntPtr bookmark);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr document, IntPtr bookmark);

    [DllImport(Lib)]
    public static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, byte[]? buffer, uint bufferLength);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBookmark_GetDest(IntPtr document, IntPtr bookmark);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);

    [DllImport(Lib)]
    public static extern uint FPDFAction_GetType(IntPtr action);

    [DllImport(Lib)]
    public static extern IntPtr FPDFAction_GetDest(IntPtr document, IntPtr action);

    [DllImport(Lib)]
    public static extern uint FPDFAction_GetURIPath(IntPtr document, IntPtr action, byte[]? buffer, uint bufferLength);

    [DllImport(Lib)]
    public static extern int FPDFDest_GetDestPageIndex(IntPtr document, IntPtr dest);

    [DllImport(Lib)]
    public static extern int FPDFLink_Enumerate(IntPtr page, ref int startPos, out IntPtr linkAnnot);

    [DllImport(Lib)]
    public static extern int FPDFLink_GetAnnotRect(IntPtr linkAnnot, out FsRectF rect);

    [DllImport(Lib)]
    public static extern IntPtr FPDFLink_GetDest(IntPtr document, IntPtr link);

    [DllImport(Lib)]
    public static extern IntPtr FPDFLink_GetAction(IntPtr link);

    // ---------- Ký tự ----------
    [DllImport(Lib)]
    public static extern uint FPDFText_GetUnicode(IntPtr textPage, int index);

    [DllImport(Lib)]
    public static extern int FPDFText_GetLooseCharBox(IntPtr textPage, int index, out FsRectF rect);

    // ---------- Ảnh & path ----------
    [DllImport(Lib)]
    public static extern IntPtr FPDFPageObj_NewImageObj(IntPtr document);

    [DllImport(Lib)]
    public static extern int FPDFImageObj_SetBitmap(IntPtr[]? pages, int count, IntPtr imageObject, IntPtr bitmap);

    [DllImport(Lib)]
    public static extern int FPDFImageObj_LoadJpegFileInline(IntPtr[]? pages, int count, IntPtr imageObject, ref FpdfFileAccess fileAccess);

    [DllImport(Lib)]
    public static extern IntPtr FPDFImageObj_GetRenderedBitmap(IntPtr document, IntPtr page, IntPtr imageObject);

    [DllImport(Lib)]
    public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    [DllImport(Lib)]
    public static extern int FPDFBitmap_GetWidth(IntPtr bitmap);

    [DllImport(Lib)]
    public static extern int FPDFBitmap_GetHeight(IntPtr bitmap);

    [DllImport(Lib)]
    public static extern int FPDFBitmap_GetFormat(IntPtr bitmap);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_SetStrokeColor(IntPtr pageObject, uint r, uint g, uint b, uint a);

    [DllImport(Lib)]
    public static extern int FPDFPageObj_SetStrokeWidth(IntPtr pageObject, float width);

    [DllImport(Lib)]
    public static extern int FPDF_ImportPagesByIndex(IntPtr destDoc, IntPtr srcDoc, int[] pageIndices, uint length, int index);
}
