# Chỉnh sửa văn bản

## Mục đích
Sửa text có sẵn, thêm text mới, xóa / di chuyển text; định dạng font, cỡ chữ, đậm, nghiêng, màu
(yêu cầu "Chỉnh sửa Văn bản" + "Module Chỉnh sửa" trong `design.md`).

## File / module liên quan
- `Services/PdfService.cs` — `GetTextObjects`, `AddText`, `UpdateTextObject`, `DeleteTextObject`,
  `MoveTextObject`, `EditTextObject` → `EditObject` → `EditObjects`, `TryFixFontNames`, `RewriteText`, `AddCover`,
  `VerifyPage`, `HasAmbiguousFonts`.
- `Services/PdfService.TextBlocks.cs` — `GetTextBlocks`, `UpdateTextBlock`, `DeleteTextBlock`, `MoveTextBlock`
  (sửa theo khối chữ — cái UI dùng), `RewriteBlock`, `LayoutBlock` (tự xuống dòng), `MeasureText`.
- `Services/TextBlockBuilder.cs` — gộp text object thành khối chữ (dòng → đoạn văn), thuần C#, không gọi PDFium.
- `Models/PdfModels.cs` — `TextBlockInfo` (khối chữ), `TextObjectInfo` (một object, còn dùng cho test / API cũ).
- `Services/FontNameFixer.cs` — đổi tên font nhúng trùng tên bằng PDFsharp (sửa tận gốc lỗi gộp font của PDFium).
- `Services/FontService.cs` — chọn font chuẩn PDF hoặc file TrueType của Windows; đoán family từ tên font PDF.
- `Views/Tools/ContentTools.cs` — `EditContentTool` (chọn / kéo / nhấp đúp sửa, chung với ảnh & hình — xem
  page-objects.md; `FindTarget` ưu tiên khối chữ), `AddTextTool`, `InlineTextEditor` (ô sửa trực tiếp trên trang;
  `OpenBlock` cho khối chữ có sẵn, kèm tay nắm kéo đổi bề rộng ô).
- `Views/Panels/PropertiesPanel.xaml(.cs)` — nội dung + định dạng (font, cỡ, đậm, nghiêng, màu) của chữ đang chọn / chữ mới.
- `Views/DocumentWorkspace(.Commands).cs` — `EditAsync`, nối công cụ / panel với `PdfService`.

## Logic chính
- **Đơn vị chọn / sửa = khối chữ** (`TextBlockInfo`), không phải từng text object. Lý do: nhiều PDF tách một
  dòng thành nhiều object (mỗi từ / cụm một object, dấu cách chỉ là khoảng trống giữa các object) → sửa theo
  object thì ô sửa chỉ bằng một từ và mất dấu cách. Mỗi khối chỉ có **một kiểu chữ**: đoạn đậm, đoạn thường,
  đoạn nghiêng / khác màu / khác cỡ trên cùng một dòng là các khối riêng (khoanh đúng vùng sửa, sửa không làm
  mất định dạng của phần bên cạnh). Chữ, ảnh, hình là các loại riêng (xem page-objects.md).
- `GetTextObjects` (từng object): chỉ số, nội dung, khung (trang + view), style nhận diện được (font gần nhất
  có trên máy, cỡ chữ thực = Tf × độ co giãn của ma trận, đậm/nghiêng từ tên font + weight + italic angle, màu tô).
