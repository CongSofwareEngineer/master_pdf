# Mục lục log chức năng

Mỗi chức năng có **một file log riêng**. Trước khi code, đọc file log của chức năng liên quan
(xem quy tắc trong `CLAUDE.md`).

| Chức năng | File log |
|-----------|----------|
| Giao diện chính (shell, ribbon, panel, phím tắt) | [ui-layout.md](ui-layout.md) |
| Giao diện tối / sáng (WPF-UI Fluent) | [theme.md](theme.md) |
| Nhiều tài liệu (tab) + màn hình chính | [tabs.md](tabs.md) |
| Engine PDF (PDFium, P/Invoke, luồng xử lý) | [pdf-engine.md](pdf-engine.md) |
| Mở file PDF (dialog, kéo thả, mật khẩu, file gần đây) | [open-file.md](open-file.md) |
| Xem PDF (cuộn liên tục, chế độ xem, zoom, thumbnail, chế độ đêm) | [viewer.md](viewer.md) |
| Chọn & sao chép văn bản, liên kết | [text-select.md](text-select.md) |
| Tìm kiếm văn bản | [search.md](search.md) |
| Mục lục (bookmark) | [bookmarks.md](bookmarks.md) |
| Chỉnh sửa văn bản (sửa / thêm / xóa / di chuyển text) | [text-edit.md](text-edit.md) |
| Sửa đối tượng & chèn ảnh (ảnh, hình vẽ) | [page-objects.md](page-objects.md) |
| Nhận xét (tô sáng, gạch chân, ghi chú, bút vẽ, hình) | [annotations.md](annotations.md) |
| Điền form | [forms.md](forms.md) |
| Chữ ký & Điền nhanh (✓, ✗, ngày) | [sign.md](sign.md) |
| Watermark | [watermark.md](watermark.md) |
| Đầu trang / chân trang, đánh số trang | [header-footer.md](header-footer.md) |
| Quản lý trang (xoay, xóa, thêm, sắp xếp, trích xuất, tách) | [page-manage.md](page-manage.md) |
| Tạo PDF (trắng, từ ảnh, kết hợp nhiều file) | [create-pdf.md](create-pdf.md) |
| Hoàn tác / Làm lại (Undo / Redo) | [undo-redo.md](undo-redo.md) |
| Lưu file (Save, Save As, xác nhận, nhắc lưu khi đóng) | [save.md](save.md) |
| Bảo vệ (đặt / gỡ mật khẩu, che thông tin) | [protect.md](protect.md) |
| In | [print.md](print.md) |
| Export (PNG, JPG, TXT, Word) | [export.md](export.md) |
| Đa ngôn ngữ (Strings.resx, đổi ngôn ngữ) | [i18n.md](i18n.md) |
| Cài đặt người dùng (settings.json) | [settings.md](settings.md) |
| Lệnh dev / build / lint / test | [build-tooling.md](build-tooling.md) |

## Mẫu file log

Tạo file `docs/<ten-chuc-nang>.md` theo mẫu dưới, rồi thêm một dòng vào bảng trên.

```markdown
# <Tên chức năng>

## Mục đích
<Chức năng làm gì, cho ai, vì sao cần.>

## File / module liên quan
- `PDFEditorApp/...` — vai trò

## Logic chính
<Luồng xử lý chính, đủ để người khác trong team hiểu và sửa tiếp.>

## Lưu ý / giới hạn
- ...

## Cách test
- Tự động: `dotnet test` — test nào bao phủ.
- Thủ công: các bước kiểm tra trên app.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| YYYY-MM-DD | ... | ... |
```
