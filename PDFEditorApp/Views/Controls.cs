using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;

namespace PDFEditorApp.Views;

// Các nút giao diện kiểu Fluent của app (template trong Resources/Styles.xaml). Xem docs/ui-layout.md.

/// <summary>Nút lệnh trên ribbon: icon + nhãn bên dưới.</summary>
public class RibbonButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(SymbolRegular), typeof(RibbonButton), new PropertyMetadata(SymbolRegular.Empty));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(RibbonButton), new PropertyMetadata(string.Empty));

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>Nút công cụ (bật / tắt) trên ribbon.</summary>
public class RibbonToggle : ToggleButton
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(SymbolRegular), typeof(RibbonToggle), new PropertyMetadata(SymbolRegular.Empty));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(RibbonToggle), new PropertyMetadata(string.Empty));

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>Nút chỉ có icon (thanh nổi, panel).</summary>
public class IconButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(SymbolRegular), typeof(IconButton), new PropertyMetadata(SymbolRegular.Empty));

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }
}

/// <summary>Nút bật / tắt chỉ có icon (thanh bên trái, thanh nổi).</summary>
public class IconToggle : ToggleButton
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(SymbolRegular), typeof(IconToggle), new PropertyMetadata(SymbolRegular.Empty));

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }
}

/// <summary>Mục chọn nhóm công cụ của ribbon (Xem, Chỉnh sửa, Nhận xét…).</summary>
public class RibbonTab : RadioButton
{
}

/// <summary>Ô màu tròn để chọn nhanh màu công cụ.</summary>
public class ColorSwatch : RadioButton
{
}
