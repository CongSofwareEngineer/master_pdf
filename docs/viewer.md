# Xem PDF

## Mục đích
Hiển thị trang rõ nét, cuộn liên tục nhiều trang (như Acrobat / Edge), chế độ một trang / hai trang, zoom, vừa trang /
vừa chiều rộng, thumbnail tất cả các trang, chế độ đọc ban đêm.

## File / module liên quan
- `Views/DocumentView.cs` — ScrollViewer + Canvas host; bố cục trang, ảo hóa `PageView`, hàng đợi render nền,
  zoom, chuyển trang, chuyển chuột / phím tới công cụ đang dùng (`HitTestHost`, `ToPageView`).
- `Views/PageView.cs` — một trang: ảnh render + viền mảnh + `HighlightLayer` / `FormLayer` / `ToolLayer`.
- `Views/ThumbnailsPanel.xaml(.cs)`, `Views/ThumbnailLoader.cs`, `Views/ThumbnailItem.cs` — thumbnail lười.
- `Views/BitmapHelper.cs` — `RenderedPage` → `BitmapSource` (Freeze, dùng được từ luồng nền).
- `Views/DocumentWorkspace.xaml(.cs)` — thanh zoom nổi, ô số trang, đồng bộ trang hiện tại với thumbnail / trạng thái.
- `Services/PdfService.cs` — `GetPageInfo`, `GetPageSizes`, `RenderPage`, `RenderCore`.

## Logic chính
- **Bố cục** (`ViewLayout`): Cuộn liên tục (mặc định), Một trang, Hai trang (cặp 1-2, 3-4…). Lề 20 DIP, khoảng
  cách trang 14 DIP. Trang hẹp hơn khung nhìn được căn giữa; rộng hơn thì sát lề trái và cuộn ngang (`CenterX`).
- **Ảo hóa**: chỉ tạo `PageView` cho trang nằm trong khung nhìn ± 1 màn hình; trang ra xa bị gỡ (giải phóng ảnh).
- **Render nét**: pixel/point = zoom × 96/72 × `PixelsPerDip`. Hàng đợi render chạy nền (`Task.Run`), ưu tiên trang
  gần trang hiện tại; ảnh cũ được giữ (co giãn) tới khi ảnh mới xong nên zoom không nháy trắng. Mỗi trang có số
  phiên bản: sửa trang → `InvalidatePage` tăng phiên bản → render lại. Ảnh > 40 triệu pixel tự giảm độ phân giải.
- **Zoom**: 10%–800%, bậc `ZoomSteps` (10, 25, 33, 50, 67, 75, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500,
  600, 800%). Ctrl + lăn chuột zoom quanh con trỏ (giữ điểm dưới con trỏ). Vừa chiều rộng (mặc định khi mở) dùng
  chiều rộng **trang hiện tại**; Vừa trang / Vừa chiều rộng tự tính lại khi đổi kích thước cửa sổ.
- **Trang hiện tại** = trang chiếm nhiều diện tích nhất trong khung nhìn (`CurrentPageChanged`) → cập nhật ô số
  trang, thumbnail đang chọn, trạng thái.
- **Chuyển trang**: thanh nổi, ô số trang (Enter), Ctrl+PageUp/PageDown, Ctrl+Home/End, bấm thumbnail / mục lục /
  kết quả tìm / link nội bộ (`GoToPage`, `ScrollIntoView`).
- **Đọc ban đêm**: đảo độ sáng ảnh render nhưng giữ sắc (`Invert`), chỉ trên màn hình (export / in không đổi).
- **Thumbnail**: `ThumbnailLoader` — item hiện ra (`Loaded`) mới xếp hàng render, item mới nhất ưu tiên, một vòng
  render nền; dùng chung cho panel Trang và màn hình Sắp xếp trang. Sửa trang → render lại thumbnail trang đó.
- **DPI**: chỉ xử lý `DpiChanged` khi chính cửa sổ đổi DPI (sự kiện nổi bọt từ visual con → tránh vòng lặp).

## Lưu ý / giới hạn
- Không dùng DropShadowEffect cho trang (làm cuộn / zoom giật) — dùng viền 1 px.
- Zoom rất lớn với trang khổ to bị giới hạn 40 MP nên hơi mờ.

## Cách test
- Tự động: `RenderPage_ReturnsBitmapOfExpectedSize`, `GetPageInfo_ViewToPageIsInverseOfPageToView`,
  `RotatePage_PersistsAndSwapsDisplaySize`.
- Thủ công: mở PDF 100+ trang, cuộn nhanh (không giật, RAM ổn định), Ctrl + lăn chuột, đổi Một trang / Hai trang,
  file có trang ngang lẫn dọc (căn giữa đúng), đọc ban đêm, kéo cửa sổ sang màn hình DPI khác.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo viewer, zoom, thumbnail lười | Khởi tạo dự án |
| 2026-10-03 | `OnDpiChanged` chỉ xử lý khi chính cửa sổ đổi DPI | Màn hình 125%: DpiChanged nổi bọt từ thumbnail mới → dựng lại thumbnail → vòng lặp vô hạn, CPU 100%, trang không hiện |
| 2026-10-03 | Thay `PdfViewer` (một trang) bằng `DocumentView` cuộn liên tục ảo hóa + `PageView` nhiều lớp; chế độ một / hai trang, đọc ban đêm, zoom quanh con trỏ | Trải nghiệm đọc như Acrobat / Edge |
| 2026-10-03 | Vừa chiều rộng theo trang hiện tại; căn giữa theo từng trang (`CenterX`); viền trang thay bóng đổ | Trang ngang làm trang dọc lệch trái; mép trang khó thấy trên nền sáng |
