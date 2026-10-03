using System.Windows.Data;
using System.Windows.Markup;
using PDFEditorApp.Services;

namespace PDFEditorApp.Views;

/// <summary>
/// Markup extension lấy chuỗi đa ngôn ngữ: <c>Text="{v:Tr Menu_File}"</c>.
/// Trả về binding động nên đổi ngôn ngữ cập nhật ngay, không cần khởi động lại. Xem docs/i18n.md.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
