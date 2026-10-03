# Lưu file

## Mục đích
Lưu ghi đè (Save), lưu tên mới (Save As), xác nhận trước khi lưu, báo lỗi rõ ràng; nhắc lưu khi đóng tab / app.

## File / module liên quan
- `Services/PdfService.cs` — `SaveToFile`, `SaveTo`, `SaveToBytes`, `SaveWithSecurity`, `SaveCore` (`FPDF_SaveAsCopy`).
- `Services/FileService.cs` — `WriteAtomic`.
- `Views/DocumentWorkspace.Commands.cs` — `SaveAsync(saveAs)`, `ConfirmCloseAsync` (hỏi lưu), `WaitIdleAsync`.
- `Views/UiHelpers.cs` — `Dialogs.ConfirmAsync`, `Dialogs.AskSaveAsync` (WPF-UI MessageBox).
- `MainWindow.xaml.cs` — lệnh Lưu / Lưu thành, `CloseWorkspaceAsync`, `OnClosing` (xem tabs.md).

## Logic chính
- Lưu (Ctrl+S): tài liệu chưa có đường dẫn → như Lưu thành. Có đường dẫn → hỏi "Ghi đè file gốc?" (tắt được trong
  Cài đặt, `ConfirmBeforeOverwrite`).
- Lưu thành (Ctrl+Shift+S): SaveFileDialog lọc `*.pdf`, tự thêm đuôi, hỏi khi trùng tên.
- Trước khi lưu: hoàn tất ô sửa trực tiếp đang mở và chờ thao tác nền xong (`WaitIdleAsync`).
- Bảo mật khi lưu theo `SecurityMode` (giữ / gỡ / đặt mật khẩu) — xem protect.md.
- `WriteAtomic`: ghi file tạm `.<tên>.<guid>.tmp` cùng thư mục → flush → `File.Move(overwrite)`. Lỗi giữa chừng → xóa
  file tạm, file cũ còn nguyên. Ghi đè được chính file đang mở vì tài liệu nằm trong RAM.
- Lưu xong: bỏ dấu • trên tab / tiêu đề, thêm vào file gần đây, Snackbar "Đã lưu".
- Đóng tab / app khi có thay đổi → hỏi Lưu / Không lưu / Hủy.
- Lỗi → thông báo theo `PdfErrorKind` (vd. `Error_File`: file bị chương trình khác khóa) kèm chi tiết.

## Lưu ý / giới hạn
- Lưu luôn ghi lại toàn bộ file (không incremental).

## Cách test
- Tự động: `SaveToFile_WritesFileAndClearsModified`, `SaveToFile_OverwritesOpenedFile`,
  `FileService_WriteAtomic_FailureKeepsOriginal`, `DocumentFeatureTests.Security_ApplyAndRemovePassword`.
- Thủ công: mở file trong thư mục chỉ đọc → Lưu (báo lỗi rõ ràng); sửa rồi đóng tab / app (hỏi lưu).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo Save / Save As / ghi nguyên tử / nhắc lưu | Khởi tạo dự án |
| 2026-10-03 | Chuyển luồng lưu sang `DocumentWorkspace` (mỗi tab), hộp thoại WPF-UI + Snackbar, lưu kèm chế độ bảo mật | Đa tab + UI v2 + tính năng mật khẩu |
