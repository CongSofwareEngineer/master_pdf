# PDF Editor

Ứng dụng desktop Windows 10/11 để **xem, chỉnh sửa, nhận xét, ký và quản lý PDF** — giao diện Fluent hiện đại
(mặc định tông tối), nhiều tài liệu theo tab, bộ công cụ theo kiểu Adobe Acrobat / Microsoft Edge.

- Người phát triển: Nguyễn Thị Bích Lý — MSSV 50.01.902.066 — Lớp ST4 Ca 2
- Công nghệ: C# · .NET 8 · WPF · [WPF-UI](https://github.com/lepoco/wpfui) (Fluent, Mica) ·
  [PDFium](https://pdfium.googlesource.com/pdfium/) (engine PDF của Chrome, NuGet `bblanchon.PDFium.Win32`) ·
  [PDFsharp](https://github.com/empira/PDFsharp) (mã hóa AES-256) · Open XML SDK (xuất Word)
- Yêu cầu gốc: [design.md](design.md) · Quy tắc làm việc: [CLAUDE.md](CLAUDE.md) · Log từng chức năng: [docs/log.md](docs/log.md)

## Tính năng

| Nhóm | Chức năng |
|------|-----------|
| Giao diện | Tối / Sáng / Theo hệ thống (Mica trên Windows 11), ribbon 7 tab, nhiều tab tài liệu + Trang chủ, panel trái (Trang / Mục lục / Tìm / Nhận xét), panel thuộc tính, tiếng Việt / English |
| Mở & tạo | Hộp thoại, kéo thả nhiều file, dòng lệnh, file gần đây, PDF có mật khẩu; tạo trắng, Ảnh → PDF, Kết hợp nhiều PDF |
| Xem | Cuộn liên tục / Một trang / Hai trang, zoom 10–800% (Ctrl + lăn chuột quanh con trỏ), Vừa trang / Vừa chiều rộng, thumbnail, đọc ban đêm |
| Đọc | Chọn & sao chép chữ, bấm liên kết, mục lục, tìm kiếm (không dấu tiếng Việt, cả từ, phân biệt hoa thường) |
| Chỉnh sửa | Sửa / thêm / xóa / di chuyển chữ (font, cỡ, đậm, nghiêng, màu), chọn & di chuyển / co giãn / xóa ảnh, hình; chèn ảnh; watermark; đầu / chân trang, đánh số trang |
| Nhận xét | Tô sáng, gạch chân, gạch ngang, gạch sóng, ghi chú, bút vẽ, chữ nhật, elip, đường, mũi tên, tẩy; danh sách nhận xét |
| Điền & Ký | Điền form (chữ, ô đánh dấu, radio, danh sách), chữ ký vẽ / gõ lưu để dùng lại, ✓ ✗ ngày |
| Trang | Màn hình Sắp xếp trang (chọn nhiều, kéo thả), xoay, xóa, thêm trắng, nhân bản, chèn từ file, trích xuất, tách |
| Bảo vệ | Đặt mật khẩu AES-256 + quyền (in, sao chép, sửa), gỡ mật khẩu, che thông tin (xóa thật) |
| Chuyển đổi | PNG, JPG (72–300 DPI), Word .docx, TXT; In (chọn phạm vi trang) |
| Lưu | Lưu (hỏi trước khi ghi đè), Lưu thành, ghi an toàn (file tạm), nhắc lưu khi đóng tab / app, hoàn tác / làm lại 30 bước |

Phím tắt đầy đủ: [docs/ui-layout.md](docs/ui-layout.md).

## Chạy & build

Cần **.NET SDK 8** (hoặc mới hơn) để phát triển. Máy chỉ chạy file `.exe` production thì không cần cài gì.

| Mục đích | Lệnh |
|----------|------|
| **Chạy dev (Debug)** | `dev.bat` — hoặc `dev.bat duong\dan\file.pdf` để mở sẵn file |
| Chạy dev, tự build lại khi sửa code | `dev.bat watch` |
| **Build production** | `build_windows.bat` → `dist\win-x64\PDFEditor.exe` (1 file, self-contained) |
| Build cho Windows 32-bit / ARM | `build_windows.bat win-x86` / `build_windows.bat win-arm64` |
| Chạy test | `test.bat` (= `dotnet test`) |
| Lint (tự sửa format) | `lint.bat` |
| Lint kiểm tra (phải 0 lỗi) | `lint.bat --check` |

`build_windows.bat` chạy toàn bộ test trước; test lỗi thì dừng, không tạo exe. Đang chạy bản dev thì đóng app trước
khi build lại (file exe trong `bin\Debug` bị khóa). Chi tiết: [docs/build-tooling.md](docs/build-tooling.md).

## Cấu trúc dự án

```
Master_PDF/
├── PDFEditorApp.sln
├── dev.bat · build_windows.bat · lint.bat · test.bat · scripts/env.bat
├── global.json · Directory.Build.props · .editorconfig
├── docs/                         # log từng chức năng (mục lục: docs/log.md)
├── PDFEditorApp/
│   ├── App.xaml(.cs)             # khởi động, theme, ngôn ngữ, bắt lỗi chung
│   ├── MainWindow.xaml(.cs)      # FluentWindow: tab tài liệu, ribbon, điều phối lệnh
│   ├── Models/                   # dữ liệu thuần (không WPF): Geometry, PdfModels, ReadingModels, EditModels, AppSettings…
│   ├── Services/                 # logic (không WPF)
│   │   ├── Pdfium/               # P/Invoke pdfium.dll + khởi tạo/lock
│   │   ├── PdfService*.cs        # mở, render, sửa, trang, nhận xét, form, object, watermark, undo/redo, lưu (partial)
│   │   ├── FontService.cs · FontNameFixer.cs · TextSearch.cs · SecurityService.cs
│   │   ├── ExportService.cs · PngEncoder.cs · PageRangeParser.cs
│   │   ├── FileService.cs · SettingsService.cs · SystemService.cs · LocalizationService.cs
│   ├── Views/                    # WPF: DocumentWorkspace, DocumentView, PageView, ThemeService, dialog…
│   │   ├── Tools/                # công cụ trên trang (chọn, sửa nội dung, nhận xét, vẽ, ký, che…)
│   │   └── Panels/               # thumbnail, mục lục, tìm, nhận xét, thuộc tính, sắp xếp trang
│   └── Resources/                # Styles.xaml, Strings.resx (en), Strings.vi.resx (vi), app.ico
└── PDFEditorApp.Tests/           # xUnit: engine PDF, tính năng, export, settings, i18n, tài nguyên UI, kiến trúc
```

Quy tắc chính (đầy đủ trong `CLAUDE.md`): đọc `docs/` trước khi code; `Models/`, `Services/` không dùng WPF;
chữ UI lấy từ `Strings.resx` + bản dịch `Strings.vi.resx`; `lint.bat --check` và `test.bat` phải sạch.

## Giới hạn đã biết

- Đơn vị sửa chữ là "text object" của PDF (thường là một dòng / cụm chữ, tùy file).
- PDF có font trùng tên (hay gặp khi in từ Chrome): app đổi tên font để PDFium sửa đúng; nếu không được thì dùng
  **chế độ phủ** (che chữ cũ bằng màu nền, đặt chữ mới lên trên). Chi tiết: [docs/text-edit.md](docs/text-edit.md).
- Chữ ký là chữ ký hình ảnh, không phải chữ ký số. Form: không chạy JavaScript, không hỗ trợ XFA.
- Xuất Word chỉ giữ chữ (không bố cục / ảnh). Không có OCR cho PDF scan. In dạng ảnh 200 DPI.
- Che thông tin chuyển trang đó thành ảnh (không còn chọn / tìm chữ trên trang đó).

## Giấy phép thư viện

PDFium: BSD-3-Clause / Apache-2.0 (bản build của bblanchon/pdfium-binaries, MIT cho phần đóng gói) ·
WPF-UI: MIT · PDFsharp: MIT · DocumentFormat.OpenXml: MIT.
