# In

## Mục đích
In tài liệu (Ctrl+P) qua hộp thoại in của Windows, chọn được phạm vi trang.

## File / module liên quan
- `Views/UiHelpers.cs` — `PrintHelper.Print` + `PdfPaginator` (`DocumentPaginator`).
- `MainWindow.xaml(.cs)` — lệnh `AppCommands.Print` (Ctrl+P), nút In trên thanh công cụ nhanh và menu Tệp.

## Logic chính
- `System.Windows.Controls.PrintDialog` với `UserPageRangeEnabled` (Từ trang / Đến trang).
- `PdfPaginator.GetPage(n)`: render trang `from + n` ở 200 DPI (`forScreen: false`, kèm annotation + form),
  co vừa vùng in (`PrintableAreaWidth/Height`) giữ tỉ lệ, căn giữa. Trang render lần lượt khi trình in cần
  → không giữ cả tài liệu dạng ảnh trong RAM.
- Con trỏ chờ trong lúc gửi lệnh in.

## Lưu ý / giới hạn
- In dạng ảnh (raster 200 DPI): file in lớn hơn in vector, chữ không chọn được trên bản "Microsoft Print to PDF".
- Hướng giấy do người dùng chọn trong hộp thoại in; trang ngang trên giấy dọc sẽ được thu nhỏ cho vừa.

## Cách test
- Thủ công: Ctrl+P → máy in "Microsoft Print to PDF", in trang 2-3; mở file kết quả kiểm tra đúng trang, có nhận xét
  và giá trị form.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành in raster 200 DPI qua `DocumentPaginator`, chọn phạm vi trang; đặt trong `UiHelpers.cs` | In cơ bản, không phụ thuộc thư viện ngoài |
