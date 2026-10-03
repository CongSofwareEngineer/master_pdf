# Giao diện chính

## Mục đích
Bố cục hiện đại kiểu Acrobat / Microsoft 365 (Fluent, mặc định tối): thanh tiêu đề + dải tab tài liệu, ribbon theo
nhóm chức năng, vùng xem PDF ở giữa, panel trái (trang / mục lục / tìm / nhận xét), panel thuộc tính bên phải,
thanh trạng thái ở dưới.

## File / module liên quan
- `MainWindow.xaml(.cs)` — `ui:FluentWindow`: `TitleBar`, dải tab (xem tabs.md), ribbon, `ContentHost`,
  `SnackbarPresenter`, thanh trạng thái; CommandBinding cho toàn bộ `AppCommands`; `OnAction` (switch theo tên).
- `Views/AppCommands.cs` — toàn bộ lệnh + phím tắt; `Tool` (tham số = tên `ToolId`), `Action` (tham số = tên hành động).
- `Views/DocumentWorkspace.xaml(.cs)` — bố cục một tài liệu: thanh rail trái 4 nút, panel trái, `DocumentView` /
  `OrganizeView`, `InfoBar` (vd. form), thanh zoom nổi, lớp chờ, panel thuộc tính; `RunBusyAsync`, `SetStatus`, `Notify`.
- `Views/DocumentWorkspace.Commands.cs` — xử lý lệnh của tài liệu (lưu, export, trang, watermark, bảo mật…).
- `Views/Controls.cs` — control tự định nghĩa: `RibbonButton`, `RibbonToggle`, `IconButton`, `IconToggle`, `ColorSwatch`…
- `Views/Tools/*` — công cụ trên trang (xem text-select, text-edit, annotations, page-objects, sign, protect).
- `Views/UiHelpers.cs` — `Dialogs` (WPF-UI MessageBox đa ngôn ngữ: báo lỗi, xác nhận, hỏi lưu).
- `Resources/Styles.xaml` — style Fluent của app; `Resources/app.ico`; `app.manifest` (DPI PerMonitorV2, Win10/11, long path).
- `App.xaml(.cs)` — merge theme WPF-UI + Styles, bắt lỗi không lường trước (`DispatcherUnhandledException`).

## Logic chính
- Ribbon: nút "Tệp" (menu: Mới, Mở, Gần đây, Lưu, Lưu thành, Xuất, In, Kết hợp, Ảnh → PDF, Tách, Đóng, Cài đặt,
  Giới thiệu) + 7 tab: **Xem** (công cụ Chọn / Bàn tay, zoom, chế độ xem, đọc ban đêm, tìm, ẩn/hiện panel),
  **Chỉnh sửa** (Sửa nội dung, Thêm chữ, Chèn ảnh, Watermark, Đầu/chân trang), **Nhận xét** (11 công cụ + danh sách),
  **Trang** (Sắp xếp, xoay, thêm trắng, chèn file, nhân bản, xóa, di chuyển, trích xuất, tách), **Điền & Ký**,
  **Bảo vệ** (mật khẩu, che thông tin), **Chuyển đổi** (PNG, JPG, Word, TXT, In, Kết hợp, Ảnh → PDF).
  Bên phải ribbon: Lưu, Hoàn tác, Làm lại, In.
- Công cụ dạng toggle (`RibbonToggle` + `AppCommands.Tool`); công cụ đang dùng được tô; Esc về công cụ Chọn;
  thanh trạng thái hiện gợi ý của công cụ (`Hint_Tool_<ToolId>`, test `ToolHintKeysCoverEveryTool`).
- Lệnh là `RoutedUICommand`; `CanExecute` đọc trạng thái đã cache của tab đang mở (không khóa PDFium), tắt hết khi
  tab đang bận hoặc đang ở Trang chủ.
- Thao tác dài chạy qua `DocumentWorkspace.RunBusyAsync`: khóa lệnh, hiện lớp chờ (ProgressRing) sau ~200 ms,
  bắt lỗi → hộp thoại lỗi đa ngôn ngữ. Thông báo ngắn (đã lưu, đã xuất…) hiện bằng Snackbar.
