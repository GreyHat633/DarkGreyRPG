using System.Globalization;
using System.Windows.Data;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Keeps the resource-library selection visual tied to workspace identity.</summary>
internal sealed class CanonicalStorySelectionEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
