# PDF Editor Desktop Application

## Thông tin Dự án

**Người phát triển**: Nguyễn Thị Bích Lý  
**MSSV**: 50.01.902.066  
**Lớp**: ST4 Ca 2  
**Ngày tạo**: 2026-10-03  

---

## Mô tả Dự án

Xây dựng một ứng dụng desktop Windows cho phép người dùng:
- **Đọc file PDF**: Mở và hiển thị các file PDF
- **Chỉnh sửa nội dung PDF**: Thay đổi, thêm, xóa hoặc sửa đổi nội dung trong PDF

---

## Yêu cầu Chức năng (Features)

### Chức năng Cơ bản
1. **Mở File PDF**
   - Hỗ trợ chọn file PDF từ máy tính
   - Hiển thị tất cả các trang PDF

2. **Xem PDF**
   - Hiển thị từng trang PDF rõ ràng
   - Chuyển đến trang trước/sau
   - Zoom in/out
   - Fit to screen

3. **Chỉnh sửa Văn bản**
   - Chỉnh sửa text hiện có trong PDF
   - Thêm text mới vào trang
   - Xóa text

4. **Quản lý Trang**
   - Xoay trang (90°, 180°, 270°)
   - Xóa trang
   - Thêm trang trắng

5. **Lưu & Export**
   - Lưu file PDF sau khi chỉnh sửa
   - Export PDF sang định dạng khác (nếu cần)

---

## Yêu cầu Kỹ thuật

### Nền tảng
- **OS**: Windows 10/11
- **Tập tin**: .exe (ứng dụng độc lập)

### Giao diện
- **Thiết kế**: Hiện đại, dễ sử dụng
- **Layout**: 
  - Thanh menu & toolbar ở trên
  - Bảng hiển thị PDF ở giữa
  - Panel properties bên phải (tùy chọn)

### Performance
- Tải file PDF nhanh (< 2 giây)
- Chỉnh sửa không lag

---

## Stack Công nghệ Đề xuất

### Lựa chọn 1: **.NET (C# + WPF)** ⭐ Khuyến nghị
- **Framework**: .NET Framework / .NET 6+
- **UI**: WPF (Windows Presentation Foundation)
- **Thư viện PDF**: iTextSharp hoặc PdfSharp
- **Ưu điểm**: Native Windows, hiệu suất tốt, dễ deploy
- **Tệp**: .csproj, .xaml

### Lựa chọn 2: **Electron + React/Vue + Node.js**
- **Framework**: Electron
- **UI**: React/Vue
- **Thư viện PDF**: pdf-lib, pdfjs
- **Ưu điểm**: Cross-platform, UI dễ đẹp
- **Nhược điểm**: File nặng hơn

### Lựa chọn 3: **Python + PyQt/Tkinter**
- **Framework**: PyQt6 hoặc Tkinter
- **Thư viện PDF**: PyPDF2, pypdf, reportlab
- **Ưu điểm**: Code nhanh, dễ học
- **Nhược điểm**: Hiệu suất chậm hơn

---

## Cấu trúc Dự án (Đề xuất - .NET)

```
PDFEditorApp/
├── PDFEditorApp.sln
├── PDFEditorApp/
│   ├── PDFEditorApp.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml          # UI chính
│   ├── MainWindow.xaml.cs       # Logic chính
│   ├── Models/
│   │   └── PDFDocument.cs       # Model cho PDF
│   ├── Services/
│   │   ├── PDFService.cs        # Xử lý PDF
│   │   └── FileService.cs       # Xử lý file
│   ├── Views/
│   │   ├── PDFViewer.xaml
│   │   └── EditorPanel.xaml
│   └── Resources/
│       └── Styles.xaml
├── packages.config              # Dependencies
└── README.md
```

---

## Các Dependencies Cần Thiết

### Nếu dùng .NET + iTextSharp:
```xml
<ItemGroup>
  <PackageReference Include="itext7" Version="8.0.x" />
  <PackageReference Include="pdfium.net" Version="5.x.x" />
</ItemGroup>
```

### Hoặc dùng PdfSharp:
```xml
<ItemGroup>
  <PackageReference Include="PdfSharp" Version="6.x.x" />
</ItemGroup>
```

---

## Chi tiết Chức năng Chi tiết

### 1. Module Mở File
```
- Mở dialog file browser
- Filter: *.pdf files only
- Load PDF vào memory
- Hiển thị thumbnails trang (tùy chọn)
```

### 2. Module Xem PDF
```
- Render trang hiện tại
- Navigation buttons (Previous/Next)
- Zoom controls (%, Fit, etc.)
- Hiển thị số trang hiện tại
```

### 3. Module Chỉnh sửa
```
- Click vào text trong PDF để chỉnh sửa
- Toolbar: Bold, Italic, Font size, Color
- Thêm text box mới
- Xóa text
- Thay đổi font family & size
```

### 4. Module Lưu
```
- Lưu PDF với tên mới (Save As)
- Lưu ghi đè file cũ (Save)
- Xác nhận trước khi lưu
- Hiển thị lỗi nếu có
```

---

## Lộ trình Phát triển

### Phase 1: Setup & Basic UI
- [ ] Tạo project .NET + WPF
- [ ] Thiết kế MainWindow
- [ ] Thêm menu & toolbar

### Phase 2: Mở & Xem PDF
- [ ] Implement file open dialog
- [ ] Integrate PDF library
- [ ] Render trang PDF
- [ ] Implement navigation

### Phase 3: Chỉnh sửa
- [ ] Implement text editing
- [ ] Thêm formatting controls
- [ ] Add/Delete text features

### Phase 4: Lưu & Polish
- [ ] Implement save functionality
- [ ] Error handling & validation
- [ ] Testing & bug fixes
- [ ] Build .exe installer

---

## Testing Checklist

- [ ] Mở file PDF thành công
- [ ] Hiển thị đúng tất cả trang
- [ ] Chỉnh sửa text không bị lỗi
- [ ] Zoom in/out hoạt động
- [ ] Lưu file không mất dữ liệu
- [ ] Xử lý file PDF lớn (> 50MB)
- [ ] Error messages rõ ràng

---

## Ghi chú Quan trọng

1. **Chọn công nghệ**: Nếu bạn đã có kinh nghiệm với C# từ dự án image-batch-editor-app, hãy dùng .NET
2. **PDF Library**: iTextSharp và PdfSharp đều tốt, nhưng iTextSharp mạnh hơn cho chỉnh sửa
3. **Performance**: Với PDF lớn, cân nhắc async loading để không freeze UI
4. **Testing**: Test với nhiều định dạng PDF (text-based, scanned, complex)

---

## Liên hệ & Support

Cho Claude Code biết nếu cần:
- Chi tiết hơn về chức năng nào
- Giúp với dependency management
- Code structure & best practices
- Debugging issues
