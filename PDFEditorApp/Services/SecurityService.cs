using PDFEditorApp.Models;
using PdfSharp.Pdf.IO;

namespace PDFEditorApp.Services;

/// <summary>
/// Mã hóa PDF bằng mật khẩu (AES-256, PDF 2.0 / R6) qua PDFsharp — PDFium không tự mã hóa được.
/// Xem docs/protect.md.
/// </summary>
public static class SecurityService
{
    /// <summary>Mã hóa bytes PDF (chưa mã hóa) theo tùy chọn; trả về bytes mới.</summary>
    public static byte[] Encrypt(byte[] pdf, SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrEmpty(options.UserPassword) && string.IsNullOrEmpty(options.OwnerPassword))
        {
            throw new ArgumentException("A password is required.", nameof(options));
        }

        try
        {
            using var input = new MemoryStream(pdf);
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var settings = document.SecuritySettings;
            settings.UserPassword = options.UserPassword;
            settings.OwnerPassword = string.IsNullOrEmpty(options.OwnerPassword) ? options.UserPassword : options.OwnerPassword;
            settings.PermitPrint = options.AllowPrint;
            settings.PermitFullQualityPrint = options.AllowPrint;
            settings.PermitExtractContent = options.AllowCopy;
            settings.PermitModifyDocument = options.AllowEdit;
            settings.PermitAnnotations = options.AllowEdit;
            settings.PermitFormsFill = options.AllowEdit;
            settings.PermitAssembleDocument = options.AllowEdit;
            document.SecurityHandler.SetEncryptionToV5(true);

            using var output = new MemoryStream();
            document.Save(output, false);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or ArgumentException))
        {
            throw new PdfException(PdfErrorKind.Save, ex);
        }
    }
}
