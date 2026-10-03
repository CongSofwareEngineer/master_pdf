using System.Windows;
using System.Windows.Input;

namespace PDFEditorApp.Views.Tools;

/// <summary>
/// Công cụ chuột trên vùng xem (chọn, bàn tay, tô sáng, bút vẽ…). DocumentView chuyển sự kiện vào đây.
/// Hình xem trước vẽ trên <see cref="PageView.ToolLayer"/>. Xem docs/ui-layout.md.
/// </summary>
public abstract class Tool(IToolHost host)
{
    protected IToolHost Host { get; } = host;

    public virtual Cursor? Cursor => null;

    public virtual void Activate(DocumentView view)
    {
    }

    public virtual void Deactivate(DocumentView view)
    {
    }

    public virtual void OnMouseDown(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
    }

    public virtual void OnMouseMove(DocumentView view, PageHit? hit, Point hostPoint, MouseEventArgs e)
    {
    }

    public virtual void OnMouseUp(DocumentView view, PageHit? hit, Point hostPoint, MouseButtonEventArgs e)
    {
    }

    public virtual void OnMouseLeave(DocumentView view)
    {
    }

    /// <summary>Trả về true nếu đã xử lý phím.</summary>
    public virtual bool OnKeyDown(DocumentView view, KeyEventArgs e) => false;

    /// <summary>Vẽ lại phần của công cụ trên trang (trang vừa hiện / đổi zoom).</summary>
    public virtual void OnPageDecorate(DocumentView view, PageView page)
    {
    }
}
