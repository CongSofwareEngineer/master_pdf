# Hoàn tác / Làm lại

## Mục đích
Hoàn tác (Ctrl+Z) / làm lại (Ctrl+Y) mọi thao tác sửa tài liệu (text, object, nhận xét, form, trang, watermark…).

## File / module liên quan
- `Services/PdfService.cs` — `Mutate`, `EditObject`, `CommitMutation`, `PushUndo`, `Undo`, `Redo`, `ReplaceDocument`.
- `Views/DocumentWorkspace.Commands.cs` — `UndoRedoAsync` (sau undo/redo nạp lại dữ liệu trang, thumbnail,
  mục lục, nhận xét như khi đổi cấu trúc trang). Mỗi tab có ngăn undo riêng.

## Logic chính
- Trước mỗi thao tác sửa: chụp snapshot = bytes PDF (`FPDF_SaveAsCopy` vào bộ nhớ) + mã trạng thái.
- Undo: lưu trạng thái hiện tại vào ngăn redo, nạp lại snapshot (`ReplaceDocument`, giữ đường dẫn + mật
  khẩu). Redo ngược lại. Thao tác sửa mới xóa ngăn redo.
- "Có thay đổi chưa lưu" = mã trạng thái hiện tại ≠ mã lúc lưu gần nhất → undo về đúng trạng thái đã lưu thì
  không còn dấu •.
- Giới hạn: 30 bước và tổng 512 MB snapshot; vượt thì bỏ bước cũ nhất (luôn giữ ít nhất 1).
- Thao tác lỗi → khôi phục snapshot, không tạo bước undo.

## Lưu ý / giới hạn
- File rất lớn (50 MB+): mỗi thao tác tốn thêm thời gian lưu snapshot (~ thời gian lưu file) và RAM.
- Undo / redo đóng ô sửa trực tiếp đang mở.

## Cách test
- Tự động: `UndoRedo_RestoresStatesAndModifiedFlag`, `Undo_TextEdit_RestoresOriginalText`,
  `DeleteTextObject_NonTextIndex_Throws` (lỗi không tạo bước undo), `DeleteAndMove_OnPageWithAmbiguousFonts_...`.
- Thủ công: sửa text, tô sáng, điền form, xoay trang, Ctrl+Z / Ctrl+Y; dấu • trên tab biến mất khi undo về trạng thái đã lưu.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo undo/redo bằng snapshot | Khởi tạo dự án |
| 2026-10-03 | Tách `CommitMutation` dùng chung cho `Mutate` và `EditTextObject` | Sửa text có thể khôi phục + làm lại ở chế độ phủ |
| 2026-10-03 | Undo / redo theo từng tab; mọi tính năng v2 (nhận xét, form, object, watermark…) đi qua `Mutate` / `EditObject` | UI v2 đa tab |
