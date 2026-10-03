using System.Windows;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>Hộp thoại nhập một chuỗi (ghi chú, phạm vi trang…), có kiểm tra hợp lệ tùy chọn.</summary>
public partial class TextInputDialog : FluentWindow
{
    private readonly Func<string, string?>? _validate;

    private TextInputDialog(string title, string prompt, string initial, bool multiline, Func<string, string?>? validate)
    {
        InitializeComponent();
        _validate = validate;
        Title = title;
        TitleBarControl.Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
        if (multiline)
        {
            InputBox.AcceptsReturn = true;
            InputBox.TextWrapping = TextWrapping.Wrap;
            InputBox.MinHeight = 100;
            InputBox.VerticalContentAlignment = VerticalAlignment.Top;
            OkButton.IsDefault = false;
        }

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    /// <summary>Trả về chuỗi đã nhập, null nếu hủy. <paramref name="validate"/> trả về thông báo lỗi hoặc null.</summary>
    public static string? Ask(Window? owner, string title, string prompt, string initial, bool multiline = false, Func<string, string?>? validate = null)
    {
        var dialog = new TextInputDialog(title, prompt, initial, multiline, validate) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.InputBox.Text : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_validate?.Invoke(InputBox.Text) is { } error)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }
}