- **Gộp khối** (`GetTextBlocks` → `TextBlockBuilder.Build`, mọi ngưỡng tính theo cỡ chữ = em, hệ view):
  1. *Hàng*: object chữ nằm ngang có baseline lệch ≤ 0.3 em và cỡ chữ chênh ≤ 1.5 lần.
  2. *Dòng*: trong hàng, sắp theo X; **tách** khi khoảng trống > 1.5 em (cột, ô bảng, tab; văn bản căn đều có
     thể cách từ ~1 em nên không lấy 1 em), khi có ảnh / đường kẻ nằm giữa 2 mảnh (cao ≥ 50% dòng; nền /
     highlight bao trùm mảnh không tính), hoặc khi **đổi kiểu chữ** tại ranh giới từ (`SameLook` so với mảnh đầu của đoạn đang gộp: khác đậm /
     nghiêng, cỡ chênh > 1.15 lần, màu lệch > 24/kênh; KHÔNG so tên font vì PDF hay dùng font dự phòng khác tên
     cho vài ký tự như dấu tiếng Việt). Ranh giới từ = khoảng trống > 0.15 em hoặc có dấu cách ở chỗ nối; mảnh
     khác kiểu dính liền (giữa một từ) không tách. Đoạn bị tách vì đổi kiểu được bỏ dấu cách cuối, và đánh dấu
     `JoinedLeft` / `JoinedRight`. Khi nối: khoảng trống > 0.15 em mà hai bên chưa có dấu cách → **chèn dấu cách**.
  3. *Đoạn văn*: dòng dưới nối vào đoạn khi: khoảng cách baseline 0.9–1.8 em và đều với các dòng trước (±0.2 em),
     cùng font / đậm / nghiêng / màu, cỡ chữ chênh ≤ 1.2 lần, chồng nhau theo chiều ngang ≥ 50% dòng ngắn hơn,
     thẳng lề trái / phải / giữa (≤ 3 em), không có đường kẻ / ảnh xen giữa, và không nối qua chỗ bị tách vì
     đổi kiểu (dòng trên `JoinedRight` hoặc dòng dưới `JoinedLeft`). Hai cột cạnh nhau không bị gộp.
  4. *Ngắt mềm / cứng* giữa 2 dòng của đoạn: mềm (tự xuống dòng → nối bằng dấu cách, bỏ dấu cách nếu dòng trên
     kết thúc bằng '-') khi từ đầu dòng sau (ước lượng 0.5 em/ký tự) không vừa chỗ trống cuối dòng trên; cứng
     ('\n') khi dòng trên kết thúc bằng `. : ; ! ?` hoặc dòng sau là mục danh sách (`•`, `-`, `1.`, `1)`…).
  5. Chữ xoay / dọc: mỗi object một khối như cũ.
  Khối lưu: chỉ số object theo thứ tự đọc, nội dung, khung, style chiếm đa số, số dòng, `LineHeight` (khoảng cách
  baseline đo được), `WrapWidth` (> 0 nếu có ngắt mềm = đoạn văn tự xuống dòng), `Indent` (thụt dòng đầu).
- **Sửa khối** (`UpdateTextBlock` → `RewriteBlock`, qua `EditObjects` nên có đủ FontNameFixer / chế độ phủ /
  `VerifyPage` như sửa một object): gỡ (hoặc phủ) mọi object của khối, rồi ghi lại từ gốc chữ của mảnh đầu,
  cùng hướng; dòng sau lùi `LineHeight` (đổi cỡ chữ thì co giãn theo), lề các dòng sau = lề gốc của dòng 2.
  `WrapWidth` > 0 → tự xuống dòng theo từ (`LayoutBlock`, đo bằng `MeasureText` với font sẽ dùng, cho lệch 2%);
  `UpdateTextBlock(..., wrapWidth)` với wrapWidth > 0 → dùng bề rộng đó thay cho `block.WrapWidth` (người dùng
  kéo rộng / hẹp ô sửa), kể cả khối một dòng → khối đó bắt đầu tự xuống dòng;
  '\n' luôn là xuống dòng cứng. Font gốc được giữ nếu không đổi font/kiểu và mọi ký tự mới có trong các mảnh
  dùng font đó. `MoveTextBlock` / `DeleteTextBlock` áp dụng cho mọi object của khối.
- **Ô sửa trực tiếp** (`InlineTextEditor.OpenBlock`): đặt ở góc trên-trái khối, rộng ít nhất bằng khối, cỡ chữ
  theo zoom; khối là đoạn văn thì ô rộng cố định = bề rộng khối và tự xuống dòng (gần giống kết quả ghi vào PDF),
  khoảng cách dòng theo `LineHeight`. Ô là `TextBox` nhiều dòng (`AcceptsReturn`): **Shift+Enter = xuống dòng
  cứng**, Enter = xong, Esc = hủy, mũi tên / Delete chạy như ô nhập bình thường. Để các phím này tới được ô,
  `DocumentView.PreviewKeyDown` **không** chuyển phím cho công cụ khi phím phát ra từ ô nhập trên trang
  (xem viewer.md) — nếu không, `EditContentTool.OnKeyDown` bắt Enter / mũi tên / Delete trước (tunneling đi từ
  ngoài vào). Khi ghi vào PDF, `SplitLines` chuẩn hóa `\r\n` của TextBox về `\n` = xuống dòng cứng.
