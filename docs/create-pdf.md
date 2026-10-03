# Tạo PDF

## Mục đích
Tạo tài liệu trắng, tạo PDF từ nhiều ảnh, kết hợp nhiều file PDF thành một.

## File / module liên quan
- `Services/PdfService.Pages.cs` — `CreateFromImages`, `CreateCombined`; `PdfService.CreateNew` (trang A4 trắng).
- `Views/UiHelpers.cs` — `ImageCodec.Load` (PNG / JPG / BMP / GIF / TIFF → `ImageData`, JPEG giữ dữ liệu gốc).
- `Views/HomeView.xaml(.cs)` — thẻ Tạo mới, Kết hợp, Ảnh → PDF.
- `MainWindow.xaml(.cs)` — `NewDocumentAsync`, `CombineAsync`, `ImagesToPdfAsync`; menu Tệp có cùng lệnh.
- `Views/DocumentWorkspace.xaml.cs` — `LoadNewAsync`, `LoadImagesAsync`, `LoadCombinedAsync`, `TryWithPasswordAsync`.

## Logic chính
- Mỗi lệnh mở tab mới (`LoadIntoNewTabAsync`); tài liệu mới chưa có đường dẫn → Ctrl+S sẽ hỏi Lưu thành.
- Ảnh → PDF: mỗi ảnh một trang, kích thước trang = pixel × 72 / DPI của ảnh (DPI ≤ 1 coi là 96); ảnh phủ kín trang.
  Giải mã ảnh chạy nền (`Task.Run`). Hộp thoại chọn ảnh mở ở thư mục Ảnh (`SystemService.PicturesDirectory`).
- Kết hợp: đọc từng file, file có mật khẩu → hỏi mật khẩu (thử mở bằng `PdfService` tạm); hủy thì bỏ cả lệnh.
  Sau đó `FPDF_ImportPages` lần lượt từng file vào tài liệu mới (thứ tự = thứ tự chọn trong hộp thoại).
- Thêm trang từ file khác vào tài liệu đang mở: xem page-manage.md (Ctrl+Shift+I).

## Lưu ý / giới hạn
- Thứ tự file kết hợp theo hộp thoại Windows; muốn đổi thứ tự trang dùng màn hình Sắp xếp trang sau khi kết hợp.
- Bookmark / form của file nguồn có thể không được giữ khi kết hợp (giới hạn của `FPDF_ImportPages`).

## Cách test
- Tự động: `DocumentFeatureTests.CreateFromImages_And_Combined`, `PdfServiceTests.CreateNew_HasOneA4PageAndIsNotModified`.
- Thủ công: Trang chủ → Ảnh → PDF (chọn JPG + PNG), Kết hợp 2 file (1 file có mật khẩu), Tạo mới rồi Ctrl+S.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành Tạo mới / Ảnh → PDF / Kết hợp (hỏi mật khẩu từng file) | Tính năng như Acrobat |
