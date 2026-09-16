using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class SessionPresentation0330InspectorTests
{
    [TestMethod]
    public void MusicImportStopAndUndoAreAtomicAndLoopSupportsBothValues()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "music", "music");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        string audio = "media/" + new string('a', 64) + ".ogg";
        int before = editor.Host.Session.UndoCount;
        Assert.IsTrue(inspector.SetMusic(audio));
        Assert.AreEqual(before + 1, editor.Host.Session.UndoCount);
        Assert.AreEqual("play", editor.Host.Graph.Nodes.Single().Properties["operation"].GetString());
        inspector.MusicLoop = true;
        Assert.IsTrue(inspector.MusicLoop);
        inspector.MusicFadeIn = "2.5";
        Assert.AreEqual("2.5", inspector.MusicFadeIn);
        Assert.IsTrue(inspector.SetMusic(null));
        Assert.AreEqual(JsonValueKind.Null, editor.Host.Graph.Nodes.Single().Properties["media_ref"].ValueKind);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(audio, editor.Host.Graph.Nodes.Single().Properties["media_ref"].GetString());
        Assert.IsTrue(inspector.MusicLoop);
    }

    [TestMethod]
    public void MusicVolumeDraftCommitsOnceAndUndoRedoRestoresValue()
    {
        var music = GraphNodeFactory.Create(GraphScope.Session, "music", "music");
        music.Properties.Remove("volume"); // legacy music fixture
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([music])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual(1d, inspector.MusicVolumeValue, 0.0001);
        var before = editor.Host.Session.UndoCount;
        inspector.MusicVolumeDraft = 0.65;
        Assert.AreEqual(before, editor.Host.Session.UndoCount);
        Assert.AreEqual(0.65, inspector.MusicVolumeDisplayValue, 0.0001);
        inspector.CommitVolumePreview();
        Assert.AreEqual(before + 1, editor.Host.Session.UndoCount);
        Assert.AreEqual(0.65, editor.Host.Graph.Nodes.Single().Properties["volume"].GetDouble(), 0.0001);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(1d, inspector.MusicVolumeValue, 0.0001);
        Assert.IsTrue(editor.Host.Redo());
        Assert.AreEqual(0.65, inspector.MusicVolumeValue, 0.0001);
    }

    [TestMethod]
    public void FullScreenReplacementUndoAndInvalidTransformsPreservePreviousSnapshot()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        string image = "media/" + new string('b', 64) + ".png";
        var layers = JsonSerializer.SerializeToElement(new[] { new { media_ref = image, x = 0.5, y = 0.5, width = 0.5, height = 1, anchor_x = 0.5, anchor_y = 0.5, z = 0 } });
        Assert.IsTrue(inspector.SetScreenLayers(layers));
        Assert.IsTrue(inspector.SetScreenLayers(JsonSerializer.SerializeToElement(Array.Empty<object>())));
        Assert.AreEqual(0, inspector.ScreenLayers.GetArrayLength());
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual(layers.GetRawText(), inspector.ScreenLayers.GetRawText());
        using var invalid = JsonDocument.Parse(layers.GetRawText().Replace("\"width\":0.5", "\"width\":0"));
        Assert.IsFalse(inspector.SetScreenLayers(invalid.RootElement));
        Assert.AreEqual(layers.GetRawText(), inspector.ScreenLayers.GetRawText());
    }
}
