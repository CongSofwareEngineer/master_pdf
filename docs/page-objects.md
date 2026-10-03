# Sửa đối tượng & chèn ảnh

## Mục đích
Như Acrobat "Edit PDF": chọn ảnh / hình vẽ / khối chữ trên trang để di chuyển, đổi kích thước, xóa; chèn ảnh mới.

## File / module liên quan
- `Services/PdfService.Objects.cs` — `GetPageObjects`, `TransformObject`, `DeleteObject`, `InsertImage`
  (+ `ApplyRedactions`, xem protect.md).
- `Models/EditModels.cs` — `PageObjectKind`, `PageObjectInfo`, `ImageData`.
- `Views/Tools/ContentTools.cs` — `EditContentTool` (khung chọn, 8 tay nắm, kéo di chuyển, Delete xóa,
  nhấp đúp vào chữ để sửa), `PlaceTool` (đặt ảnh / chữ ký / ✓ ✗ ngày theo bóng xem trước).
- `Views/UiHelpers.cs` — `ImageCodec.Load` giải mã PNG / JPG / BMP / GIF / TIFF (WPF) thành `ImageData`.
- `Views/Panels/PropertiesPanel` — thông tin + nút Xóa / sửa chữ của object đang chọn.

## Logic chính
- `GetPageObjects` liệt kê text / ảnh / path / shading / form XObject với khung trong hệ view; bỏ qua object
  che (mark `PDFEditorCover`); khối chữ kèm nội dung để hiện trong panel.
- Biến đổi: tool tính `Affine` trong hệ view (dịch + co giãn quanh tay nắm đối diện; ảnh luôn giữ tỉ lệ, object khác giữ tỉ lệ khi nhấn Shift;
  phím mũi tên dịch 1 pt, Shift+mũi tên 10 pt, Delete xóa, Esc bỏ chọn),
  service đổi sang hệ trang (`ViewToPage`) rồi `FPDFPageObj_Transform`, sau đó tái tạo nội dung có kiểm tra
  (`EditObject` → `GenerateContent` → `VerifyPage`; lỗi gộp font của PDFium xử lý như text-edit.md:
  `FontNameFixer` trước, không được thì dùng lớp che).
- Chèn ảnh: JPEG giữ nguyên dữ liệu nén (`FPDFImageObj_LoadJpegFileInline`), định dạng khác chuyển BGRA rồi
  `FPDFImageObj_SetBitmap`; ma trận `ImageMatrix` giữ ảnh đứng thẳng trên màn hình kể cả trang xoay.
  Ảnh được đặt bằng `PlaceTool`: chọn file → bóng xem trước theo con trỏ → bấm để đặt (rộng mặc định 200 pt).
- Mọi thao tác đi qua `Mutate` nên Undo / Redo được.

## Lưu ý / giới hạn
- Object nằm trong Form XObject chỉ chọn / di chuyển được cả khối, không chọn riêng từng phần.
- Không xoay tự do object (chỉ di chuyển / co giãn).

## Cách test
- Tự động: `PageObjectTests.InsertImage_MoveResizeDelete`, `PageObjectTests.FontNameFixer_AllowsTrueDeletionOnAmbiguousPdf`.
- Thủ công: Chỉnh sửa → Sửa nội dung, chọn ảnh, kéo tay nắm (thử Shift), Delete; Chèn ảnh JPG + PNG; lưu, mở lại.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành chọn / di chuyển / co giãn / xóa object, chèn ảnh (JPEG inline), đặt bằng bóng xem trước | Tính năng Edit PDF kiểu Acrobat |
