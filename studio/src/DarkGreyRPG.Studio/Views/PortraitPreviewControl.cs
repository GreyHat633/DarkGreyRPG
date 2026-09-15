using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views;

public sealed class PortraitPreviewControl : Image
{
    public PortraitPreviewControl() { Width = 96; Height = 96; Stretch = Stretch.Uniform; HorizontalAlignment = HorizontalAlignment.Left; }
    public static readonly DependencyProperty MediaRefProperty = DependencyProperty.Register(nameof(MediaRef), typeof(string), typeof(PortraitPreviewControl), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ProjectDirectoryProperty = DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(PortraitPreviewControl), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ActorProperty = DependencyProperty.Register(nameof(Actor), typeof(CanonicalStoryActorItem), typeof(PortraitPreviewControl), new PropertyMetadata(null, Changed));
    public string? MediaRef { get => (string?)GetValue(MediaRefProperty); set => SetValue(MediaRefProperty, value); }
    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }
    public CanonicalStoryActorItem? Actor { get => (CanonicalStoryActorItem?)GetValue(ActorProperty); set => SetValue(ActorProperty, value); }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args) => ((PortraitPreviewControl)target).Refresh();
    private void Refresh()
    {
        Source = null;
        Visibility = Visibility.Collapsed;
        if (MediaRef is not { } reference || !MediaReference.IsImage(reference)) return;
        try
        {
            byte[]? bytes = Actor?.Provider is { } provider
                ? OfflineDgrsPackageReader.Read(provider.PackagePath).Entries.GetValueOrDefault("resources/" + reference)
                : ProjectDirectory is { } root ? File.ReadAllBytes(Path.Combine(root, "resources", reference.Replace('/', Path.DirectorySeparatorChar))) : null;
            if (bytes is null) return;
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            Source = bitmap;
            Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException or InvalidOperationException) { }
    }
}
