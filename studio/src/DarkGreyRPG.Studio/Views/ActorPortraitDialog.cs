using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public sealed class ActorPortraitDialog : Window
{
    public ActorPortraitDialog(ActorDocument document, string projectDirectory)
    {
        var editor = new ActorEditorViewModel(document);
        Title = $"头像与表情 · {document.DisplayName}";
        Width = 600; Height = 700; MinWidth = 470; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var dock = new DockPanel { Margin = new Thickness(24) };
        Content = dock;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "取消", IsCancel = true };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "保存头像", Margin = new Thickness(8, 0, 0, 0) };
        save.Click += (_, _) => { if (document.ValidationErrors.Count == 0) DialogResult = true; };
        footer.Children.Add(cancel); footer.Children.Add(save);
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        dock.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new ActorPortraitEditor { DataContext = editor, ProjectDirectory = projectDirectory }
        });
    }
}