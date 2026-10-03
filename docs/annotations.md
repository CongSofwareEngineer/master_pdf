# Nhận xét (annotation)

## Mục đích
Công cụ nhận xét kiểu Acrobat / Edge: tô sáng, gạch chân, gạch ngang, gạch sóng, ghi chú, bút vẽ, hình chữ nhật,
elip, đường thẳng, mũi tên, tẩy; danh sách nhận xét.

## File / module liên quan
- `Services/PdfService.Annotations.cs` — `GetAnnotations`, `AddTextMarkup`, `AddInk`, `AddLine`, `AddShape`,
  `AddNote`, `DeleteAnnotation`, `SetAnnotationContents`, `AnnotationAuthor`, `PdfDate`.
- `Models/EditModels.cs` — `AnnotationKind`, `AnnotationInfo`.
- `Views/Tools/DrawTools.cs` — `MarkupTool` (kéo qua chữ), `InkTool` (bút / bút dạ quang), `ShapeTool`
  (chữ nhật, elip, đường, mũi tên), `NoteTool`, `EraserTool`.
- `Views/Panels/CommentsPanel.xaml(.cs)` + `CommentItem` — danh sách nhận xét theo trang, bấm để nhảy tới, xóa.
- `Views/Panels/PropertiesPanel` — màu, độ dày, độ mờ của công cụ đang dùng; với nhận xét đang chọn: sửa nội dung,
  xóa (`SaveSelectedAnnotationContents`, `DeleteSelectedAnnotation`).
- `Models/AppSettings.cs` — `AnnotationColor`, `InkColor`, `InkWidth`, `AnnotationAuthor`.

## Logic chính
- Tạo annotation chuẩn PDF (`FPDFPage_CreateAnnot`), đặt màu (`FPDFAnnot_SetColor`), quad points / ink list /
  rect, tác giả (`T`), ngày (`M`, `CreationDate`), nội dung (`Contents`). PDFium tự sinh appearance khi render
  cho highlight / underline / strikeout / squiggly / ink / square / circle / text.
- Không đụng content stream → an toàn với mọi PDF; Undo / Redo qua `Mutate` như các sửa khác.
- Đường thẳng / mũi tên lưu dạng Ink (PDFium không sinh appearance cho Line); đầu mũi tên là hai nét ngắn.
- Markup: chọn chữ bằng `MarkupTool` (hoặc `SelectTool` + nút trong panel / Ctrl+Shift+H) → khung từng dòng
  từ `PageTextLayout.GetRangeRects` → quad points.
- Tẩy: bấm vào nhận xét (hit-test theo khung) → `DeleteAnnotation`. Panel nhận xét và panel thuộc tính cũng xóa được.
- Tác giả mặc định = tên người dùng Windows (`SystemService.UserName`), đổi trong Cài đặt.

## Lưu ý / giới hạn
- Chưa di chuyển / đổi màu nhận xét đã tạo (xóa rồi vẽ lại).
- Annotation do app khác tạo mà không có appearance có thể hiển thị khác Acrobat.

## Cách test
- Tự động: `AnnotationTests.Highlight_IsRenderedAndPersisted`, `AnnotationTests.Ink_Shapes_Note_Line_Delete`.
- Thủ công: tab Nhận xét → dùng từng công cụ, đổi màu / độ dày, xóa bằng tẩy, sửa nội dung ghi chú trong panel thuộc tính,
  lưu rồi mở bằng Edge / Acrobat.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành 11 công cụ nhận xét + panel nhận xét; đường / mũi tên lưu dạng Ink | PDFium không sinh appearance cho Line |
