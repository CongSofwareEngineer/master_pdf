using System.Runtime.InteropServices;
using System.Text;
using PDFEditorApp.Models;
using PDFEditorApp.Services.Pdfium;
using static PDFEditorApp.Services.Pdfium.PdfiumNative;
using static PDFEditorApp.Services.Pdfium.PdfiumNativeExtra;

namespace PDFEditorApp.Services;

// Điền form (AcroForm) qua môi trường form-fill của PDFium. Xem docs/forms.md.
public sealed partial class PdfService
{
    /// <summary>Đủ lớn cho FPDF_FORMFILLINFO (mọi callback = null; PDFium kiểm tra null trước khi gọi).</summary>
    private const int FormInfoSize = 1024;

    public bool HasForms
    {
        get { lock (PdfiumLibrary.Sync) { return _form != IntPtr.Zero; } }
    }

    /// <summary>Các ô form trên trang.</summary>
    public IReadOnlyList<FormFieldInfo> GetFormFields(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            var result = new List<FormFieldInfo>();
            if (_form == IntPtr.Zero)
            {
                return result;
            }

            var page = ActivatePage(pageIndex);
            var info = BuildPageInfo(page, pageIndex);
            var count = FPDFPage_GetAnnotCount(page);
            for (var i = 0; i < count; i++)
            {
                var annot = FPDFPage_GetAnnot(page, i);
                if (annot == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    if (FPDFAnnot_GetSubtype(annot) != FPDF_ANNOT_WIDGET || FPDFAnnot_GetRect(annot, out var r) == 0)
                    {
                        continue;
                    }

                    var kind = FPDFAnnot_GetFormFieldType(_form, annot) switch
                    {
                        FPDF_FORMFIELD_PUSHBUTTON => FormFieldKind.PushButton,
                        FPDF_FORMFIELD_CHECKBOX => FormFieldKind.CheckBox,
                        FPDF_FORMFIELD_RADIOBUTTON => FormFieldKind.RadioButton,
                        FPDF_FORMFIELD_COMBOBOX => FormFieldKind.ComboBox,
                        FPDF_FORMFIELD_LISTBOX => FormFieldKind.ListBox,
                        FPDF_FORMFIELD_TEXTFIELD => FormFieldKind.Text,
                        _ => FormFieldKind.Unknown,
                    };
                    var flags = FPDFAnnot_GetFormFieldFlags(_form, annot);
                    var options = new List<string>();
                    var selected = -1;
                    if (kind is FormFieldKind.ComboBox or FormFieldKind.ListBox)
                    {
                        var optionCount = FPDFAnnot_GetOptionCount(_form, annot);
                        for (var o = 0; o < optionCount; o++)
                        {
                            options.Add(ReadWide((b, n) => FPDFAnnot_GetOptionLabel(_form, annot, o, b, n)));
                            if (selected < 0 && FPDFAnnot_IsOptionSelected(_form, annot, o) != 0)
                            {
                                selected = o;
                            }
                        }
                    }

                    var bounds = new PdfBounds(r.Left, Math.Min(r.Bottom, r.Top), r.Right, Math.Max(r.Bottom, r.Top));
                    result.Add(new FormFieldInfo(
                        i,
                        kind,
                        ReadWide((b, n) => FPDFAnnot_GetFormFieldName(_form, annot, b, n)),
                        ReadWide((b, n) => FPDFAnnot_GetFormFieldValue(_form, annot, b, n)),
                        FPDFAnnot_IsChecked(_form, annot) != 0,
                        (flags & FPDF_FORMFLAG_READONLY) != 0,
                        (flags & FPDF_FORMFLAG_TEXT_MULTILINE) != 0,
                        (flags & FPDF_FORMFLAG_TEXT_PASSWORD) != 0,
                        options,
                        selected,
                        info.ToView(bounds)));
                }
                finally
                {
                    FPDFPage_CloseAnnot(annot);
                }
            }

            return result;
        }
    }

    /// <summary>Ghi chữ vào ô text.</summary>
    public void SetFormFieldText(int pageIndex, int annotIndex, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        WithFormField(pageIndex, annotIndex, (page, annot, center) =>
        {
            _ = FORM_SetFocusedAnnot(_form, annot);
            _ = FORM_SelectAllText(_form, page);
            FORM_ReplaceSelection(_form, page, text);
        });
    }

    /// <summary>Bật / tắt ô đánh dấu hoặc chọn nút radio (giả lập một cú click giữa ô).</summary>
    public void ToggleFormField(int pageIndex, int annotIndex) =>
        WithFormField(pageIndex, annotIndex, (page, annot, center) =>
        {
            _ = FORM_OnLButtonDown(_form, page, 0, center.X, center.Y);
            _ = FORM_OnLButtonUp(_form, page, 0, center.X, center.Y);
        });

    /// <summary>Chọn một mục trong danh sách / combo.</summary>
    public void SetFormFieldChoice(int pageIndex, int annotIndex, int optionIndex) =>
        WithFormField(pageIndex, annotIndex, (page, annot, center) =>
        {
            _ = FORM_SetFocusedAnnot(_form, annot);
            _ = FORM_SetIndexSelected(_form, page, optionIndex, 1);
        });

    private void WithFormField(int pageIndex, int annotIndex, Action<IntPtr, IntPtr, PointD> action)
    {
        Mutate(() =>
        {
            if (_form == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            var page = ActivatePage(pageIndex);
            var annot = FPDFPage_GetAnnot(page, annotIndex);
            if (annot == IntPtr.Zero)
            {
                throw new PdfException(PdfErrorKind.InvalidObject);
            }

            try
            {
                if (FPDFAnnot_GetSubtype(annot) != FPDF_ANNOT_WIDGET || FPDFAnnot_GetRect(annot, out var r) == 0)
                {
                    throw new PdfException(PdfErrorKind.InvalidObject);
                }

                action(page, annot, new PointD((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2));
                _ = FORM_ForceToKillFocus(_form);
            }
            finally
            {
                FPDFPage_CloseAnnot(annot);
            }

            return 0;
        });
    }

    private void InitForms()
    {
        ExitForms();
        if (_doc == IntPtr.Zero || FPDF_GetFormType(_doc) == FORMTYPE_NONE)
        {
            return;
        }

        _formInfo = Marshal.AllocHGlobal(FormInfoSize);
        unsafe
        {
            new Span<byte>((void*)_formInfo, FormInfoSize).Clear();
        }

        Marshal.WriteInt32(_formInfo, 1); // version = 1 (không XFA)
        _form = FPDFDOC_InitFormFillEnvironment(_doc, _formInfo);
        if (_form == IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_formInfo);
            _formInfo = IntPtr.Zero;
        }
    }

    private void ExitForms()
    {
        if (_form != IntPtr.Zero)
        {
            FPDFDOC_ExitFormFillEnvironment(_form);
            _form = IntPtr.Zero;
        }

        if (_formInfo != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_formInfo);
            _formInfo = IntPtr.Zero;
        }
    }

    /// <summary>Đọc chuỗi UTF-16LE theo kiểu PDFium: gọi lần 1 lấy độ dài (byte, kể cả NUL), lần 2 lấy dữ liệu.</summary>
    private static string ReadWide(Func<byte[]?, uint, uint> getter)
    {
        var size = getter(null, 0);
        if (size <= 2)
        {
            return string.Empty;
        }

        var buffer = new byte[size];
        _ = getter(buffer, size);
        return NormalizeSpaces(Encoding.Unicode.GetString(buffer, 0, (int)size - 2));
    }
}
