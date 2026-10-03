# Cài đặt người dùng

## Mục đích
Lưu lựa chọn của người dùng giữa các lần mở app; hộp thoại Cài đặt.

## File / module liên quan
- `Models/AppSettings.cs` — `Language`, `Theme` (mặc định Dark), `RecentFiles` (tối đa 12), `ExportDpi`,
  `ConfirmBeforeOverwrite`, `ShowSidePanel`, `ShowProperties`, kích thước / phóng to cửa sổ, định dạng chữ mới
  (font, cỡ, màu), `AnnotationColor`, `InkColor`, `InkWidth`, `AnnotationAuthor`, `Signatures`.
- `Services/SettingsService.cs` — `Load` / `Save` (System.Text.Json, ghi qua `FileService.WriteAtomic`).
- `Services/SystemService.cs` — đường dẫn `%APPDATA%\PDFEditorApp\settings.json` (qua `Environment.GetFolderPath`).
- `Views/SettingsDialog.xaml(.cs)` (Ctrl+,) — giao diện (tối / sáng / hệ thống), ngôn ngữ, tác giả nhận xét,
  hỏi trước khi ghi đè file gốc.
- `App.xaml.cs` (đọc lúc khởi động), `MainWindow.xaml.cs` (`SaveSettings`, `SaveWindowSettings`).

## Logic chính
- Đọc khi khởi động; file không có / hỏng → dùng mặc định (không crash). `ExportDpi` kẹp trong 36–600.
- Lưu ngay khi đổi một lựa chọn (ngôn ngữ, theme, file gần đây, panel, màu công cụ, chữ ký…) và khi đóng cửa sổ.
- Đổi theme / ngôn ngữ có hiệu lực ngay (DynamicResource / binding `{v:Tr}`), không cần khởi động lại.
- Lỗi ghi chỉ ghi log Debug, không làm phiền người dùng.

## Lưu ý / giới hạn
- Xóa thư mục `%APPDATA%\PDFEditorApp` để trả về mặc định (mất cả chữ ký đã lưu).

## Cách test
- Tự động: `Settings_RoundTrip`, `Settings_CorruptFile_ReturnsDefaults`, `AppSettings_RecentFiles_DedupesAndLimits`.
- Thủ công: đổi từng mục trong Cài đặt, khởi động lại app → vẫn giữ.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo settings.json | Khởi tạo dự án |
| 2026-10-03 | Thêm Theme, màu / độ dày công cụ, tác giả nhận xét, chữ ký; RecentFiles 12; `ShowThumbnails` → `ShowSidePanel`; hộp thoại Cài đặt | UI v2 + nhận xét / chữ ký |
