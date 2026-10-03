# Tìm kiếm văn bản

## Mục đích
Tìm chữ trong toàn bộ tài liệu (Ctrl+F), tô sáng kết quả, nhảy tới kết quả trước / sau; tìm được tiếng Việt không dấu.

## File / module liên quan
- `Services/TextSearch.cs` — `FindAll` (phân biệt hoa thường, bỏ dấu tiếng Việt, cả từ), `Context`, `RemoveDiacritics`.
- `Models/ReadingModels.cs` — `SearchOptions`, `SearchHit`.
- `Views/Panels/SearchPanel.xaml(.cs)` + `SearchResultItem` — ô tìm, tùy chọn, danh sách kết quả.
- `Views/DocumentWorkspace.xaml.cs` — chạy tìm nền, tô sáng kết quả (`SearchHitBrush`, `SearchCurrentBrush`).

## Logic chính
- Tìm theo từng trang: lấy layout đã cache hoặc `Task.Run(pdf.GetTextLayout)` (có `CancellationToken`, tìm mới
  hủy lần cũ); kết quả hiện dần trong danh sách kèm đoạn ngữ cảnh.
- `Fold`: bỏ dấu = chuẩn hóa NFD, bỏ dấu kết hợp, `đ→d`; không phân biệt hoa thường = lower invariant. Giữ mảng
  `map` chỉ số chuỗi đã gấp → chuỗi gốc để tô đúng vị trí ký tự.
- Cả từ: kiểm tra ký tự trước / sau không phải chữ / số.
- Kết quả hiện tại tô màu cam đậm, các kết quả khác màu vàng; nhảy tới kết quả sẽ cuộn trang vào giữa.
- Ctrl+F mở panel tìm và focus ô nhập (qua `Dispatcher.BeginInvoke(..., Input)` vì panel vừa hiện);
  Enter / F3 kết quả kế, Shift+Enter / Shift+F3 kết quả trước.

## Lưu ý / giới hạn
- Không tìm được trong PDF scan (không có lớp chữ).
- Cụm từ bị ngắt dòng có thể không khớp nếu PDF không có ký tự xuống dòng / khoảng trắng giữa hai dòng.

## Cách test
- Tự động: `ReadingTests.TextSearch_Options`, `ReadingTests.TextSearch_MapsBackToOriginalIndices`.
- Thủ công: Ctrl+F, gõ không dấu "tieng viet" → ra "Tiếng Việt"; bật Cả từ / Phân biệt hoa thường; F3 / Shift+F3.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành tìm nền có hủy, bỏ dấu với bản đồ chỉ số, F3 / Shift+F3; sửa focus ô tìm qua Dispatcher | Ô tìm không nhận focus khi panel vừa mở |
