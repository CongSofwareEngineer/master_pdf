using System.Reflection;
using PDFEditorApp.Services;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

public partial class AboutDialog : FluentWindow
{
    public AboutDialog()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        VersionText.Text = Loc.Format("About_Version", version);
    }
}
