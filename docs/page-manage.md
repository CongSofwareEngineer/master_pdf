# Quản lý trang

## Mục đích
Như "Organize Pages" của Acrobat: xoay, xóa, thêm trang trắng, nhân bản, di chuyển / kéo thả sắp xếp, chèn trang
từ PDF khác, trích xuất trang ra file mới, tách tài liệu — trên một hoặc nhiều trang cùng lúc.

## File / module liên quan
- `Services/PdfService.Pages.cs` — `RotatePages`, `DeletePages`, `MovePages`, `DuplicatePages`, `ExtractPages`.
- `Services/PdfService.cs` — `InsertBlankPage`, `ImportPages` (+ bản một trang `RotatePage`, `DeletePage`, `MovePage`).
- `Views/Panels/OrganizeView.xaml(.cs)` — màn hình Sắp xếp trang: lưới thumbnail lớn, chọn nhiều (Ctrl / Shift),
  kéo thả để đổi thứ tự, thanh công cụ (xoay, nhân bản, trích xuất, xóa, thêm trắng, chèn file, Xong).
- `Views/Panels/ThumbnailsPanel.xaml(.cs)` — menu chuột phải trên thumbnail.
- `Views/DocumentWorkspace.Commands.cs` — `RotatePagesAsync`, `DeletePagesAsync`, `DuplicatePagesAsync`,
  `ExtractPagesAsync`, `SplitAsync`, chèn trang; `MainWindow.xaml` — tab ribbon "Trang".

## Logic chính
- Mọi thao tác chạy trong `Mutate` (snapshot → undo được; lỗi → khôi phục) và đóng trang đang hoạt động.
- **Xoay** (`FPDFPage_SetRotation`): cộng dồn bước 90°; phải = +1 (Ctrl+R), trái = +3 (Ctrl+Shift+R).
- **Xóa** (Ctrl+Delete): hỏi xác nhận; không cho xóa hết trang (`PdfErrorKind.LastPage`). Xóa nhiều trang theo thứ tự
  giảm dần để chỉ số không lệch.
- **Thêm trang trắng** (Ctrl+Shift+Enter): sau trang hiện tại, cùng kích thước hiển thị trang hiện tại.
- **Nhân bản** (Ctrl+Shift+D): bản sao chèn ngay sau trang gốc (`FPDF_ImportPages` từ chính tài liệu).
- **Di chuyển**: nút lên / xuống (một vị trí) hoặc kéo thả trong Sắp xếp trang (`FPDF_MovePages` tới vị trí thả).
- **Chèn từ PDF khác** (Ctrl+Shift+I): toàn bộ trang chèn sau trang hiện tại; hỗ trợ file có mật khẩu.
- **Trích xuất**: trang đã chọn → tài liệu mới (`FPDF_ImportPages` vào doc trống) → Lưu thành `<tên>_<hậu tố>.pdf`.
- **Tách**: nhập số trang mỗi phần → chọn thư mục → `<tên>_01-03.pdf`, `<tên>_04-06.pdf`… (số đệm 0, ghi nguyên tử).
- Sau thao tác: cập nhật kích thước trang cho `DocumentView`, dựng lại thumbnail / Sắp xếp trang (giữ vùng chọn),
  nạp lại mục lục.

## Lưu ý / giới hạn
- Trích xuất / tách không mang theo bookmark của file gốc.
- Kéo thả chỉ trong màn hình Sắp xếp trang (panel thumbnail dùng menu chuột phải / nút lên xuống).

## Cách test
- Tự động: `DocumentFeatureTests.BatchPageOperations`, `RotatePage_PersistsAndSwapsDisplaySize`,
  `DeletePage_RemovesPage_LastPageCannotBeDeleted`, `InsertBlankPage_InsertsAtIndexWithSize`, `MovePage_ReordersPages`,
  `ImportPages_InsertsAllPagesOfOtherPdf`.
- Thủ công: Trang → Sắp xếp trang, chọn 3 trang (Ctrl+click), xoay, kéo thả đổi chỗ, nhân bản, trích xuất, Ctrl+Z;
  Tách 2 trang / phần.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo chức năng xoay / xóa / thêm / di chuyển / chèn trang | Khởi tạo dự án |
| 2026-10-03 | Thao tác nhiều trang, nhân bản, trích xuất, tách; màn hình Sắp xếp trang kéo thả; thanh công cụ dùng WrapPanel | Tính năng Organize Pages như Acrobat; thanh công cụ bị chồng khi cửa sổ hẹp |
