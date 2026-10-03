using System.Windows;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>Đặt mật khẩu mở file + quyền (in, sao chép, sửa). Xem docs/protect.md.</summary>
public partial class ProtectDialog : FluentWindow
{
    public ProtectDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordBox1.Focus();
    }

    public SecurityOptions? Result { get; private set; }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string? error = null;
        if (PasswordBox1.Password.Length < 4)
        {
            error = Loc.Get("Protect_TooShort");
        }
        else if (PasswordBox1.Password != PasswordBox2.Password)
        {
            error = Loc.Get("Protect_Mismatch");
        }

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = new SecurityOptions(PasswordBox1.Password, OwnerBox.Password, PrintBox.IsChecked == true, CopyBox.IsChecked == true, EditBox.IsChecked == true);
        DialogResult = true;
    }
}
