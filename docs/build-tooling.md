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
| `build_windows.bat [rid]` | Chạy test (Release) → `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true` → `dist\win-x64\PDFEditor.exe` (~72 MB, không cần cài .NET). `rid` khác: `win-x86`, `win-arm64`. Test lỗi → dừng. |
| `lint.bat` | `dotnet format` — tự sửa theo `.editorconfig`. |
| `lint.bat --check` | `dotnet format --verify-no-changes` + `dotnet build -warnaserror` — phải 0 lỗi. |
| `test.bat` | `dotnet test PDFEditorApp.sln` (tham số thêm được chuyển tiếp, vd. `test.bat --filter I18n`). |

- `pdfium.dll` (native) được nhúng vào exe nhờ `IncludeNativeLibrariesForSelfExtract`, tự giải nén khi chạy.

## Lưu ý / giới hạn
- Cần .NET SDK 8 để dev/build (máy chạy exe production thì không cần).
- File `.bat` phải dùng xuống dòng CRLF.
- Analyzer: tên hàm P/Invoke giữ đúng tên C (`FPDF_*`) → tắt CA1707/IDE1006 riêng cho `Services/Pdfium/`.
  Test project tắt CA1707 (tên test có `_`) và CA1861 (mảng trong `[InlineData]`).

## Cách test
- Chạy lần lượt `lint.bat --check`, `test.bat`, `build_windows.bat` → exit code 0; mở `dist\win-x64\PDFEditor.exe`.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo dev.bat, build_windows.bat, lint.bat, test.bat, scripts/env.bat | Khởi tạo dự án |
| 2026-10-03 | Thêm package WPF-UI, PDFsharp, OpenXml; exe production ~66 MB → ~72 MB | UI v2 + mật khẩu + xuất Word |
