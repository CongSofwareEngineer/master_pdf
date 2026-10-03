# Quy tắc chính của dự án — PDF Editor

File này là bộ quy tắc bắt buộc cho mọi phiên làm việc (người hoặc AI) trên dự án.
Yêu cầu chức năng / kỹ thuật gốc: xem `design.md`.

## Cấu trúc docs

- Mỗi chức năng có **một file log riêng** trong `docs/` (vd. `docs/open-file.md`,
  `docs/viewer.md`, `docs/text-edit.md`, `docs/page-manage.md`, `docs/save.md`…).
  **Không gộp nhiều chức năng vào một file.**
- `docs/log.md` chỉ là **mục lục**: bảng chức năng → file log, kèm mẫu file log.

## 1. Đọc `docs/` TRƯỚC khi code

- Trước khi làm bất kỳ tính năng / sửa lỗi / sửa logic nào, **luôn đọc `docs/log.md`** để tìm
  file log của chức năng liên quan, rồi **đọc các file log đó** để lấy dữ liệu, quyết định cũ
  và lưu ý trước khi viết code.
- Nếu thay đổi chạm nhiều chức năng → đọc file log của từng chức năng đó.
- Nếu docs mâu thuẫn với code hiện tại: tin code, rồi sửa lại docs cho đúng.

## 2. Làm tính năng mới → tạo file log riêng

- Tính năng mới (chưa thuộc chức năng nào đã có) → tạo file `docs/<ten-chuc-nang>.md` theo
  mẫu trong `docs/log.md`, và thêm một dòng vào bảng mục lục của `docs/log.md`.
- Tính năng mở rộng một chức năng đã có → ghi vào file log của chức năng đó.
- Ghi ngay trong lần làm đó, không để sau. Nội dung tối thiểu: mục đích, file/module bị ảnh
  hưởng, logic chính (đủ để người khác trong team hiểu), lưu ý / giới hạn, cách test.

## 3. Sửa logic → cập nhật file log có sẵn của chức năng đó

- Sửa **bất kỳ** logic nào cũng phải cập nhật file log của chức năng đó để team hiểu.
- **Không tạo file log mới cho mỗi lần sửa.** Thay vào đó:
  1. Sửa phần "Logic chính" / "Lưu ý" cho đúng hiện trạng, và
  2. Thêm một dòng vào "Lịch sử thay đổi" (ngày + sửa gì + vì sao).
- Sửa chạm nhiều chức năng → cập nhật file log của từng chức năng bị ảnh hưởng.

## 4. Điều kiện bắt buộc trước khi code (luồng chuẩn)

**Chưa có file log của chức năng thì KHÔNG được bắt đầu code.**

```
Nhận task (thêm / sửa tính năng)
        │
        ▼
Đọc docs/log.md (mục lục) → chức năng này đã có file log chưa?
        │
   ┌────┴─────────────────────────┐
   │ CÓ                            │ CHƯA CÓ
   ▼                               ▼
Đọc file log đó                 Tạo file docs/<ten-chuc-nang>.md TRƯỚC
→ lấy làm dữ liệu               (theo mẫu, ghi mục đích, file dự kiến
→ sửa tiếp trên nền đó           sửa, logic dự kiến) + thêm vào mục lục
   │                               │
   │                               ▼
   │                            Lấy file log vừa tạo làm dữ liệu
   └────┬─────────────────────────┘
        ▼
Đọc code liên quan → BẮT ĐẦU CODE → test (`dotnet test`)
        │
        ▼
Cập nhật lại file log cho đúng với code thực tế
(Logic chính, Lưu ý, Test, Lịch sử thay đổi)
+ README.md nếu ảnh hưởng tới người dùng / cách build
        │
        ▼
Xong (chỉ xong khi log đã khớp với code)
```

**Ví dụ: task "thêm / sửa tính năng đổi sáng tối (light/dark theme)"**

1. Mở `docs/log.md`, tìm chức năng giao diện sáng/tối.
2. **Nếu đã có** (vd. `docs/theme.md`): đọc hết file đó (logic hiện tại, file liên quan, lưu ý,
   lịch sử) → dùng làm dữ liệu → sửa tiếp trên nền đó.
3. **Nếu chưa có**: tạo `docs/theme.md` trước (mục đích, file dự kiến chạm như
   `Resources/Styles.xaml`, `MainWindow.xaml`, `Views/SettingsView.xaml`; logic dự kiến; lưu ý)
   và thêm dòng "Giao diện sáng / tối → theme.md" vào mục lục → lấy file đó làm dữ liệu.
4. Sau đó mới đọc code và bắt đầu code.
5. Code xong → cập nhật `docs/theme.md` cho khớp với code thực tế + ghi "Lịch sử thay đổi".

## Ghi chú kỹ thuật nhanh

- Stack: C# + WPF (.NET 6+). Cấu trúc dự án, cách chạy, build, test: xem `README.md`.
- Chữ hiển thị trên UI (XAML lẫn C#) không được hard-code: dùng key trong
  `Resources/Strings.resx` và phải có bản dịch trong `Resources/Strings.vi.resx`
  (test `I18nTests` trong `PDFEditorApp.Tests/` sẽ fail nếu thiếu).
- `Models/` và `Services/` không được dùng WPF (`System.Windows.*`); UI nằm trong `Views/`,
  `MainWindow.xaml`, `Resources/`.
- Code style như ESLint: `.editorconfig` + .NET analyzers (`Nullable` bật, warning = lỗi).
  Sửa xong chạy `lint.bat --check` (`dotnet format --verify-no-changes` + `dotnet build -warnaserror`)
  — phải 0 lỗi.
- App chạy Windows 10/11: phần phụ thuộc hệ điều hành đặt trong `Services/SystemService.cs`;
  không hard-code `C:\…` (dùng `Environment.GetFolderPath`, `Path.Combine`).
  Build `.exe`: `build_windows.bat` (`dotnet publish` self-contained, single file).
