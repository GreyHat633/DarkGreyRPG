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
        Editor.NodeEditRequested += node =>
        {
            if (Model?.IsReferencedStory(node.NodeId) == true)
            { Help.IsExpanded = true; HelpText.Text = "引用故事只读。请从引用包资源列表查看，或导入到项目后编辑。"; }
            else Model?.OpenStoryFlow(node.NodeId);
        };
        Editor.SelectionChanged += (_, args) =>
        {
            Help.IsExpanded = false;
            HelpText.Text = args.Node is { } node
                ? node.DisplayName + "\n故事端口来自内部公开边界。流程驱动条目映射 Flow 输入，命名终止映射 Flow 输出，逻辑边界保持类型。拖动端口接线、选线 Delete 断开、Ctrl+Z/Y 撤销重做。双击进入故事编辑；本图只修改位置和连线。\n"
                    + string.Join("\n", node.Inputs.Concat(node.Outputs).Select(p => $"{(p.IsInput ? "输入" : "输出")} · {p.DisplayName}（{p.InterfaceKind}）"))
                : "选择故事查看其公开边界；故事内容通过双击进入后编辑。";
        };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || Model?.CanonicalHost is not { } host) return;
            if (e.Key == Key.Z) { host.Undo(); e.Handled = true; }
            if (e.Key == Key.Y) { host.Redo(); e.Handled = true; }
        };
    }
    private ProjectGraphViewModel? Model => DataContext as ProjectGraphViewModel;
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (Model is not null) Model.SearchText = Search.Text;
    }
    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Model?.CanonicalHost is not { } host) return;
        var node = host.Nodes.FirstOrDefault(n => n.DisplayName.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)
            || n.NodeId.Contains(Search.Text, StringComparison.OrdinalIgnoreCase));
        if (node is not null) Editor.FocusNode(node);
        e.Handled = true;
    }
}
