using System.Windows;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

/// <summary>Hỏi mật khẩu khi mở PDF được bảo vệ. Xem docs/open-file.md.</summary>
public partial class PasswordDialog : FluentWindow
{
    public PasswordDialog(string fileName, bool wrongPassword)
    {
        InitializeComponent();
        MessageText.Text = Loc.Format("Password_Message", fileName);
        ErrorText.Visibility = wrongPassword ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string Password => PasswordInput.Password;

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
