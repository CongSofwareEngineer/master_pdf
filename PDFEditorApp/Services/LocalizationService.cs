using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace PDFEditorApp.Services;

/// <summary>
/// Lấy chuỗi giao diện theo key từ Resources/Strings*.resx và thông báo khi đổi ngôn ngữ.
/// XAML dùng <c>{v:Tr Key}</c> (binding tới indexer này), C# dùng <see cref="Loc"/>. Xem docs/i18n.md.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string English = "en";
    public const string Vietnamese = "vi";

    private static readonly ResourceManager Resources =
        new("PDFEditorApp.Resources.Strings", typeof(LocalizationService).Assembly);

    private LocalizationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public static LocalizationService Instance { get; } = new();

    public static IReadOnlyList<string> SupportedLanguages { get; } = [English, Vietnamese];

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(English);

    public string Language => Culture.TwoLetterISOLanguageName;

    public string this[string key] => Get(key);

    public string Get(string key) => Resources.GetString(key, Culture) ?? $"[{key}]";

    /// <summary>Ngôn ngữ mặc định khi chưa chọn: tiếng Việt (người dùng chính của app).</summary>
    public const string DefaultLanguage = Vietnamese;

    public void SetLanguage(string? language)
    {
        var code = SupportedLanguages.Contains(language ?? string.Empty) ? language! : DefaultLanguage;
        Culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Lối tắt lấy chuỗi đa ngôn ngữ trong C#.</summary>
public static class Loc
{
    public static string Get(string key) => LocalizationService.Instance.Get(key);

    public static string Format(string key, params object?[] args) =>
        string.Format(LocalizationService.Instance.Culture, Get(key), args);
}