- **Kéo rộng ô đang sửa** (`InlineTextEditor.CreateGrip` / `SetWidth`): mép phải ô có tay nắm (thanh dọc màu
  nhấn, con trỏ ↔, đặt trên `ToolLayer` cạnh ô, bám theo ô qua `SizeChanged`). Kéo ngang → đổi `TextBox.Width`
  (tối thiểu ~37 DIP; `MinWidth` hạ về 0 để kéo hẹp được) và bật `TextWrapping.Wrap` → thấy ngay chữ chảy lại
  theo bề rộng mới. Bề rộng mới (hệ view = (Width − 10 DIP padding/viền) / zoom) được trả về cùng text khi
  Enter / click ra ngoài, rồi truyền vào `UpdateTextBlock(..., wrapWidth)` nên PDF ghi lại đúng bề rộng đó.
  Trong lúc kéo, ô có thể mất focus bàn phím → cờ `_resizing` chặn không cho đóng ô; nhả chuột thì focus về ô.
  Tay nắm chỉ có khi sửa khối có sẵn (`OpenBlock`), không có ở ô "Thêm chữ" (`AddText` chưa tự xuống dòng).
- **Công cụ** (toolbar / menu Sửa):
  - *Sửa nội dung* (Alt+2, tab Chỉnh sửa): click = chọn (panel hiện nội dung + định dạng); nhấp đúp = sửa trực tiếp trên
    trang (Enter = xong, Shift+Enter = xuống dòng, Esc = hủy; kéo tay nắm mép phải ô = đổi bề rộng khối chữ);
    kéo = di chuyển; Delete = xóa.
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
- Gộp khối là heuristic theo hình học: bảng không có đường kẻ mà cột sát nhau (< 1 em) vẫn bị gộp; vài dòng
  ngắn liền nhau (địa chỉ) có thể bị coi là ngắt mềm nếu dòng trên dài nhất → khi sửa sẽ chảy lại thành đoạn
  (ô sửa tự xuống dòng giống kết quả nên người dùng thấy trước; Shift+Enter để xuống dòng cứng).
- Khối luôn một kiểu chữ nên sửa không làm mất định dạng phần khác; đổi lại, đoạn văn có chữ đậm / màu xen giữa
  bị chia thành nhiều khối (phần trước, chữ đậm, phần sau). Sửa một khối nằm giữa dòng thành chữ dài hơn thì
  có thể đè lên khối bên phải (không tự đẩy phần còn lại của dòng). Đoạn văn chảy lại sẽ gộp nhiều dấu cách liền nhau thành một; căn đều (justify) không giữ.
- Đoạn văn ghi lại luôn căn trái theo lề dòng 2 (căn giữa / phải không giữ).
- Kéo rộng ô sửa chỉ đổi bề rộng tự xuống dòng (chữ chảy lại), **không** co giãn cỡ chữ và không đẩy nội dung
  khác trên trang → kéo rộng quá có thể đè lên khối bên phải. Bề rộng ô trên màn hình đo bằng font của Windows
  nên có thể lệch nhẹ so với dòng thật trong PDF (cho lệch 2% trong `LayoutBlock`). Kéo hẹp hơn một từ dài thì
  từ đó vẫn tràn ra ngoài (không cắt giữa từ).
- Chế độ phủ: text gốc vẫn còn trong file bên dưới lớp che (tìm kiếm / copy trong app khác vẫn thấy); lớp
  che màu đặc nên nếu nền là ảnh / gradient sẽ thấy ô màu.
- Text gốc ở chế độ "ẩn" (OCR của PDF scan) khi sửa sẽ thành chữ hiển thị (render mode = fill).
- Font không có trên máy → dùng font gần nhất (Arial mặc định). Tahoma không có bản nghiêng.

## Cách test
- Tự động (`dotnet test`, `PdfServiceTests`): thêm text (vị trí, nhiều dòng, tiếng Việt, trang xoay), sửa
  text + style, text rỗng = xóa, xóa, di chuyển, index sai không làm hỏng tài liệu,
  `Edit_OnPageWithAmbiguousFonts_KeepsOtherTextIntact`, `DeleteAndMove_OnPageWithAmbiguousFonts_HideOriginal`.
- `TextBlockTests`: `Builder_*` (thuần: nối từ + chèn dấu cách, không nhân đôi dấu cách, tách cột / vật cản,
  nền không tách, đoạn văn ngắt mềm, ngắt cứng `:` / danh sách, khác cỡ / kiểu / khoảng cách lớn / 2 cột / đường
  kẻ → tách; đậm + thường cùng dòng → 2 khối, các mảnh cùng kiểu quanh chữ đậm vẫn gộp, đổi kiểu giữa từ không
  tách, khoảng cách căn đều 1.2 em vẫn một dòng, đoạn văn không nối qua chữ đậm); PDFium: `GetTextBlocks_*`, `UpdateTextBlock_ReplacesAllPiecesOfLine`,
  `UpdateTextBlock_Paragraph_RewrapsWithinOriginalWidth`, `UpdateTextBlock_NewWrapWidth_RewrapsToNewWidth`,
  `UpdateTextBlock_SingleLine_NewWrapWidth_StartsWrapping`, `DeleteAndMoveTextBlock_AffectWholeBlock`,
  `UpdateTextBlock_DuplicateIndices_Rejected`.
