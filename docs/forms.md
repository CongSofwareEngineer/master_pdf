# Điền form

## Mục đích
Điền các form PDF (AcroForm): ô chữ (1 dòng / nhiều dòng), ô đánh dấu, nút chọn (radio), combo / danh sách.

## File / module liên quan
- `Services/PdfService.Forms.cs` — `HasForms`, `GetFormFields`, `SetFormFieldText`, `ToggleFormField`, `SetFormFieldChoice`.
- `Services/PdfService.cs` — khởi tạo / hủy môi trường form-fill khi mở / đóng / khôi phục snapshot;
  `RenderCore` vẽ widget bằng `FPDF_FFLDraw`.
- `Services/Pdfium/PdfiumNativeExtra.cs` — P/Invoke `FPDFDOC_InitFormFillEnvironment`, `FORM_*`, `FPDF_FFLDraw`, `FPDFAnnot_GetFormField*`.
- `Models/EditModels.cs` — `FormFieldKind`, `FormFieldInfo`.
- `Views/DocumentWorkspace.xaml.cs` — `DrawForms`: đặt control WPF trùng vị trí từng ô (lớp `PageView.FormLayer`),
  thanh thông báo "Tài liệu có form điền được".

## Logic chính
- Mở tài liệu: cấp `FPDF_FORMFILLINFO` (vùng nhớ 1024 byte, version 1, mọi callback = null — PDFium kiểm tra null
  trước khi gọi) → `FPDFDOC_InitFormFillEnvironment`. `_form != 0` ⇔ tài liệu có AcroForm.
- `GetFormFields`: duyệt annotation `Widget`, đọc loại / tên / giá trị / trạng thái chọn / cờ (chỉ đọc, nhiều dòng,
  mật khẩu) / danh sách lựa chọn, khung đổi sang hệ view. Được cache theo trang cùng text layout.
- Ghi (đều qua `Mutate` → Undo / Redo được):
  - Ô chữ: `FORM_SetFocusedAnnot` + `FORM_SelectAllText` + `FORM_ReplaceSelection` (ghi khi TextBox mất focus).
  - Ô đánh dấu / radio: giả lập click giữa ô (`FORM_OnLButtonDown/Up`).
  - Combo / danh sách: `FORM_SetFocusedAnnot` + `FORM_SetIndexSelected`.
- Hiển thị: PDFium **không** vẽ widget trong `FPDF_RenderPageBitmap`; phải gọi `FPDF_FFLDraw` sau đó
  (với trang không phải trang đang hoạt động thì bọc `FORM_OnAfterLoadPage` / `FORM_OnBeforeClosePage`).
  Ảnh cho export / in cũng vẽ widget.
- Ô chỉ đọc không đặt control.

## Lưu ý / giới hạn
- Không chạy JavaScript của form (tính toán, định dạng, kiểm tra). Không hỗ trợ XFA.
- Nút bấm (push button) và ô chữ ký số không điền được.
- Cỡ chữ trong TextBox ước lượng theo chiều cao ô; giá trị khi lưu do PDFium sinh appearance.
- Ô nhập của form nằm trên trang nên phím gõ trong ô không bị công cụ đang chọn chiếm (Enter của ô nhiều dòng,
  mũi tên, Delete) — xem phần "Phím → công cụ" trong viewer.md.

## Cách test
- Tự động: `ReadingTests.Forms_FillTextAndCheckbox_PersistAfterSave` (PDF fixture `PdfFixtures.FormAndLinks`;
  kiểm tra giá trị sau lưu + mở lại và pixel ô đánh dấu trên ảnh render phải tối).
- Thủ công: mở form thật, điền chữ, tick ô, chọn combo, Ctrl+Z, lưu, mở lại bằng app / Edge.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành điền form (text, checkbox, radio, combo/list) với control WPF đặt chồng | Tính năng Fill & Sign |
| 2026-10-03 | `RenderCore` gọi `FPDF_FFLDraw`; thêm kiểm tra pixel vào test form | Ô đánh dấu đã tick không hiện trên trang vì PDFium không vẽ widget khi render thường |
