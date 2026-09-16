using System.IO;
using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio;

public partial class MainWindow
{
    private async void ImportActorPortrait_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ActorEditorViewModel actor } button || DataContext is not ShellViewModel shell) return;
        var variant = Equals(button.Tag, "variant");
        var name = actor.PortraitVariantName;
        if (variant && !ValidatePortraitName(actor, name)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ProjectMediaStore.ImageFileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        button.IsEnabled = false;
        try
        {
            var toolRoot = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
            var store = new ProjectMediaStore(shell.ProjectDirectory, Path.Combine(toolRoot, "ffmpeg.exe"), Path.Combine(toolRoot, "ffprobe.exe"));
            var imported = await store.ImportImageAsync(dialog.FileName);
            if (variant)
            {
                if (ValidatePortraitName(actor, name)) actor.SetPortraitVariants([.. actor.PortraitVariants, new(name, imported.MediaRef)]);
            }
            else actor.DefaultPortraitRef = imported.MediaRef;
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "头像导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { button.IsEnabled = true; }
    }

    private bool ValidatePortraitName(ActorEditorViewModel actor, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && actor.PortraitVariants.All(value => value.Name != name)) return true;
        MessageBox.Show(this, "请输入非空且不重复的变体名称。", "头像变体", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void RemoveActorDefaultPortrait_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ActorEditorViewModel actor }) actor.DefaultPortraitRef = null;
    }

    private void RemoveActorPortrait_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ActorEditorViewModel actor } && actor.SelectedPortraitVariant is { } selected)
            actor.SetPortraitVariants(actor.PortraitVariants.Where(value => value != selected));
    }

    private void RenameActorPortrait_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ActorEditorViewModel actor } && actor.SelectedPortraitVariant is { } selected
            && ValidatePortraitName(actor, actor.PortraitVariantName))
            actor.SetPortraitVariants(actor.PortraitVariants.Select(value => value == selected ? value with { Name = actor.PortraitVariantName } : value));
    }
}
