using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views;

public partial class ProjectGraphView : UserControl
{
    public ProjectGraphView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ConfigureStoryCreation();
        Loaded += (_, _) => ConfigureStoryCreation();
        Editor.NodeEditRequested += node =>
        {
            if (Model?.IsReferencedStory(node.NodeId) == true)
            { Help.IsExpanded = true; HelpText.Text = "引用故事只读。请从引用包资源列表查看，或导入到项目后编辑。"; }
            else Model?.OpenStoryFlow(node.NodeId);
        };
        Editor.SelectionChanged += (_, args) =>
        {
            Model?.SelectStory(args.Node?.NodeId);
            Help.IsExpanded = false;
            HelpText.Text = args.Node is { } node
                ? node.DisplayName + "\n故事端口来自内部公开边界。拖动端口连接故事，拖动已有连线端点改接，剪刀或按住 Ctrl 剪线。引用故事的输出连线由源包维护。Ctrl+Z/Y 撤销重做，双击进入故事。\n"
                    + string.Join("\n", node.Inputs.Concat(node.Outputs).Select(p => $"{(p.IsInput ? "输入" : "输出")} · {p.DisplayName}（{p.InterfaceKind}）"))
                : "选择故事查看其公开边界；故事内容通过双击进入后编辑。";
        };
        PreviewKeyDown += (_, e) =>
        {
            // The application owns cross-resource/file transactions (including group deletion).
            // A standalone view may still use its local graph history.
            if (Window.GetWindow(this)?.DataContext is ShellViewModel) return;
            if (Keyboard.Modifiers != ModifierKeys.Control || Model?.CanonicalHost is not { } host) return;
            if (e.Key == Key.Z) { host.Undo(); e.Handled = true; }
            if (e.Key == Key.Y) { host.Redo(); e.Handled = true; }
        };
    }
    private ProjectGraphViewModel? Model => DataContext as ProjectGraphViewModel;
    private void ConfigureStoryCreation()
    {
        Editor.RenameStoryGroupRequested = (key, name) => Model?.RenameStoryGroup(key, name);
        Editor.DeleteStoryGroupsRequested = keys => Model?.DeleteStoryGroupsRequested?.Invoke(keys) == true;
        Editor.ProjectStoryCreateRequested = Model?.CreateStoryRequested is null
            ? null : point => Model?.CreateStoryRequested?.Invoke(point.X, point.Y);
    }
}
