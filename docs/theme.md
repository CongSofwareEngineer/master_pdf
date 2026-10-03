# Giao diện tối / sáng (WPF-UI Fluent)

## Mục đích
Giao diện hiện đại kiểu Windows 11 (Fluent Design), mặc định tông tối, có thể chuyển sáng / theo hệ thống.

## File / module liên quan
- `App.xaml` — merge `ui:ThemesDictionary Theme="Dark"` + `ui:ControlsDictionary` (NuGet `WPF-UI` 4.3.0) + `Resources/Styles.xaml`.
- `App.xaml.cs` — gọi `ThemeService.Apply(settings.Theme)` trước khi tạo `MainWindow`.
- `Resources/Styles.xaml` — style riêng của app (nút ribbon, tab, thẻ, panel…), chỉ dùng `DynamicResource`.
- `Views/ThemeService.cs` — áp dụng theme, màu nhấn, brush riêng; event `ThemeChanged`.
- `MainWindow.xaml(.cs)` — `ui:FluentWindow` (Mica), nút đổi nhanh sáng / tối trên dải tab.
- `Views/SettingsDialog.xaml(.cs)` — chọn Tối / Sáng / Theo hệ thống.
- `Models/AppSettings.cs` — `Theme` (`AppTheme.Dark` mặc định / `Light` / `System`).

## Logic chính
- `ThemeService.Apply(theme)`: `System` đọc `ApplicationThemeManager.GetSystemTheme()`; rồi
  `ApplicationThemeManager.Apply(Dark|Light, WindowBackdropType.Mica, false)`.
- Màu nhấn đặt rõ 4 sắc độ qua `ApplicationAccentColorManager.Apply`:
  tối `#4F8DFF / #6EA2FF / #8EB8FF / #B3D0FF`; sáng `#2563EB / #1D4ED8 / #1E40AF / #1E3A8A`
  (nền sáng cần màu đậm để chữ trắng trên nút chính đủ tương phản).
- Brush riêng của app (gán lại vào `Application.Resources` mỗi lần đổi theme, đã `Freeze`):
  `ViewerBackgroundBrush`, `RibbonBackgroundBrush`, `PanelBackgroundBrush`, `FloatingBarBrush`,
  `AccentBrandBrush`, `AccentSoftBrush`, `SelectionStrokeBrush`, `SelectionFillBrush`,
  `TextSelectionBrush`, `SearchHitBrush`, `SearchCurrentBrush`, `RedactBrush`, `DangerBrush`.
- Mọi màu trong XAML dùng `DynamicResource` (key WPF-UI như `TextFillColorPrimaryBrush`,
  `ControlStrokeColorDefaultBrush` hoặc key ở trên) nên đổi theme có hiệu lực ngay, không khởi động lại.
- Icon dùng `ui:SymbolIcon` (Fluent System Icons) — test `AllSymbolsInXaml_Exist` kiểm tra tên icon.
- Trang PDF luôn nền trắng + viền mảnh `ControlStrokeColorDefaultBrush` (không dùng DropShadowEffect vì
  làm cuộn / zoom giật). "Đọc ban đêm" (đảo màu trang) là tính năng riêng của viewer (xem viewer.md).

## Lưu ý / giới hạn
- Mica chỉ có trên Windows 11; Windows 10 dùng nền đặc của theme.
- Thêm brush mới cho app → đặt trong `ThemeService.Apply` (cả 2 nhánh) và dùng `DynamicResource`;
  test `DynamicKeys_Exist` sẽ fail nếu XAML dùng key không tồn tại.

## Cách test
- Tự động: `UiResourceTests` (`AllSymbolsInXaml_Exist`, `DynamicKeys_Exist`).
- Thủ công: Cài đặt → Giao diện → Tối / Sáng / Theo hệ thống; nút mặt trăng trên dải tab. Kiểm tra
  nút chính, ribbon, panel, thanh zoom nổi, tô sáng tìm kiếm ở cả hai theme.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành: WPF-UI 4.3 FluentWindow + Mica, 3 chế độ, brush riêng, màu nhấn 4 sắc độ theo theme | Theme sáng ban đầu dùng accent nhạt → chữ trên nút chính khó đọc |
