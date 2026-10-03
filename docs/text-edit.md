# Chỉnh sửa văn bản

## Mục đích
Sửa text có sẵn, thêm text mới, xóa / di chuyển text; định dạng font, cỡ chữ, đậm, nghiêng, màu
(yêu cầu "Chỉnh sửa Văn bản" + "Module Chỉnh sửa" trong `design.md`).

## File / module liên quan
- `Services/PdfService.cs` — `GetTextObjects`, `AddText`, `UpdateTextObject`, `DeleteTextObject`,
  `MoveTextObject`, `EditTextObject` → `EditObject`, `TryFixFontNames`, `RewriteText`, `AddCover`, `VerifyPage`,
  `HasAmbiguousFonts`.
- `Services/FontNameFixer.cs` — đổi tên font nhúng trùng tên bằng PDFsharp (sửa tận gốc lỗi gộp font của PDFium).
- `Services/FontService.cs` — chọn font chuẩn PDF hoặc file TrueType của Windows; đoán family từ tên font PDF.
- `Views/Tools/ContentTools.cs` — `EditContentTool` (chọn / kéo / nhấp đúp sửa, chung với ảnh & hình — xem
  page-objects.md), `AddTextTool`, `InlineTextEditor` (ô sửa trực tiếp trên trang).
- `Views/Panels/PropertiesPanel.xaml(.cs)` — nội dung + định dạng (font, cỡ, đậm, nghiêng, màu) của chữ đang chọn / chữ mới.
- `Views/DocumentWorkspace(.Commands).cs` — `EditAsync`, nối công cụ / panel với `PdfService`.

## Logic chính
- **Đơn vị chọn** = một text object của PDFium (thường là một dòng / cụm chữ). `GetTextObjects` trả về
  chỉ số object, nội dung, khung (trang + view), style nhận diện được (font gần nhất có trên máy, cỡ chữ
  thực = Tf × độ co giãn của ma trận, đậm/nghiêng từ tên font + weight + italic angle, màu tô).
- **Công cụ** (toolbar / menu Sửa):
  - *Sửa nội dung* (Alt+2, tab Chỉnh sửa): click = chọn (panel hiện nội dung + định dạng); nhấp đúp = sửa trực tiếp trên
    trang (Enter = xong, Shift+Enter = xuống dòng, Esc = hủy); kéo = di chuyển; Delete = xóa.
  - *Thêm chữ* (Ctrl+T): chọn định dạng ở panel, click lên trang → gõ → Enter. Xong tự về công cụ Sửa nội dung
    và chọn sẵn text vừa thêm. Định dạng text mới được nhớ trong settings.
- **Thêm text** (`AddText`): điểm click là góc trên-trái; baseline = điểm click + 0.8 × cỡ chữ. Hướng chữ lấy
  từ ma trận view → trang nên chữ luôn nằm ngang trên màn hình kể cả trang đã xoay. Nhiều dòng = mỗi dòng
  một object, cách nhau 1.25 × cỡ chữ.
- **Chọn font** (`GetFont`, cache theo tài liệu):
  - Text chỉ có ký tự Latin-1 và family có bản chuẩn PDF (Arial→Helvetica, Times New Roman→Times,
    Courier New→Courier) → font chuẩn, không nhúng (file nhỏ).
  - Còn lại (vd. tiếng Việt) → nhúng file `.ttf` của Windows dạng font CID (Unicode). Mỗi font ~0.5–1 MB.
  - Khi sửa mà KHÔNG đổi font/đậm/nghiêng và mọi ký tự mới đã có trong text cũ → giữ font gốc (giữ đúng
    dáng chữ). Sau khi tạo, đọc lại text; nếu font gốc không mã hóa được thì dùng font thay thế.
