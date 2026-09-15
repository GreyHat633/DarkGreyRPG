using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Separate authoring guidance from entered text, including across theme switches.</summary>
public static class AuthoringText
{
    private static readonly DependencyProperty ThemeForegroundProperty = DependencyProperty.RegisterAttached(
        "ThemeForeground", typeof(Brush), typeof(AuthoringText), new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty IsHelpProperty = DependencyProperty.RegisterAttached(
        "IsHelp", typeof(bool), typeof(AuthoringText), new PropertyMetadata(false, OnIsHelpChanged));

    public static bool GetIsHelp(DependencyObject element) => (bool)element.GetValue(IsHelpProperty);
    public static void SetIsHelp(DependencyObject element, bool value) => element.SetValue(IsHelpProperty, value);

    private static void OnIsHelpChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not TextBlock text || args.NewValue is not true) return;
        text.FontSize = 11;
        text.FontWeight = FontWeights.Normal;
        text.TextWrapping = TextWrapping.Wrap;
        BindTone(text, text, false);
    }

    internal static void BindPlaceholder(TextBlock label, TextBox owner) => BindTone(label, owner, true);

    private static void BindTone(TextBlock label, FrameworkElement owner, bool placeholder)
    {
        // Resolve through the real control, not the detached adorner's resource scope.
        owner.SetResourceReference(ThemeForegroundProperty, "TextFillColorPrimaryBrush");
        label.SetBinding(TextBlock.ForegroundProperty, new Binding
        {
            Source = owner, Path = new PropertyPath(ThemeForegroundProperty),
            Converter = ToneConverter.Instance, ConverterParameter = placeholder
        });
    }

    private sealed class ToneConverter : IValueConverter
    {
        public static readonly ToneConverter Instance = new();
        private static readonly Brush DarkHelp = Make(0xB8);
        private static readonly Brush DarkPlaceholder = Make(0x90);
        private static readonly Brush LightHelp = Make(0x66);
        private static readonly Brush LightPlaceholder = Make(0x88);
        private static Brush Make(byte tone)
        {
            var brush = new SolidColorBrush(Color.FromRgb(tone, tone, tone));
            brush.Freeze();
            return brush;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var color = (value as SolidColorBrush)?.Color ?? Colors.White;
            var darkTheme = color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722 >= 128;
            return parameter is true
                ? darkTheme ? DarkPlaceholder : LightPlaceholder
                : darkTheme ? DarkHelp : LightHelp;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
