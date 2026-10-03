# Mở file PDF

## Mục đích
Chọn và mở file PDF từ máy tính (mỗi file một tab), hỏi mật khẩu khi cần; file gần đây.

## File / module liên quan
- `MainWindow.xaml.cs` — `OpenWithDialogAsync`, `OpenFileAsync`, `LoadIntoNewTabAsync`, kéo thả (`OnDrop`), file gần đây
  (`RefreshRecent`, menu Tệp → Gần đây).
- `Views/HomeView.xaml(.cs)` — nút Mở + danh sách file gần đây ở Trang chủ.
- `Views/DocumentWorkspace.xaml.cs` — `LoadFileAsync`, `TryWithPasswordAsync`.
- `App.xaml.cs` — đọc tham số dòng lệnh (`PDFEditor.exe file.pdf`).
- `Services/PdfService.cs` — `Open(bytes, password, path)`.
- `Services/FileService.cs` — `ReadAllBytesAsync` (lỗi IO → `PdfErrorKind.File`), `IsPdfPath`.
- `Views/PasswordDialog.xaml(.cs)` — nhập mật khẩu.
- `Models/AppSettings.cs` — `RecentFiles` (tối đa 12, bỏ trùng không phân biệt hoa thường).

## Logic chính
- Cách mở: Ctrl+O / Tệp → Mở (lọc `*.pdf`), kéo thả file `.pdf` (nhiều file được), Tệp → Gần đây, Trang chủ,
  tham số dòng lệnh. File đã mở ở tab khác → chỉ chuyển tab (xem tabs.md).
- Đọc file + load trên luồng nền (lớp chờ). Lỗi `Password` → `PasswordDialog`, thử lại tới khi đúng hoặc hủy
  (sai → báo "Mật khẩu không đúng"). Hủy / lỗi → gỡ tab vừa tạo.
- Sau khi mở: thêm vào file gần đây, dựng thumbnail, mục lục; vừa chiều rộng; tài liệu có form → thanh thông báo.
- File gần đây không còn tồn tại → báo lỗi và gỡ khỏi danh sách.
- Tạo mới / Ảnh → PDF / Kết hợp: xem create-pdf.md.

## Lưu ý / giới hạn
- File được nạp hết vào RAM (PDFium đọc lười nên file lớn vẫn mở nhanh, nhưng cần RAM tương ứng).

## Cách test
- Tự động: `Open_InvalidBytes_ThrowsFormatError`, `Open_EmptyBytes_ThrowsFormatError`, `AppSettings_RecentFiles_DedupesAndLimits`.
- Thủ công: mở bằng dialog / kéo thả 2 file / dòng lệnh; mở file không phải PDF (báo lỗi định dạng); PDF có mật khẩu
  (nhập sai rồi đúng; hủy → không còn tab rỗng).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo chức năng mở file, kéo thả, mật khẩu, file gần đây, tạo mới | Khởi tạo dự án |
| 2026-10-03 | Mở vào tab mới (`LoadIntoNewTabAsync`), Trang chủ có file gần đây, kéo thả nhiều file, gần đây tối đa 12 | Đa tài liệu (tabs.md) |
