# Export

## Mục đích
Xuất PDF sang định dạng khác: ảnh PNG / JPG (mỗi trang một file), Word (.docx), văn bản thuần (.txt).

## File / module liên quan
- `Services/ExportService.cs` — `ExportPng`, `ExportImages` (nhận hàm mã hóa ảnh), `ExportText`, `ExportDocx`, `BuildParagraphs`.
- `Services/PngEncoder.cs` — mã hóa PNG thuần .NET (RGB 8-bit, filter Sub, zlib, chunk pHYs chứa DPI).
- `Views/UiHelpers.cs` — `ImageCodec.WriteJpeg` (JPEG chất lượng 90 bằng WPF, vì Services không dùng WPF).
- `Services/PageRangeParser.cs` — đọc phạm vi trang "1-3, 5, 8-".
- `Views/ExportDialog.xaml(.cs)` — `ExportFormat` (Png / Jpg / Docx / Text), phạm vi trang, DPI.
- `Views/DocumentWorkspace.Commands.cs` — `ExportAsync(format)`; tab ribbon "Chuyển đổi" mở thẳng định dạng tương ứng.
- NuGet `DocumentFormat.OpenXml` 3.5.1 — ghi .docx.

## Logic chính
- Tệp → Xuất (Ctrl+E) hoặc tab Chuyển đổi → dialog: định dạng, trang (tất cả / hiện tại / tùy chọn), DPI cho ảnh
  (72–300, nhớ trong settings).
- PNG / JPG: chọn thư mục → `<tên>_p01.png|jpg`… Render ở `DPI / 72` px/pt (không LCD text, kèm annotation + form).
  Chạy nền, lớp chờ hiện tiến độ "trang x/y".
- TXT: UTF-8 có BOM; mỗi trang mở đầu bằng dòng `----- n -----`.
- Word: mỗi trang → các đoạn từ `BuildParagraphs` (gộp dòng sát nhau — khoảng cách < 0.9 chiều cao dòng — thành một
  đoạn; cỡ chữ = cỡ chữ phổ biến của đoạn), ngắt trang giữa các trang PDF. Chỉ chữ (không ảnh / bảng / định dạng).
- Xong: Snackbar thông báo + tự mở Explorer chọn file vừa xuất (`SystemService.RevealInExplorer`).

## Lưu ý / giới hạn
- PDF scan (chỉ có ảnh) xuất TXT / Word rỗng (không có OCR).
- Word chỉ giữ chữ + cỡ chữ, không giữ bố cục, ảnh, bảng, font.

## Cách test
- Tự động: `PageRangeParser_ValidInput` / `_InvalidInput`, `PngEncoder_ProducesDecodablePng`,
  `PngEncoder_Crc32_MatchesKnownValue`, `ExportPng_WritesOneFilePerPage`, `ExportText_WritesUtf8TextOfPages`,
  `UiResourceTests.ExportFormats_AreAllHandled`.
- Thủ công: xuất PNG 300 DPI, JPG, Word (mở bằng Word / LibreOffice, tiếng Việt đúng dấu); nhập phạm vi sai (dialog báo lỗi).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo export PNG / TXT | Khởi tạo dự án |
| 2026-10-03 | Thêm JPG (`ExportImages` + bộ mã hóa truyền vào) và Word .docx (OpenXml, gộp đoạn) | Chuyển đổi như Acrobat "Export PDF" |