- Tiêu đề cửa sổ: `PDF Editor — <tên file> •` (• = có thay đổi chưa lưu); thanh trạng thái: thông báo, "Có thay đổi
  chưa lưu", Trang x / y, zoom.
- Panel trái / phải ẩn hiện được (nhớ trong settings `ShowSidePanel`, `ShowProperties`), kéo giãn bằng GridSplitter.
  Rail trái chọn panel: Trang (thumbnail), Mục lục, Tìm kiếm, Nhận xét.
- Thanh nổi dưới vùng xem: trang trước / sau, ô số trang, zoom −/+, menu % (50–400%), Vừa chiều rộng, Vừa trang.

### Phím tắt
| Phím | Lệnh | Phím | Lệnh |
|------|------|------|------|
| Ctrl+N | Tạo mới | Ctrl+O | Mở |
| Ctrl+S | Lưu | Ctrl+Shift+S | Lưu thành |
| Ctrl+E | Xuất | Ctrl+P | In |
| Ctrl+W | Đóng tab | Ctrl+Tab / Ctrl+Shift+Tab | Tab kế / trước |
| Ctrl+, | Cài đặt | Ctrl+F | Tìm |
| F3 / Shift+F3 | Kết quả tìm kế / trước | Esc | Hủy / bỏ chọn / về công cụ Chọn |
| Ctrl+Z / Ctrl+Y | Hoàn tác / Làm lại | Alt+1 / Alt+2 | Công cụ Chọn / Sửa nội dung |
| Ctrl+T | Thêm chữ | Ctrl+Shift+H | Tô sáng chữ đang chọn |
| Ctrl+C / Ctrl+A | Sao chép chữ / chọn hết chữ trang | Delete | Xóa object / nhận xét đang chọn |
| Mũi tên (Shift) | Dịch object 1 pt (10 pt) | Ctrl + lăn chuột | Zoom quanh con trỏ |
| Ctrl+ + / Ctrl+ - | Phóng to / thu nhỏ | Ctrl+1 / Ctrl+0 / Ctrl+2 | 100% / Vừa trang / Vừa chiều rộng |
| Ctrl+PageUp / Ctrl+PageDown | Trang trước / sau | Ctrl+Home / Ctrl+End | Trang đầu / cuối |
| Ctrl+R / Ctrl+Shift+R | Xoay phải / trái | Ctrl+Shift+Enter | Thêm trang trắng |
| Ctrl+Delete | Xóa trang | Ctrl+Shift+D | Nhân bản trang |
| Ctrl+Shift+I | Chèn trang từ PDF | | |

## Lưu ý / giới hạn
- Mọi chữ hiển thị lấy từ `Resources/Strings*.resx` (xem i18n.md); icon là `ui:SymbolIcon` (xem theme.md).
- Thêm lệnh mới: khai báo trong `AppCommands` (hoặc dùng `AppCommands.Action` + một nhánh trong `OnAction`),
  thêm CommandBinding trong `MainWindow.xaml`, thêm chuỗi vào cả hai file resx, cập nhật bảng phím tắt ở trên.

## Cách test
- Tự động: `I18nTests`, `UiResourceTests` (icon, resource key, gợi ý công cụ, định dạng export).
- Thủ công: `dev.bat`; thu nhỏ cửa sổ tới kích thước tối thiểu (ribbon cuộn ngang, không bị cắt); kéo GridSplitter;
  ẩn/hiện panel; thử bảng phím tắt; đổi theme / ngôn ngữ khi đang mở tài liệu.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo giao diện chính, styles, phím tắt | Khởi tạo dự án |
| 2026-10-03 | Làm lại toàn bộ UI v2: WPF-UI FluentWindow, ribbon 7 tab, tab tài liệu, rail + panel trái, thanh zoom nổi, Snackbar, công cụ dạng toggle, thêm phím tắt | Yêu cầu UI tối, hiện đại, đủ tính năng như Acrobat / Microsoft |
