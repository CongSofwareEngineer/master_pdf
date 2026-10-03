# Chữ ký & Điền nhanh

## Mục đích
Như "Fill & Sign": tạo chữ ký (vẽ / gõ), lưu để dùng lại, đặt lên trang; thêm nhanh ✓, ✗, ngày hôm nay.

## File / module liên quan
- `Views/SignatureDialog.xaml(.cs)` — tạo chữ ký: tab Vẽ (canvas, nút Xóa) và tab Gõ (chọn font viết tay, màu).
- `Views/Panels/PropertiesPanel.xaml(.cs)` — mục "Chữ ký": danh sách chữ ký đã lưu (vẽ bằng `SignaturePreview`),
  bấm để chọn đặt, nút xóa, nút Tạo chữ ký mới.
- `Models/EditModels.cs` — `SavedSignature` (`Id`, `Strokes` chuẩn hóa 0..1, `AspectRatio`, `Text`, `FontFamily`, `Color`).
- `Models/AppSettings.cs` — `Signatures` (lưu trong `settings.json`, xem settings.md).
- `Services/FontService.cs` — `HandwritingFamilies` (font viết tay có trên máy), `ScriptFamily` (mặc định Segoe Script).
- `Views/Tools/ContentTools.cs` — `PlaceTool` (bóng xem trước theo con trỏ, bấm để đặt), `PlaceSignature`.
- `Views/DocumentWorkspace.Commands.cs` — `SetStamp`, `UseSignature`, `CreateSignature`, `DeleteSignature`.
- `MainWindow.xaml` — tab ribbon "Điền & Ký": Thêm chữ, ✓, ✗, Ngày, Ký (công cụ đặt chữ ký), Tạo chữ ký mới, Thông tin form.

## Logic chính
- Chữ ký vẽ: nét được chuẩn hóa về 0..1 theo khung bao + lưu tỉ lệ rộng/cao; khi đặt, nét được ánh xạ vào khung
  (rộng 160 pt) → `AddInk` (Ink annotation, nét 1.6 pt, màu chữ ký).
- Chữ ký gõ: `AddText` với font viết tay đã chọn, cỡ 26 → là text object trong nội dung trang.
- ✓ / ✗: ký hiệu cỡ 18 bằng font ký hiệu (`FontService.SymbolFamily`); Ngày: `DateTime.Now` định dạng ngắn (`"d"`) của
  culture hiện tại, kiểu chữ của công cụ Thêm chữ. Màu = màu bút (`InkColor`). Đều là text object (sửa / xóa được bằng công cụ Sửa nội dung).
- Đặt xong tự quay về công cụ mặc định; mọi thao tác Undo / Redo được.

## Lưu ý / giới hạn
- Đây là chữ ký hình ảnh, **không phải chữ ký số** (certificate).
- Chưa có chữ ký từ file ảnh (có thể dùng Chèn ảnh thay thế).
- Chữ ký lưu trong `settings.json` của người dùng Windows hiện tại (không mã hóa).

## Cách test
- Tự động: `ServiceTests.Settings_RoundTrip` (lưu / đọc settings.json).
- Thủ công: Điền & Ký → Tạo chữ ký mới (vẽ và gõ), đặt lên trang, đặt ✓ / ✗ / ngày, lưu, mở lại; khởi động lại
  app thấy chữ ký vẫn còn; xóa chữ ký đã lưu.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành chữ ký vẽ (Ink) / gõ (text), lưu trong settings thay vì SignatureStore riêng; ✓ ✗ ngày | Giảm file lưu trữ riêng; settings đã có ghi file nguyên tử |
