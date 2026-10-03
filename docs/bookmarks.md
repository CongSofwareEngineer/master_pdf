# Mục lục (bookmark)

## Mục đích
Hiện cây mục lục (outline) của PDF ở panel trái, bấm để nhảy tới trang.

## File / module liên quan
- `Services/PdfService.Reading.cs` — `GetBookmarks`, `ReadBookmarks`, `DestPage`, `ReadAction`.
- `Models/ReadingModels.cs` — `BookmarkNode` (tiêu đề, trang, con).
- `Views/Panels/BookmarksPanel.xaml(.cs)` — `TreeView`, thông báo khi tài liệu không có mục lục.

## Logic chính
- Duyệt đệ quy `FPDFBookmark_GetFirstChild` / `FPDFBookmark_GetNextSibling`.
- Trang đích: `FPDFBookmark_GetDest` → `FPDFDest_GetDestPageIndex`; nếu không có dest thì đọc action GoTo.
- Chống vòng lặp / file hỏng: `HashSet` các handle đã thăm + giới hạn 20 000 nút.
- Bấm một mục → `DocumentView` cuộn tới trang đó. Panel nạp lại sau thao tác trang (xóa / thêm trang).

## Lưu ý / giới hạn
- Chỉ đọc (chưa thêm / sửa / xóa bookmark).
- Đích trỏ tới vị trí trong trang (XYZ) chỉ nhảy tới đầu trang.

## Cách test
- Tự động: `ReadingTests.Bookmarks_And_Links` (PDF fixture có outline 2 cấp).
- Thủ công: mở PDF có mục lục, panel trái → biểu tượng mục lục, bấm từng mục.

## Lịch sử thay đổi
| Ngày | Thay đổi | Lý do |
|------|----------|-------|
| 2026-10-03 | Tạo file log (dự kiến) | Nâng cấp UI tối + tính năng kiểu Acrobat |
| 2026-10-03 | Hoàn thành panel mục lục chỉ đọc, chống vòng lặp | Điều hướng tài liệu dài |
