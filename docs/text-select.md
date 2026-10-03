# Chọn & sao chép văn bản, liên kết

## Mục đích
Bôi đen chữ trên trang để sao chép (Ctrl+C) hoặc tô sáng / gạch chân / gạch ngang; bấm vào liên kết để mở.

## File / module liên quan
- `Services/PdfService.Reading.cs` — `GetTextLayout` (ký tự + khung trong hệ view), `GetLinks`.
- `Models/ReadingModels.cs` — `PageTextLayout` (`HitTest`, `GetRangeRects`, `GetRangeText`, `WordAt`), `LinkInfo`.
- `Views/Tools/SelectionTools.cs` — `SelectTool` (kéo chọn chữ, nhấp đúp chọn từ, bấm liên kết), `HandTool`.
- `Views/DocumentWorkspace.xaml.cs` — cache layout / link theo trang, `OpenLink`.
- `Views/DocumentWorkspace.Commands.cs` — Copy, áp dụng markup cho vùng chọn.
- `Views/Panels/PropertiesPanel` — nút Sao chép / Tô sáng / Gạch chân / Gạch ngang khi có vùng chữ được chọn.

## Logic chính
- Khi trang hiện lên, workspace nạp (nền) `GetTextLayout` + `GetLinks` một lần và cache; chọn / tô sáng chạy bằng
  code quản lý trên layout này nên không khóa PDFium khi rê chuột.
- `SelectTool`: nhấn trên chữ → kéo để chọn (`HitTest(nearest)` → chỉ số ký tự đầu / cuối), vẽ khung vào
  `HighlightLayer`; nhấp đúp chọn cả từ (`WordAt`). Chuột trên chữ hiện con trỏ I-beam, trên liên kết hiện bàn tay.
- Liên kết: `FPDFLink_Enumerate` → đích nội bộ (nhảy trang) hoặc URI (mở trình duyệt qua `SystemService.OpenUrl`).
  Bấm-thả không kéo mới mở link (kéo thì vẫn là chọn chữ).
- Ctrl+C sao chép `GetRangeText` (ghép dòng theo thứ tự ký tự của PDFium). Ctrl+Shift+H tô sáng vùng chọn.

## Lưu ý / giới hạn
- Chọn trong phạm vi một trang.
- PDF scan (ảnh) không có chữ để chọn. Trang đã che thông tin (redact) cũng thành ảnh.

## Cách test
- Tự động: `ReadingTests.TextLayout_HitTestRectsAndWords`, `ReadingTests.Bookmarks_And_Links`.
- Thủ công: bôi đen, Ctrl+C, dán vào Notepad (tiếng Việt đúng dấu); nhấp đúp một từ; bấm link nội bộ và link web.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành chọn chữ / sao chép / link, cache layout theo trang, nút markup trong panel thuộc tính | Tính năng đọc cơ bản như Acrobat / Edge |
