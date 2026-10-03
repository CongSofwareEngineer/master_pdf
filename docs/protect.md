# Bảo vệ

## Mục đích
Đặt / gỡ mật khẩu PDF (kèm quyền in, sao chép, sửa); che thông tin nhạy cảm (redact) — xóa thật, không chỉ phủ đen.

## File / module liên quan
- `Services/SecurityService.cs` — `Encrypt` bằng PDFsharp 6.2.4 (AES-256, `SetEncryptionToV5`).
- `Services/PdfService.cs` — `SaveWithSecurity` (áp dụng chế độ bảo mật khi lưu).
- `Services/PdfService.Pages.cs` — `SetSecurity`, `SecurityMode`, `IsEncrypted`.
- `Services/PdfService.Objects.cs` — `ApplyRedactions`.
- `Models/EditModels.cs` — `SecurityOptions`, `SecurityMode` (`Keep` / `Remove` / `Apply`).
- `Views/ProtectDialog.xaml(.cs)` — nhập mật khẩu mở, mật khẩu chủ, quyền.
- `Views/Tools/DrawTools.cs` — `RedactTool` (kéo để đánh dấu vùng che, bấm vào vùng để bỏ).
- `Views/DocumentWorkspace.Commands.cs` — `ApplyRedactions` (xác nhận rồi áp dụng các vùng chờ).

## Logic chính
- Bảo mật chỉ áp dụng **khi lưu** (`SetSecurity` tính là một thay đổi chưa lưu):
  - `Keep`: lưu như thường (PDFium giữ bảo mật của file gốc).
  - `Remove`: `FPDF_SaveAsCopy` với cờ `FPDF_REMOVE_SECURITY`.
  - `Apply`: lưu không bảo mật như trên rồi `SecurityService.Encrypt` mã hóa AES-256; mật khẩu chủ trống thì dùng
    mật khẩu mở; quyền in / sao chép / sửa (sửa gồm chú thích, điền form, sắp xếp trang).
- Che thông tin: vùng chờ hiển thị viền đỏ (`RedactBrush`), lưu trong `ToolOptions.Redactions` theo trang. Áp dụng →
  với mỗi trang: render 150 DPI không kèm annotation, tô đen các vùng, **xóa mọi object** của trang, xóa annotation
  giao với vùng che, chèn ảnh đã tô làm nội dung duy nhất của trang. Là một bước Undo.
- Panel thuộc tính hiển thị "Mật khẩu: Có / Không" (`IsEncrypted`).

## Lưu ý / giới hạn
- Trang đã che thông tin thành ảnh: không còn chọn / tìm chữ được, dung lượng tăng.
- Annotation không giao vùng che vẫn giữ nguyên (trên ảnh).
- Không hỗ trợ chứng chỉ (certificate security) hay chữ ký số.

## Cách test
- Tự động: `DocumentFeatureTests.Security_ApplyAndRemovePassword`, `PageObjectTests.Redaction_RemovesTextForReal`.
- Thủ công: Bảo vệ → Đặt mật khẩu, lưu, mở lại (hỏi mật khẩu), Gỡ mật khẩu, lưu, mở lại. Che thông tin: kéo vùng,
  Áp dụng, thử tìm chữ đã che (không ra), lưu và mở bằng Edge.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành mật khẩu AES-256 (PDFsharp) + quyền, gỡ mật khẩu, che thông tin bằng rasterize trang | PDFium không có API mã hóa; che bằng ảnh đảm bảo chữ bị xóa thật |
