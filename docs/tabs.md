# Nhiều tài liệu (tab) + màn hình chính

## Mục đích
Mở nhiều PDF cùng lúc trong các tab như trình duyệt / Acrobat; màn hình chính có thao tác nhanh và file gần đây.

## File / module liên quan
- `MainWindow.xaml(.cs)` — dải tab (`TabStrip`, model `DocTab`), chuyển / đóng tab, chuyển lệnh ribbon tới tab đang mở (`Active`).
- `Views/HomeView.xaml(.cs)` — màn hình chính: thẻ Mở, Tạo mới, Kết hợp, Ảnh → PDF + danh sách file gần đây.
- `Views/DocumentWorkspace.xaml(.cs)` — mỗi tab = một workspace sở hữu `PdfService` riêng.

## Logic chính
- Tab đầu tiên luôn là Trang chủ (`DocTab.Workspace == null`, không có nút đóng).
- `OpenFileAsync(path)`: file đã mở (so sánh đường dẫn đầy đủ, không phân biệt hoa thường) → chỉ chuyển sang tab đó;
  file không tồn tại → báo lỗi + xóa khỏi danh sách gần đây; ngược lại `LoadIntoNewTabAsync` tạo workspace mới,
  thêm vào file gần đây.
- Kéo thả nhiều file PDF vào cửa sổ → mở lần lượt từng file (mỗi file một tab).
- Đóng tab (`CloseWorkspaceAsync`): có thay đổi chưa lưu → hỏi Lưu / Không lưu / Hủy.
- Đóng app (`OnClosing`): hủy đóng, hỏi lần lượt từng tab; Hủy ở tab nào thì dừng; xong mới lưu kích thước cửa sổ và đóng.
- Tiêu đề cửa sổ = tên file tab đang mở; dấu chấm trên tab = có thay đổi chưa lưu.
- Phím tắt: Ctrl+Tab / Ctrl+Shift+Tab chuyển tab, Ctrl+W đóng tab, Ctrl+O mở, Ctrl+N tạo mới.

## Lưu ý / giới hạn
- Mỗi tab giữ tài liệu + lịch sử undo trong RAM (undo tối đa 30 bước / 512 MB mỗi tài liệu).
- Lệnh ribbon bị vô hiệu khi đang ở tab Trang chủ (`CanExecute` kiểm tra `Active`).

## Cách test
- Thủ công: mở 3 file (dialog + kéo thả), mở lại file đã mở (chỉ chuyển tab), Ctrl+Tab, sửa 1 file rồi đóng app
  (phải hỏi lưu đúng tab đó), xóa một file gần đây trên đĩa rồi bấm vào nó (báo lỗi + biến khỏi danh sách).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành tab + Trang chủ, kéo thả nhiều file, hỏi lưu từng tab khi đóng app | Đa tài liệu như Acrobat / Edge |
