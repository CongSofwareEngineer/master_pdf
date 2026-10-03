# Đầu trang / chân trang, đánh số trang

## Mục đích
Thêm số trang / đầu-chân trang (vd. "Trang 1/10") ở 6 vị trí; gỡ được.

## File / module liên quan
- `Services/PdfService.Stamps.cs` — `AddHeaderFooter`, `RemoveHeaderFooters`.
- `Models/EditModels.cs` — `HeaderFooterOptions` (mẫu, vị trí, font, cỡ, màu, lề, trang), `StampPosition`.
- `Views/StampDialogs.xaml(.cs)` — `StampDialog.AskHeaderFooter` (dùng chung với watermark).
- `Views/DocumentWorkspace.Commands.cs` — `AddHeaderFooterAsync`, `RemoveHeaderFootersAsync`.

## Logic chính
- Mẫu thay `{n}` (số trang, bắt đầu 1), `{N}` (tổng trang), `{date}` (ngày hiện tại, định dạng ngắn của culture).
- Mỗi trang đích: tạo text object nằm ngang bằng `PlaceText`, đo khung (`ObjectViewRect`) rồi đặt theo vị trí
  (Trên / Dưới × Trái / Giữa / Phải) với lề 28 pt, trong hệ view (trang hiển thị, đã xoay). Mark `PDFEditorHeaderFooter`.
- Gỡ: xóa object có mark `PDFEditorHeaderFooter` trên mọi trang.
- Hộp thoại: mẫu mặc định từ `HeaderFooter_Default`, vị trí mặc định Dưới-giữa, cỡ 10, màu đen, phạm vi trang.

## Lưu ý / giới hạn
- Vị trí tính theo trang hiển thị (đã xoay), giống cách người dùng nhìn thấy.
- Thêm trang sau khi đánh số thì số trang không tự cập nhật → Gỡ rồi thêm lại.

## Cách test
- Tự động: `DocumentFeatureTests.Watermark_And_HeaderFooter_AddRemove`.
- Thủ công: Chỉnh sửa → Đầu / chân trang, mẫu "Trang {n}/{N}", thử 6 vị trí và trang xoay; Gỡ.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành đánh số / đầu-chân trang 6 vị trí với {n} {N} {date}, gỡ theo mark | Tính năng như Acrobat |
