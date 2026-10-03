using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PDFEditorApp.Views;

/// <summary>Một thumbnail trang trong danh sách bên trái (ảnh render lười). Xem docs/viewer.md.</summary>
public sealed class ThumbnailItem(int index, double width, double height, double pageWidth) : INotifyPropertyChanged
{
    private ImageSource? _image;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; } = index;

    /// <summary>Chiều rộng hiển thị của trang (point) — để tính độ phân giải render.</summary>
    public double PageWidth { get; } = pageWidth;

    public int PageNumber => Index + 1;

    /// <summary>Kích thước khung (DIP), giữ đúng tỉ lệ trang.</summary>
    public double Width { get; } = width;

    public double Height { get; } = height;

    public ImageSource? Image
    {
        get => _image;
        set
        {
            if (!ReferenceEquals(_image, value))
            {
                _image = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
