# Bộ cài đặt Windows (Setup.exe)

## Mục đích
Bản production phát hành dưới dạng **bộ cài đặt** như một phần mềm bình thường (không phải file exe chạy rời):
cài vào thư mục chương trình, có shortcut Start menu / Desktop, hiện trong "Apps & features" để gỡ cài đặt,
đăng ký mở file `.pdf`. Người dùng cuối không cần cài .NET.

## File / module liên quan
- `installer/PDFEditor.iss` — script Inno Setup 6 (UTF-8 có BOM).
- `installer/Languages/Vietnamese.isl` — bản dịch tiếng Việt cho trình cài đặt (bản unofficial lấy từ
  `jrsoftware/issrc` → `Files/Languages/Unofficial/Vietnamese.isl`, thêm BOM UTF-8). Tiếng Anh dùng `Default.isl`
  có sẵn của Inno Setup.
- `build_windows.bat` — publish app rồi gọi `ISCC.exe` để đóng gói.
- `PDFEditorApp/PDFEditorApp.csproj` — `<Version>` là nguồn số phiên bản cho bộ cài.
- `PDFEditorApp/Resources/app.ico` — icon của Setup.exe.

## Logic chính
1. `build_windows.bat [rid]` chạy test → `dotnet publish` **self-contained, không single-file**
   (`-p:PublishSingleFile=false`) vào `dist\publish\<rid>\`. Bản cài không cần gói 1 file: khởi động nhanh hơn,
   không giải nén `pdfium.dll` ra thư mục tạm mỗi lần chạy.
2. Lấy phiên bản bằng `dotnet msbuild -getProperty:Version` (vd. `1.0.0`).
3. Tìm `ISCC.exe` (PATH → `%LOCALAPPDATA%\Programs\Inno Setup 6` → `%ProgramFiles(x86)%` → `%ProgramFiles%`),
   gọi `ISCC /DAppVersion=… /DRid=… /DSourceDir=… installer\PDFEditor.iss`.
4. Kết quả: `dist\PDFEditor-Setup-<version>-<rid>.exe`.

Bộ cài (`PDFEditor.iss`):
- `AppId` cố định `{F9C05033-07F3-42A7-827C-31583772D15F}` — **không được đổi**, nếu đổi Windows coi là app khác,
  bản mới không ghi đè / gỡ được bản cũ.
- `PrivilegesRequired=lowest` + `PrivilegesRequiredOverridesAllowed=dialog`: hỏi cài **cho riêng tôi**
  (`%LOCALAPPDATA%\Programs\PDF Editor`, không cần quyền admin) hay **cho mọi người**
  (`C:\Program Files\PDF Editor`, cần UAC). Đường dẫn dùng hằng `{autopf}`, registry dùng `HKA` (tự chọn HKCU/HKLM).
- Kiến trúc theo `rid`: `win-x64` → `x64compatible`, `win-arm64` → `arm64`, `win-x86` → chạy mọi máy.
- `MinVersion=10.0` (Windows 10/11).
- Tác vụ: shortcut Desktop (mặc định chọn), đăng ký mở file `.pdf` (mặc định chọn).
- Đăng ký `.pdf`: ProgID `PDFEditor.Document` (lệnh `"{app}\PDFEditor.exe" "%1"`), thêm vào
  `.pdf\OpenWithProgids`, `Applications\PDFEditor.exe\SupportedTypes`, và `Capabilities` + `RegisteredApplications`
  để app xuất hiện trong *Settings → Default apps*. Không ép làm app mặc định (Windows 10/11 không cho phép) —
  người dùng chọn trong "Open with". Gỡ cài đặt xóa các khóa này (`uninsdeletekey` / `uninsdeletevalue`).
- `CloseApplications=yes`: khi cập nhật / gỡ mà app đang mở, Setup đề nghị đóng app (Restart Manager).
- Cài bản mới đè lên bản cũ (cùng `AppId`) giữ nguyên thư mục và chế độ cài (cho tôi / mọi người) của lần trước.
  Cố ý **không** dùng `[InstallDelete] {app}\*` — người dùng có thể chọn thư mục đang chứa file khác; DLL thừa của
  bản cũ vô hại vì .NET chỉ nạp theo `PDFEditor.deps.json`.
- Luôn ghi `App Paths\PDFEditor.exe` (gõ `PDFEditor` trong Win + R).
- Trang cuối có ô "Chạy PDF Editor".
- Kích thước (win-x64, 1.0.0): thư mục publish ~168 MB, Setup.exe ~52 MB (nén lzma2/ultra64).

## Lưu ý / giới hạn
- Cần **Inno Setup 6.5+** trên máy build: `winget install JRSoftware.InnoSetup`. Máy người dùng không cần.
- Gỡ cài đặt **giữ lại** cài đặt người dùng `%APPDATA%\PDFEditorApp\settings.json` (chữ ký đã lưu, file gần đây…)
  như phần mềm thông thường.
- App không được ghi file cạnh exe (thư mục cài có thể là Program Files, chỉ đọc) — dữ liệu ghi qua
  `SystemService.AppDataDirectory`.
- Setup.exe chưa ký số (code signing) → Windows SmartScreen có thể cảnh báo "Unknown publisher" lần đầu.
- Tăng `<Version>` trong `PDFEditorApp.csproj` mỗi lần phát hành để Windows hiện đúng phiên bản.

## Cách test
- Tự động: `build_windows.bat` (chạy `dotnet test` trước, test lỗi thì không đóng gói).
- Cài im lặng (kiểm tra nhanh, không cần bấm):
  `PDFEditor-Setup-….exe /VERYSILENT /CURRENTUSER /DIR="<thư mục tạm>" /TASKS=""` → chạy `<thư mục>\PDFEditor.exe`
  → gỡ bằng `<thư mục>\unins000.exe /VERYSILENT`. Đã chạy 2026-10-04: cài, mở app, gỡ đều exit 0; sau gỡ không còn
  exe, shortcut Start menu, khóa `App Paths`.
- Thủ công:
  1. Chạy `dist\PDFEditor-Setup-<version>-win-x64.exe`, chọn "Chỉ cho tôi" → cài xong có shortcut Start menu,
     Desktop; app mở được.
  2. Chuột phải file `.pdf` → Open with → có "PDF Editor" → mở đúng file.
  3. Chạy lại Setup (cài đè) khi app đang mở → Setup đề nghị đóng app.
  4. Settings → Apps → "PDF Editor" → Uninstall → thư mục cài, shortcut, mục "Open with" biến mất.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-04 | Tạo bộ cài Inno Setup (`installer/`), `build_windows.bat` tạo Setup.exe thay cho exe single-file | Bản production cần cài như phần mềm (shortcut, gỡ cài đặt, mở .pdf) |
