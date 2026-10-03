# Watermark

## Mục đích
Thêm / gỡ watermark chữ (xoay, trong suốt) cho tất cả hoặc một số trang.

## File / module liên quan
- `Services/PdfService.Stamps.cs` — `AddWatermark`, `RemoveWatermarks`, `RemoveMarked`, `TargetPages`, `EditPage`.
- `Models/EditModels.cs` — `WatermarkOptions` (chữ, font, cỡ, màu, độ mờ, góc xoay, trang).
- `Views/StampDialogs.xaml(.cs)` — `StampDialog.AskWatermark` (dùng chung với đầu / chân trang).
- `Views/DocumentWorkspace.Commands.cs` — `AddWatermarkAsync`, `RemoveWatermarksAsync`.

## Logic chính
- Mỗi trang đích: tạo text object (đậm) bằng `PlaceText` với hướng xoay theo `RotationDegrees`, màu tô có alpha =
  `Opacity × 255`, đặt tâm giữa trang (hệ view → đúng cả trang xoay), gắn mark `PDFEditorWatermark`.
- Chỉ thêm object mới rồi tái tạo nội dung trang → không đổi nội dung gốc.
- Gỡ: duyệt mọi trang, xóa object có mark `PDFEditorWatermark` (chỉ watermark do app này thêm).
- Hộp thoại: chữ mặc định, 6 màu, góc 45° / 0° / 90° / −45°, độ mờ (slider), cỡ mặc định 64, phạm vi trang
  (trống = tất cả, cú pháp `1-3,5` như export).
- Cả thao tác là một bước Undo.

## Lưu ý / giới hạn
- Chỉ watermark chữ (chưa có watermark ảnh).
- Watermark nằm trên nội dung (object cuối trang).
- Watermark do app khác tạo không gỡ được bằng lệnh Gỡ.

## Cách test
- Tự động: `DocumentFeatureTests.Watermark_And_HeaderFooter_AddRemove`.
- Thủ công: Chỉnh sửa → Watermark, chọn trang 1-2, góc 45°, độ mờ 30%; lưu, mở lại; Gỡ watermark.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành thêm / gỡ watermark chữ, hộp thoại dùng chung `StampDialog` | Tính năng như Acrobat |
