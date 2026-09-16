using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class LinePagesAuthoringTests
{
    [TestMethod]
    public void PagesKeepIdentitySettingsAndBothProjectionsThroughSingleUndoReorder()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var first = inline.LinePages.Single();
        Assert.IsFalse(first.CanRemove);
        Assert.IsFalse(first.Remove());
        first.Text = "第一句";
        var secondId = inline.AddLinePage();
        Assert.IsNotNull(secondId);
        var second = inline.LinePages.Last();
        second.Text = "第二句";
        second.CustomSpeed = true; second.Speed = 120; second.Volume = .35;
        second.AudioEnabled = true;
        Assert.IsTrue(inspector.LinePages.Last().AudioEnabled);
        Assert.IsFalse(inspector.LinePages.First().AudioEnabled);
        var voice = "media/" + new string('a', 64) + ".ogg";
        Assert.IsTrue(second.SetVoice(voice));
        var undo = editor.Host.Session.UndoCount;
        Assert.IsTrue(second.Move(0));
        Assert.AreEqual(undo + 1, editor.Host.Session.UndoCount);
        Assert.AreSame(second, inline.LinePages.First());
        Assert.AreEqual(secondId, inspector.LinePages.First().PageId);
        Assert.AreEqual(voice, inspector.LinePages.First().VoiceRef);
        Assert.AreEqual(.35, inspector.LinePages.First().Volume);
        Assert.AreEqual(120d, inspector.LinePages.First().Speed);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreSame(first, inline.LinePages.First());
        Assert.AreSame(second, inline.LinePages.Last());
        Assert.IsTrue(editor.Host.Redo());
        Assert.AreSame(second, inline.LinePages.First());
        Assert.IsTrue(second.Remove());
        Assert.IsFalse(inline.LinePages.Single().CanRemove);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(voice, inline.LinePages.First().VoiceRef);
    }

    [TestMethod]
    public void LegacyFirstPagePreservesConfigWhileNewPageHasDefaults()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "old");
        line.Properties.Remove("pages");
        line.Properties.Remove("pages");
        line.Properties["text"] = JsonSerializer.SerializeToElement("旧台词");
        line.Properties["text_speed"] = JsonSerializer.SerializeToElement(45);
        line.Properties["custom_text_speed"] = JsonSerializer.SerializeToElement(true);
        line.Properties["voice_volume"] = JsonSerializer.SerializeToElement(.4);
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var vm = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual("旧台词", vm.LinePages.Single().Text);
        Assert.AreEqual(45d, vm.LinePages.Single().Speed);
        vm.AddLinePage();
        Assert.AreEqual(.4, vm.LinePages.First().Volume);
        var added = vm.LinePages.Last();
        Assert.IsFalse(added.CustomSpeed); Assert.IsFalse(added.AudioEnabled); Assert.IsNull(added.PortraitVariant);
        Assert.AreEqual(30d, added.Speed); Assert.AreEqual(1d, added.Volume);
    }

    [STATestMethod]
    public void SpecialHeadersUseDistinctFrozenColorsAndOtherTypesKeepThemeAccent()
    {
        Assert.AreEqual("#FFF5B53D", NodeHeaderPalette.ForType("task")!.ToString());
        Assert.AreEqual("#FF65C3AD", NodeHeaderPalette.ForType("session")!.ToString());
        Assert.AreEqual("#FFE58A83", NodeHeaderPalette.ForType("story")!.ToString());
        Assert.IsNull(NodeHeaderPalette.ForType("line"));
        Assert.IsTrue(NodeHeaderPalette.Task.IsFrozen);
    }
}
