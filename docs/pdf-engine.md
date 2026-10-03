# Engine PDF (PDFium)

## Mục đích
Lõi đọc / render / chỉnh sửa / lưu PDF dùng chung cho mọi chức năng.

## File / module liên quan
- `PDFEditorApp/Services/Pdfium/PdfiumNative.cs` — khai báo P/Invoke tới `pdfium.dll` (chữ ký lấy đúng
  theo header `build/native/include/pdfium/*.h` trong package NuGet).
- `PDFEditorApp/Services/Pdfium/PdfiumLibrary.cs` — `FPDF_InitLibrary` một lần + `lock` chung `Sync`.
- `PDFEditorApp/Services/PdfService.cs` — phiên làm việc với MỘT tài liệu (mở, render, sửa text, undo, lưu); các
  phần khác là `partial`: `.Reading` (text layout, link, bookmark), `.Annotations`, `.Objects`, `.Forms`, `.Pages`, `.Stamps`.
- `PDFEditorApp/Services/Pdfium/PdfiumNativeExtra.cs` — P/Invoke thêm cho annotation, form-fill, bookmark, link,
  ký tự, ảnh, `FPDF_FFLDraw`, `FPDF_REMOVE_SECURITY`.
- `PDFEditorApp/Services/PdfException.cs` — `PdfErrorKind` (UI đổi sang thông báo key `Error_<Kind>`).
- NuGet `bblanchon.PDFium.Win32` (156.0.8076) — cung cấp `pdfium.dll` cho win-x64/x86/arm64.

## Logic chính
- **Vì sao PDFium** (thay vì iText/PdfSharp gợi ý trong `design.md`): một thư viện làm được cả render nhanh
  (engine của Chrome) lẫn sửa object (text, trang) và lưu. iText 7 là AGPL; PdfSharp không render được.
- **Thread-safety**: PDFium không thread-safe → mọi phương thức public của `PdfService` đều
  `lock (PdfiumLibrary.Sync)`. UI gọi qua `Task.Run`. UI cache trạng thái (số trang, undo…) để `CanExecute`
  không phải chờ lock.
- **Mở file**: bytes được copy sang bộ nhớ không quản lý (`Marshal.AllocHGlobal`) rồi
  `FPDF_LoadMemDocument64`. Buffer sống tới khi đóng tài liệu (PDFium đọc lười). Nhờ vậy có thể ghi đè
  chính file gốc khi lưu.
- **Trang "đang hoạt động"** (`ActivatePage`): trang hiện tại được giữ mở để handle text object hợp lệ giữa
  các lần sửa. Thao tác đổi cấu trúc trang / undo đóng nó (`InvalidateActivePage`). Render trang khác
  (thumbnail, export) mở tạm rồi đóng.
- **Tọa độ**: `BuildPageInfo` lấy ma trận trang → view bằng cách chiếu 3 điểm qua `FPDF_PageToDevice` ở độ
  phân giải 100 px/pt (sai số làm tròn không đáng kể). Tự xử lý CropBox lệch gốc và góc xoay.
  Hệ "view": point, gốc trên-trái, Y hướng xuống; UI nhân `zoom × 96/72` ra DIP.
- **Render** (`RenderCore`): `FPDFBitmap_Create` (BGRx) → nền trắng → `FPDF_RenderPageBitmap` (cờ `FPDF_ANNOT`, thêm
  `FPDF_LCD_TEXT` cho màn hình) → nếu tài liệu có form: `FPDF_FFLDraw` để vẽ widget (xem forms.md). Ảnh > 40 triệu
  pixel tự giảm độ phân giải.
- **Form-fill**: môi trường `FPDFDOC_InitFormFillEnvironment` tạo khi mở / nạp lại snapshot, hủy trước khi đóng tài liệu.
- **Lưu**: `FPDF_SaveAsCopy` (`FPDF_NO_INCREMENTAL`, thêm `FPDF_REMOVE_SECURITY` khi gỡ / đặt mật khẩu) với callback
  `WriteBlock` ghi vào `Stream`. Mã hóa dùng PDFsharp (protect.md).
- **Mark** đánh dấu object do app tạo: `PDFEditorText`, `PDFEditorObject`, `PDFEditorCover`, `PDFEditorWatermark`,
  `PDFEditorHeaderFooter`.
- **Lỗi PDFium khi tái tạo nội dung**: xem `text-edit.md` mục "Chế độ phủ".

## Lưu ý / giới hạn
- `Models/` và `Services/` không dùng WPF: render trả về byte BGRA (`RenderedPage`), View tự đổi sang ảnh
  (`Views/BitmapHelper.cs`). `ArchitectureTests` kiểm tra quy tắc này.
- Font phải đóng (`FPDFFont_Close`) TRƯỚC `FPDF_CloseDocument` (`CloseDocumentHandles`).
- PDFium trả dấu cách của font CID nhúng thành U+00A0 → `NormalizeSpaces` đổi về dấu cách thường.

## Cách test
- `dotnet test` — `PdfServiceTests` (mở file hỏng/rỗng, render, tọa độ, sửa text, trang, undo, lưu) + `FeatureTests`
  (đọc, nhận xét, object, form, trang hàng loạt, tạo PDF, watermark, bảo mật) dùng PDF dựng bằng `PdfFixtures`.
- Đặt biến môi trường `PDFEDITOR_RENDER_DIR=<thư mục>` rồi chạy test → test `RenderedSample_...` xuất ảnh
  PNG + `sample.pdf` mẫu để kiểm tra bằng mắt.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo engine PDFium (P/Invoke, PdfService) | Khởi tạo dự án |
| 2026-10-03 | Chuẩn hóa U+00A0 → dấu cách khi đọc text | PDFium trả NBSP cho font CID nhúng |
| 2026-10-03 | Tách `PdfService` thành các file partial; thêm P/Invoke annotation / form / bookmark / link; render vẽ widget form bằng `FPDF_FFLDraw` | Tính năng v2 kiểu Acrobat; ô đánh dấu đã tick không hiện |
