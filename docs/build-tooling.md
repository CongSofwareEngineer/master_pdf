# Lệnh dev / build / lint / test

## Mục đích
Lệnh riêng cho chạy dev (debug) và build production (.exe độc lập), cùng lint và test.

## File / module liên quan
- `dev.bat` — chạy app Debug.
- `build_windows.bat` — build production.
- `lint.bat` — format / kiểm tra code.
- `test.bat` — chạy unit test.
- `scripts/env.bat` — tìm `dotnet` (PATH, hoặc bản cài riêng ở `%LOCALAPPDATA%\Microsoft\dotnet`).
- `global.json` — SDK 8.0.x trở lên (`rollForward: latestMajor`).
- `Directory.Build.props` — Nullable, ImplicitUsings, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`,
  `AnalysisLevel=latest-recommended`.
- `.editorconfig` — style C# (file-scoped namespace, `_camelCase` cho field private…).
- `PDFEditorApp/PDFEditorApp.csproj` — NuGet: `bblanchon.PDFium.Win32` 156.0.8076, `WPF-UI` 4.3.0,
  `PDFsharp` 6.2.4, `DocumentFormat.OpenXml` 3.5.1; `SatelliteResourceLanguages=en;vi`.

## Logic chính
| Lệnh | Việc làm |
|------|----------|
| `dev.bat` | `dotnet run -c Debug` (build + chạy). `dev.bat file.pdf` mở sẵn file. |
| `dev.bat watch` | `dotnet watch run` — tự build lại & khởi động lại khi sửa code C#. |
| `build_windows.bat [rid]` | Tìm Inno Setup (`ISCC.exe`) + đọc `<Version>` → chạy test (Release) → `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=false` vào `dist\publish\win-x64\` → `ISCC installer\PDFEditor.iss` → **bộ cài** `dist\PDFEditor-Setup-<version>-win-x64.exe` (~52 MB, không cần cài .NET). `rid` khác: `win-x86`, `win-arm64`. Test / publish / đóng gói lỗi → dừng. Chi tiết bộ cài: [installer.md](installer.md). |
| `lint.bat` | `dotnet format` — tự sửa theo `.editorconfig`. |
| `lint.bat --check` | `dotnet format --verify-no-changes` + `dotnet build -warnaserror` — phải 0 lỗi. |
| `test.bat` | `dotnet test PDFEditorApp.sln` (tham số thêm được chuyển tiếp, vd. `test.bat --filter I18n`). |

- Bản publish là thư mục (exe + DLL + `pdfium.dll`), không còn single-file: bộ cài copy cả thư mục vào máy.

## Lưu ý / giới hạn
- Cần .NET SDK 8 để dev/build và **Inno Setup 6.5+** để build production
  (`winget install JRSoftware.InnoSetup`). Máy cài bản production không cần gì.
- File `.bat` phải dùng xuống dòng CRLF.
- Analyzer: tên hàm P/Invoke giữ đúng tên C (`FPDF_*`) → tắt CA1707/IDE1006 riêng cho `Services/Pdfium/`.
  Test project tắt CA1707 (tên test có `_`) và CA1861 (mảng trong `[InlineData]`).

## Cách test
- Chạy lần lượt `lint.bat --check`, `test.bat`, `build_windows.bat` → exit code 0; chạy
  `dist\PDFEditor-Setup-<version>-win-x64.exe` để cài (xem [installer.md](installer.md)).

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo dev.bat, build_windows.bat, lint.bat, test.bat, scripts/env.bat | Khởi tạo dự án |
| 2026-10-03 | Thêm package WPF-UI, PDFsharp, OpenXml; exe production ~66 MB → ~72 MB | UI v2 + mật khẩu + xuất Word |
| 2026-10-04 | `build_windows.bat` tạo bộ cài Inno Setup thay cho exe single-file; publish ra `dist\publish\<rid>` | Bản production cần cài như phần mềm |
