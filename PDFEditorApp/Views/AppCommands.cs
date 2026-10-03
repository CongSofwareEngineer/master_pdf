using System.Windows.Input;

namespace PDFEditorApp.Views;

/// <summary>
/// Lệnh của ứng dụng kèm phím tắt. Lệnh <see cref="Tool"/> nhận CommandParameter = tên <c>ToolId</c>;
/// lệnh <see cref="Action"/> nhận tên hành động (xem MainWindow.RunAction). Danh sách phím tắt: docs/ui-layout.md.
/// </summary>
public static class AppCommands
{
    // Tệp
    public static readonly RoutedUICommand NewDocument = Create(nameof(NewDocument), Key.N, ModifierKeys.Control);
    public static readonly RoutedUICommand Open = Create(nameof(Open), Key.O, ModifierKeys.Control);
    public static readonly RoutedUICommand Save = Create(nameof(Save), Key.S, ModifierKeys.Control);
    public static readonly RoutedUICommand SaveAs = Create(nameof(SaveAs), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand Export = Create(nameof(Export), Key.E, ModifierKeys.Control);
    public static readonly RoutedUICommand Print = Create(nameof(Print), Key.P, ModifierKeys.Control);
    public static readonly RoutedUICommand CloseDocument = Create(nameof(CloseDocument), Key.W, ModifierKeys.Control);
    public static readonly RoutedUICommand NextTab = Create(nameof(NextTab), Key.Tab, ModifierKeys.Control);
    public static readonly RoutedUICommand PreviousTab = Create(nameof(PreviousTab), Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand Settings = Create(nameof(Settings), Key.OemComma, ModifierKeys.Control);

    // Sửa
    public static readonly RoutedUICommand Undo = Create(nameof(Undo), Key.Z, ModifierKeys.Control);
    public static readonly RoutedUICommand Redo = Create(nameof(Redo), Key.Y, ModifierKeys.Control);
    public static readonly RoutedUICommand Find = Create(nameof(Find), Key.F, ModifierKeys.Control);
    public static readonly RoutedUICommand FindNext = Create(nameof(FindNext), Key.F3, ModifierKeys.None);
    public static readonly RoutedUICommand FindPrevious = Create(nameof(FindPrevious), Key.F3, ModifierKeys.Shift);
    public static readonly RoutedUICommand Cancel = Create(nameof(Cancel), Key.Escape, ModifierKeys.None);

    /// <summary>Chọn công cụ; CommandParameter = tên ToolId.</summary>
    public static readonly RoutedUICommand Tool = Create(nameof(Tool), null, ModifierKeys.None);

    /// <summary>Lệnh chung theo tên (watermark, tách file…); CommandParameter = tên hành động.</summary>
    public static readonly RoutedUICommand Action = Create(nameof(Action), null, ModifierKeys.None);

    public static readonly RoutedUICommand SelectTool = Create(nameof(SelectTool), Key.D1, ModifierKeys.Alt);
    public static readonly RoutedUICommand EditContentTool = Create(nameof(EditContentTool), Key.D2, ModifierKeys.Alt);
    public static readonly RoutedUICommand AddTextTool = Create(nameof(AddTextTool), Key.T, ModifierKeys.Control);
    public static readonly RoutedUICommand HighlightTool = Create(nameof(HighlightTool), Key.H, ModifierKeys.Control | ModifierKeys.Shift);

    // Xem
    public static readonly RoutedUICommand ZoomIn = Create(nameof(ZoomIn), Key.OemPlus, ModifierKeys.Control, Key.Add);
    public static readonly RoutedUICommand ZoomOut = Create(nameof(ZoomOut), Key.OemMinus, ModifierKeys.Control, Key.Subtract);
    public static readonly RoutedUICommand ActualSize = Create(nameof(ActualSize), Key.D1, ModifierKeys.Control);
    public static readonly RoutedUICommand FitPage = Create(nameof(FitPage), Key.D0, ModifierKeys.Control);
    public static readonly RoutedUICommand FitWidth = Create(nameof(FitWidth), Key.D2, ModifierKeys.Control);
    public static readonly RoutedUICommand FirstPage = Create(nameof(FirstPage), Key.Home, ModifierKeys.Control);
    public static readonly RoutedUICommand PreviousPage = Create(nameof(PreviousPage), Key.PageUp, ModifierKeys.Control);
    public static readonly RoutedUICommand NextPage = Create(nameof(NextPage), Key.PageDown, ModifierKeys.Control);
    public static readonly RoutedUICommand LastPage = Create(nameof(LastPage), Key.End, ModifierKeys.Control);

    // Trang
    public static readonly RoutedUICommand RotateRight = Create(nameof(RotateRight), Key.R, ModifierKeys.Control);
    public static readonly RoutedUICommand RotateLeft = Create(nameof(RotateLeft), Key.R, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand InsertBlankPage = Create(nameof(InsertBlankPage), Key.Enter, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand DeletePage = Create(nameof(DeletePage), Key.Delete, ModifierKeys.Control);
    public static readonly RoutedUICommand DuplicatePage = Create(nameof(DuplicatePage), Key.D, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand ExtractPages = Create(nameof(ExtractPages), null, ModifierKeys.None);
    public static readonly RoutedUICommand MovePageUp = Create(nameof(MovePageUp), null, ModifierKeys.None);
    public static readonly RoutedUICommand MovePageDown = Create(nameof(MovePageDown), null, ModifierKeys.None);
    public static readonly RoutedUICommand ImportPages = Create(nameof(ImportPages), Key.I, ModifierKeys.Control | ModifierKeys.Shift);

    private static RoutedUICommand Create(string name, Key? key, ModifierKeys modifiers, Key? alternateKey = null)
    {
        var gestures = new InputGestureCollection();
        if (key is { } k)
        {
            gestures.Add(new KeyGesture(k, modifiers));
        }

        if (alternateKey is { } alt)
        {
            gestures.Add(new KeyGesture(alt, modifiers));
        }

        // Text của command không hiển thị (giao diện dùng chuỗi từ Strings.resx).
        return new RoutedUICommand(string.Empty, name, typeof(AppCommands), gestures);
    }
}
