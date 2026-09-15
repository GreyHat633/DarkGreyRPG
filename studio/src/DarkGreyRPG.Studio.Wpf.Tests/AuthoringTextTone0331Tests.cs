using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class AuthoringTextTone0331Tests
{
    [STATestMethod]
    public void GuidanceHasSmallNeutralTypographyAndTracksThemeWithoutRecreation()
    {
        var text = new TextBlock { Text = "播放结束后继续流程", FontSize = 14, FontWeight = FontWeights.Bold };
        text.Resources["TextFillColorPrimaryBrush"] = Brushes.White;
        AuthoringText.SetIsHelp(text, true);
        Assert.AreEqual(11.0, text.FontSize);
        Assert.AreEqual(FontWeights.Normal, text.FontWeight);
        Assert.AreEqual(TextWrapping.Wrap, text.TextWrapping);
        Assert.AreEqual(Color.FromRgb(0xB8, 0xB8, 0xB8), ((SolidColorBrush)text.Foreground).Color);
        text.Resources["TextFillColorPrimaryBrush"] = Brushes.Black;
        Assert.AreEqual(Color.FromRgb(0x66, 0x66, 0x66), ((SolidColorBrush)text.Foreground).Color);
        text.Resources["TextFillColorPrimaryBrush"] = Brushes.White;
        Assert.AreEqual(Color.FromRgb(0xB8, 0xB8, 0xB8), ((SolidColorBrush)text.Foreground).Color);
    }
}