- **Sửa / xóa / di chuyển** đi qua `EditTextObject`:
  1. Snapshot (cho undo). 2. Nếu trang có font trùng tên (xem dưới) và object không do app tạo → thử
  `FontNameFixer` (đổi tên font trùng thành `XXXXXX+Tên`, nạp lại tài liệu; bỏ qua với file có mật khẩu hoặc đã
  thất bại một lần). Hết trùng → sửa trực tiếp; vẫn trùng → **chế độ phủ**. 3. Ngược lại sửa trực tiếp (gỡ object cũ / `FPDFPageObj_Transform`) + `FPDFPage_GenerateContent`.
  4. `VerifyPage`: load lại trang từ nội dung mới, so toàn bộ text với object trong bộ nhớ. Khác → khôi phục
  snapshot rồi làm lại bằng chế độ phủ. Vẫn lỗi → `PdfErrorKind.Regeneration`.
- **Chế độ phủ** (lý do): PDFium tái tạo CẢ content stream chứa object bị sửa và gộp các font khác nhau nhưng
  trùng tên BaseFont + loại (lỗi của PDFium, hay gặp ở PDF do Chrome/Skia "Print to PDF" tạo) → mất chữ ở chỗ
  khác trên trang. Chế độ phủ không đụng tới stream gốc: vẽ hình chữ nhật (mark `PDFEditorCover`) tô màu nền
  lấy mẫu quanh khung chữ (render trang, lấy màu phổ biến nhất trên viền), rồi thêm text mới phía trên bằng
  font của app. Object mới nằm trong stream mới nên stream gốc giữ nguyên. `GetTextObjects` ẩn text bị lớp che
  vẽ sau phủ ≥ 80%.
- Text do app tạo có mark `PDFEditorText` (object khác: `PDFEditorObject`) → luôn sửa trực tiếp được.
- `MoveTextObject` trả về chỉ số object mới (ở chế độ phủ object cũ được thay bằng bản sao).

## Lưu ý / giới hạn
- Text nằm trong Form XObject không chọn được (PDFium chỉ liệt kê object cấp trang).
- Granularity phụ thuộc file: có PDF tách một dòng thành nhiều mảnh (vd. "TRÙNG P" + "ARACETAMOL") → sửa
  từng mảnh.
- Chế độ phủ: text gốc vẫn còn trong file bên dưới lớp che (tìm kiếm / copy trong app khác vẫn thấy); lớp
  che màu đặc nên nếu nền là ảnh / gradient sẽ thấy ô màu.
- Text gốc ở chế độ "ẩn" (OCR của PDF scan) khi sửa sẽ thành chữ hiển thị (render mode = fill).
- Font không có trên máy → dùng font gần nhất (Arial mặc định). Tahoma không có bản nghiêng.

## Cách test
- Tự động (`dotnet test`, `PdfServiceTests`): thêm text (vị trí, nhiều dòng, tiếng Việt, trang xoay), sửa
  text + style, text rỗng = xóa, xóa, di chuyển, index sai không làm hỏng tài liệu,
  `Edit_OnPageWithAmbiguousFonts_KeepsOtherTextIntact`, `DeleteAndMove_OnPageWithAmbiguousFonts_HideOriginal`.
- Thủ công: mở PDF in từ trình duyệt (Chrome "Save as PDF"), sửa / xóa một dòng → lưu → mở lại, kiểm tra các
  chữ khác trên trang không bị mất; thử nhấp đúp sửa trực tiếp, kéo di chuyển, Ctrl+T thêm chữ tiếng Việt.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo chức năng sửa / thêm / xóa / di chuyển text | Khởi tạo dự án |
| 2026-10-03 | Thêm kiểm tra tái tạo (`VerifyPage`) + chế độ phủ; `MoveTextObject` trả về index mới | Test với PDF thật (do Chrome tạo): PDFium gộp nhầm font trùng tên → mất chữ cả trang |
| 2026-10-03 | Thêm `FontNameFixer` (PDFsharp) chạy trước chế độ phủ; công cụ chuyển sang `EditContentTool` / `AddTextTool` + panel thuộc tính mới | Chế độ phủ để lại chữ gốc trong file; đổi tên font cho phép xóa / sửa thật. UI v2 |