- Thủ công: mở PDF in từ trình duyệt (Chrome "Save as PDF"), sửa / xóa một dòng → lưu → mở lại, kiểm tra các
  chữ khác trên trang không bị mất; thử nhấp đúp sửa trực tiếp, kéo di chuyển, Ctrl+T thêm chữ tiếng Việt.
- Thủ công (khối chữ): mở PDF có đoạn văn nhiều dòng (Word → PDF) → di chuột: khung chọn bao cả đoạn; nhấp đúp
  → một ô sửa bao cả đoạn, có dấu cách giữa các từ, tự xuống dòng; thêm vài câu → Enter → đoạn chảy lại đúng
  bề rộng cũ. Bảng có kẻ ô / cột cách xa → mỗi ô một khối. Tiêu đề đậm phía trên đoạn → khối riêng.
  Dòng "Nhãn **đậm**: giá trị thường" → di chuột lên phần đậm / phần thường thấy 2 khung riêng; dòng chữ thường
  cùng kiểu (kể cả căn đều) → một khung bao cả dòng.
- Thủ công (kéo rộng ô sửa): nhấp đúp một dòng / đoạn → kéo tay nắm ở mép phải ô sang phải: ô rộng ra, chữ chảy
  lại trong ô; Enter → trong PDF đoạn đó xuống dòng theo bề rộng mới. Kéo sang trái (hẹp hơn) → chữ xuống dòng
  sớm hơn. Kéo xong gõ tiếp vẫn được (ô không bị đóng). Esc sau khi kéo → không đổi gì. Thử ở zoom 50% và 200%
  (bề rộng ghi vào PDF phải như nhau).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo chức năng sửa / thêm / xóa / di chuyển text | Khởi tạo dự án |
| 2026-10-03 | Thêm kiểm tra tái tạo (`VerifyPage`) + chế độ phủ; `MoveTextObject` trả về index mới | Test với PDF thật (do Chrome tạo): PDFium gộp nhầm font trùng tên → mất chữ cả trang |
| 2026-10-03 | Thêm `FontNameFixer` (PDFsharp) chạy trước chế độ phủ; công cụ chuyển sang `EditContentTool` / `AddTextTool` + panel thuộc tính mới | Chế độ phủ để lại chữ gốc trong file; đổi tên font cho phép xóa / sửa thật. UI v2 |
| 2026-10-07 | Sửa theo **khối chữ** (`TextBlockBuilder`, `GetTextBlocks`, `Update/Delete/MoveTextBlock`): gộp object cùng dòng (tự chèn dấu cách), gộp dòng liền mạch thành đoạn văn, chỉ tách khi khoảng trống > 1 em hoặc có ảnh / đường kẻ chen giữa; ô sửa bao cả khối + tự xuống dòng; ghi lại đoạn văn tự xuống dòng theo bề rộng cũ | Một dòng bị tách nhiều object → ô sửa quá nhỏ, mất dấu cách, đoạn văn phải sửa từng dòng |
| 2026-10-07 | Tách khối theo kiểu chữ trong cùng dòng (đậm / nghiêng / màu / cỡ, tại ranh giới từ, không so tên font); đoạn văn không nối qua chỗ đổi kiểu; ngưỡng tách từ 1 → 1.5 em | Người dùng báo: trỏ vào dòng để sửa chỉ nhận 1–2 mảnh chữ; cần khoanh vùng sửa theo từng đoạn cùng kiểu (đậm riêng, thường riêng) |
| 2026-10-07 | Ô sửa trực tiếp nhận lại Shift+Enter / mũi tên / Delete: `DocumentView` bỏ qua phím phát ra từ ô nhập trên trang | Người dùng báo: sửa text chỉ gõ được chữ, Shift+Enter không xuống dòng (công cụ Sửa nội dung bắt Enter trước → đóng rồi mở lại ô sửa) |
| 2026-10-07 | Tay nắm kéo ngang ở mép phải ô sửa trực tiếp (`InlineTextEditor.CreateGrip` / `SetWidth`); `UpdateTextBlock` nhận tham số `wrapWidth` để ghi lại theo bề rộng mới (kể cả khối một dòng) | Người dùng báo: sửa được chữ rồi nhưng muốn kéo dài ô đang sửa theo chiều ngang (bề rộng cũ của khối bó hẹp chỗ gõ và chỗ chữ chảy lại) |
