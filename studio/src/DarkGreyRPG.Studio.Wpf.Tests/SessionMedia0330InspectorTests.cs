using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class SessionMedia0330InspectorTests
{
    [TestMethod]
    public void TextSpeedOverrideDefaultsOffSynchronizesAndUndoes()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Session, "session", "Session", new([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsFalse(inline.IsLineTextSpeedCustom);
        bool notified = false;
        inspector.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(inspector.IsLineTextSpeedCustom)) notified = true; };
        inline.IsLineTextSpeedCustom = true; Assert.IsTrue(inspector.IsLineTextSpeedCustom); Assert.IsTrue(notified);
        inline.LineTextSpeed = 120;
        inline.IsLineTextSpeedCustom = false; Assert.IsFalse(inspector.IsLineTextSpeedCustom);
        Assert.AreEqual(120, inspector.LineTextSpeed);
        Assert.IsTrue(editor.Host.Undo()); Assert.IsTrue(inspector.IsLineTextSpeedCustom);
    }

    [TestMethod]
    public void AudioGateDefaultsClosedSharesEmptyStateAndSupportsUndo()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsFalse(inline.IsLineAudioEnabled);
        var before = editor.Host.Session.UndoCount;
        inline.IsLineAudioEnabled = true;
        Assert.IsTrue(inspector.IsLineAudioEnabled);
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
        var voice = "media/" + new string('b', 64) + ".ogg";
        Assert.IsTrue(inline.SetLineVoice(voice));
        inspector.IsLineAudioEnabled = false;
        Assert.IsNull(inline.LineVoiceRef);
        Assert.IsFalse(inline.IsLineAudioEnabled);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(voice, inline.LineVoiceRef);
        Assert.IsTrue(inspector.IsLineAudioEnabled);
        Assert.IsTrue(editor.Host.Redo());
        Assert.IsFalse(inspector.IsLineAudioEnabled);
        Assert.IsNull(inline.LineVoiceRef);
    }

    [TestMethod]
    public void ExistingVoiceOpensCardWithoutChangingDocument()
    {
        var voice = "media/" + new string('b', 64) + ".ogg";
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        using (var setup = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single())) setup.SetLineVoice(voice);
        var before = editor.Host.Session.UndoCount;
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsTrue(inspector.IsLineAudioEnabled);
        Assert.AreEqual(voice, inspector.LineVoiceRef);
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
    }

    [TestMethod]
    public void VoiceVolumeDraftCommitsOnceAndUndoRedoRestoresValue()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties.Remove("voice_volume"); // legacy line fixture
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual(1d, inspector.LineVoiceVolumeValue, 0.0001);
        var before = editor.Host.Session.UndoCount;
        inspector.LineVoiceVolumeDraft = 0.35;
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
        Assert.AreEqual(0.35, inspector.LineVoiceVolumeDisplayValue, 0.0001);
        inspector.CommitVolumePreview();
        Assert.AreEqual(before + 1, editor.Host.Session.UndoCount);
        Assert.AreEqual(0.35, editor.Host.Graph.Nodes.Single().Properties["pages"][0].GetProperty("voice_volume").GetDouble(), 0.0001);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(1d, inspector.LineVoiceVolumeValue, 0.0001);
        Assert.IsTrue(editor.Host.Redo());
        Assert.AreEqual(0.35, inspector.LineVoiceVolumeValue, 0.0001);
    }

    [STATestMethod]
    public void LivePortraitDropdownPreservesSelectionWhenActorAddsExpressions()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var image = "media/" + new string('a', 64) + ".png";
        var actor = new CanonicalStoryActorItem(new ActorResourceInfo("actor", "Actor", "", [], IndividualActorResource.ResourceType, image, [new("微笑", image)]));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), [actor]);
        inspector.SelectedSpeakerId = "actor";
        inspector.SelectedPortraitVariant = "微笑";
        var box = new System.Windows.Controls.ComboBox { DataContext = inspector, DisplayMemberPath = "DisplayName", SelectedValuePath = "Name" };
        box.SetBinding(System.Windows.Controls.ItemsControl.ItemsSourceProperty, "PortraitVariantOptions");
        box.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new System.Windows.Data.Binding("SelectedPortraitVariant") { Mode = System.Windows.Data.BindingMode.TwoWay });
        var before = editor.Host.Session.UndoCount;
        actor.UpdatePortraits(image, [new("微笑", image), new("生气", image)]);
        box.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        Assert.AreEqual("微笑", box.SelectedValue);
        Assert.AreEqual("微笑", inspector.SelectedPortraitVariant);
        Assert.HasCount(3, box.Items);
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
    }
    [TestMethod]
    public void PortraitEditsRefreshExistingInlineAndInspectorWithoutGraphEdits()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var actor = new CanonicalStoryActorItem(new ActorResourceInfo("actor", "Actor", "", [], IndividualActorResource.ResourceType));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), [actor]);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), [actor]);
        inline.SelectedSpeakerId = "actor";
        var portrait = "media/" + new string('c', 64) + ".png";
        var variant = "media/" + new string('d', 64) + ".png";
        var before = editor.Host.Session.UndoCount;
        actor.UpdatePortraits(portrait, [new("微笑", variant)]);
        Assert.HasCount(2, inline.PortraitVariantOptions);
        Assert.HasCount(2, inspector.PortraitVariantOptions);
        Assert.AreEqual(portrait, inspector.SelectedPortraitMediaRef);
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
        inline.SelectedPortraitVariant = "微笑";
        Assert.AreEqual(variant, inspector.SelectedPortraitMediaRef);
        actor.UpdatePortraits(variant, [new("微笑", portrait), new("生气", variant)]);
        Assert.AreEqual("微笑", inspector.SelectedPortraitVariant);
        Assert.AreEqual(portrait, inline.SelectedPortraitMediaRef);
        Assert.HasCount(3, inspector.PortraitVariantOptions);
    }
    [TestMethod]
    public void ActorSwitchClearsVariantAtomicallyAndUndoRestoresVoiceAndVariant()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var image = "media/" + new string('a', 64) + ".png";
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), actorItems:
            [new CanonicalStoryActorItem(new ActorResourceInfo("actor", "Actor", "", [], IndividualActorResource.ResourceType, image, [new("默认头像", image)]))]);
        inspector.SelectedSpeakerId = "actor";
        inspector.SelectedPortraitVariant = "默认头像";
        Assert.AreEqual("默认头像", editor.Host.Graph.Nodes.Single().Properties["pages"][0].GetProperty("portrait_variant").GetString());
        var voice = "media/" + new string('b', 64) + ".ogg";
        Assert.IsTrue(inspector.SetLineVoice(voice));
        var before = editor.Host.Session.UndoCount;
        inspector.SelectedSpeakerId = "";
        Assert.AreEqual(before + 1, editor.Host.Session.UndoCount);
        Assert.AreEqual(JsonValueKind.Null, editor.Host.Graph.Nodes.Single().Properties["speaker_actor_id"].ValueKind);
        Assert.IsFalse(editor.Host.Graph.Nodes.Single().Properties.ContainsKey("portrait_variant"));
        Assert.AreEqual(voice, inspector.LineVoiceRef);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual("actor", inspector.SelectedSpeakerId);
        Assert.AreEqual("默认头像", inspector.SelectedPortraitVariant);
        Assert.AreEqual(voice, inspector.LineVoiceRef);
    }
}
