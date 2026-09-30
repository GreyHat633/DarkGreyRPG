using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ContinuousLine0334Tests
{
    [TestMethod]
    public void InsertAfterMiddlePreservesDraftAndUndoIdentity()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var vm = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var a = vm.LinePages[0].PageId;
        var b = vm.AddLinePage();
        vm.LinePages[0].Text = "未失焦的正文";
        var inserted = vm.InsertLinePageAfter(a);
        CollectionAssert.AreEqual(new[] { a, inserted, b }, vm.LinePages.Select(p => p.PageId).ToArray());
        Assert.AreEqual("", vm.LinePages[1].Text);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual("未失焦的正文", vm.LinePages[0].Text);
        Assert.IsTrue(editor.Host.Redo());
        Assert.AreEqual(inserted, vm.LinePages[1].PageId);
    }

    [TestMethod]
    public void ShortcutProtectsCountAndNeverProtectsOriginalIdentity()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var vm = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var a = vm.LinePages[0].PageId;
        var b = vm.AddLinePage()!;
        var c = vm.AddLinePage()!;
        vm.LinePages[0].Text = "";
        vm.ReorderLinePage(a, 2);
        Assert.IsTrue(vm.RemoveEmptyLinePage(a));
        Assert.IsTrue(vm.RemoveEmptyLinePage(b));
        Assert.IsFalse(vm.RemoveEmptyLinePage(c));
        var d = vm.AddLinePage();
        vm.LinePages[0].Text = " ";
        Assert.IsFalse(vm.RemoveEmptyLinePage(c));
        vm.LinePages[0].Text = "";
        vm.LinePages[0].CustomSpeed = true;
        vm.LinePages[0].Speed = 42;
        Assert.IsTrue(vm.RemoveEmptyLinePage(c));
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(c, vm.LinePages[0].PageId);
        Assert.AreEqual(42d, vm.LinePages[0].Speed);
        Assert.IsTrue(vm.LinePages[0].CustomSpeed);
        Assert.AreEqual(d, vm.LinePages[1].PageId);
    }
}
