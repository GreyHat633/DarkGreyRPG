using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalGraphEditorView
{
    private CanonicalStoryWorkspaceViewModel? ClipboardWorkspace => FindAncestor<CanonicalStoryWorkspaceView>(this)?.Workspace;
    private CanonicalGraphClipboard? Clipboard => ClipboardWorkspace?.Clipboard;

    public bool CopySelectedNodes()
    {
        if (Host is not { Scope: not GraphScope.Project } host || Clipboard is not { } clipboard || _selectedNodes.Count == 0) return false;
        try { clipboard.CopyFrom(ClipboardWorkspace!, _selectedNodes.Select(n => n.NodeId), false); return true; }
        catch (Exception exception) { ClipboardMessage(exception.Message); return false; }
    }

    public bool PasteNodes(Point point)
    {
        if (IsReadOnly || Host is not { Scope: not GraphScope.Project } host || Clipboard is not { Nodes: { } snapshot } clipboard) return false;
        try
        {
            var pasted = snapshot.CloneForPaste(host.Scope, out var ids);
            if (pasted.Nodes.Count == 0) return false;
            var resources = ClipboardWorkspace!.PrepareCopiedResources(pasted, ids);
            var metadata = ClipboardWorkspace.PrepareScreenMetadata(host, ids, false);
            var next = GraphDocument.FromJson(host.Graph.ToJson());
            var check = new GraphEditSession(next, host.Scope);
            foreach (var node in pasted.Nodes)
                if (!check.AddNode(node)) throw new InvalidOperationException(string.Join("\n", check.LastValidationIssues.Select(i => i.Message)));
            foreach (var connection in pasted.Connections)
                if (!check.Connect(connection)) throw new InvalidOperationException(string.Join("\n", check.LastValidationIssues.Select(i => i.Message)));
            var layout = host.Layout.ToDictionary(p => p.Key, p => p.Value);
            var minX = clipboard.Layout.Count == 0 ? 0 : clipboard.Layout.Values.Min(p => p.X);
            var minY = clipboard.Layout.Count == 0 ? 0 : clipboard.Layout.Values.Min(p => p.Y);
            double offset = clipboard.PasteCount * 24;
            foreach (var pair in ids)
            {
                var old = clipboard.Layout.GetValueOrDefault(pair.Key);
                layout[pair.Value] = new(point.X + old.X - minX + offset, point.Y + old.Y - minY + offset);
            }
            void Redo() { resources.Redo(); try { metadata.Redo(); } catch { resources.Undo(); throw; } }
            void Undo() { metadata.Undo(); try { resources.Undo(); } catch { metadata.Redo(); throw; } }
            var frames = host.FrameSnapshot().Concat(clipboard.Frames.Select(frame => frame with
            {
                Id = Guid.NewGuid().ToString("N"),
                X = point.X + frame.X - minX + offset,
                Y = point.Y + frame.Y - minY + offset,
                Members = frame.Members.Where(ids.ContainsKey).Select(id => ids[id]).ToArray()
            })).ToArray();
            host.CommitClipboardSnapshot(next, layout, Undo, Redo, frames);
            clipboard.PasteCount++;
            return true;
        }
        catch (Exception exception) { ClipboardMessage(exception.Message); return false; }
    }

    public bool CanPasteParameters(GraphEditorNodeViewModel? node) => !IsReadOnly && node is not null && Host is { } host
        && Clipboard?.Parameters is { } snapshot && snapshot.Scope == host.Scope && snapshot.Read().Nodes.SingleOrDefault()?.Type == node.Type;

    public void CopyNodeParameters(GraphEditorNodeViewModel node)
    {
        if (Host is { Scope: not GraphScope.Project } host && Clipboard is { } clipboard)
        {
            try { clipboard.CopyFrom(ClipboardWorkspace!, [node.NodeId], true); }
            catch (Exception exception) { ClipboardMessage(exception.Message); }
        }
    }

    public bool PasteNodeParameters(GraphEditorNodeViewModel node)
    {
        if (!CanPasteParameters(node) || Host is not { } host || Clipboard?.Parameters is not { } snapshot) return false;
        var source = snapshot.Read().Nodes.Single();
        if (source.Type is "session" or "task")
        {
            try { return ClipboardWorkspace!.PasteAggregateParameters(node.NodeId, message => MessageBox.Show(Window.GetWindow(this), message, "确认参数粘贴", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK); }
            catch (Exception error) { ClipboardMessage(error.Message); return false; }
        }
        var graph = GraphClipboardSnapshot.PasteParameters(host.Scope, host.Graph, source, node.NodeId, out var removed);
        if (removed.Count > 0 && MessageBox.Show(Window.GetWindow(this), "以下连线无法匹配新端口，将被移除：\n" +
            string.Join("\n", removed.Select(c => $"{c.FromNodeId}/{c.FromPortId} → {c.ToNodeId}/{c.ToPortId}")),
            "确认参数粘贴", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return false;
        var metadata = ClipboardWorkspace!.PrepareScreenMetadata(host, new Dictionary<string, string> { [source.Id] = node.NodeId }, true);
        host.CommitClipboardSnapshot(graph, host.Layout, metadata.Undo, metadata.Redo);
        return true;
    }

    public void AddParameterMenuItems(ContextMenu menu, GraphEditorNodeViewModel? node)
        => AddCopyPasteMenus(menu, node, null);

    private void AddCopyPasteMenus(ContextMenu menu, GraphEditorNodeViewModel? node, Point? point)
    {
        var copy = FluentContextMenuFactory.CreateSubmenu("复制");
        var paste = FluentContextMenuFactory.CreateSubmenu("粘贴");
        if (point is { } location)
        {
            copy.Items.Add(FluentContextMenuFactory.CreateItem("节点", () => CopySelectedNodes(), _selectedNodes.Count > 0));
            paste.Items.Add(FluentContextMenuFactory.CreateItem("节点", () => PasteNodes(location), !IsReadOnly && Clipboard?.Nodes is not null));
        }
        copy.Items.Add(FluentContextMenuFactory.CreateItem("参数", () => { if (node is not null) CopyNodeParameters(node); }, node is not null && Host?.Scope != GraphScope.Project));
        paste.Items.Add(FluentContextMenuFactory.CreateItem("参数", () => { if (node is not null) PasteNodeParameters(node); }, CanPasteParameters(node)));
        menu.Items.Add(copy); menu.Items.Add(paste);
    }

    private void AddClipboardMenuItems(ContextMenu menu, GraphEditorNodeViewModel? node, Point point)
    {
        if (Host?.Scope == GraphScope.Project) return;
        menu.Items.Add(new Separator());
        AddCopyPasteMenus(menu, node, point);
    }

    private void ClipboardMessage(string message) => MessageBox.Show(Window.GetWindow(this), message, "复制与粘贴", MessageBoxButton.OK, MessageBoxImage.Information);
}
