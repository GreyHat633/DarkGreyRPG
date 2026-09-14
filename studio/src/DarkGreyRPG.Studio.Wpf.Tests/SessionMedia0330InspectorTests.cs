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
    public void ActorSwitchClearsVariantAtomicallyAndUndoRestoresVoiceAndVariant()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var image = "media/" + new string('a', 64) + ".png";
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(), actorItems:
            [new CanonicalStoryActorItem(new ActorResourceInfo("actor", "Actor", "", [], IndividualActorResource.ResourceType, image, [new("默认头像", image)]))]);
        inspector.SelectedSpeakerId = "actor";
        inspector.SelectedPortraitVariant = "默认头像";
        Assert.AreEqual("默认头像", editor.Host.Graph.Nodes.Single().Properties["portrait_variant"].GetString());
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
