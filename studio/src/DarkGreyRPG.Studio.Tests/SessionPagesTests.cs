using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class SessionPagesTests
{
    [TestMethod]
    public void EmptyPagesSurviveSaveReloadAndClipboard()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var graph = new GraphDocument([line]);
        var edits = new GraphEditSession(graph, GraphScope.Session);
        Assert.IsTrue(edits.SetNodeProperty("line", "pages", JsonSerializer.SerializeToElement(Array.Empty<object>())));
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", graph);
        var restored = GraphResourceEnvelopeSerializer.Deserialize(GraphResourceEnvelopeSerializer.Serialize(envelope));
        Assert.IsEmpty(CanonicalSessionLineSchema.ReadPages(restored.Graph!.Nodes.Single()));
        var clipboard = new GraphClipboardSnapshot(GraphScope.Session, graph, ["line"]);
        var pasted = clipboard.CloneForPaste(GraphScope.Session, out _).Nodes.Single();
        Assert.IsEmpty(CanonicalSessionLineSchema.Validate(pasted));
        Assert.IsEmpty(CanonicalSessionLineSchema.ReadPages(pasted));
    }
    [TestMethod]
    public void LegacySaveMigratesAllSettingsWithoutMutatingSource()
    {
        var line = new GraphNode("line", "line", "台词", [], new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement("第一句😀\n第二行"),
            ["speaker_actor_id"] = JsonSerializer.SerializeToElement("npc"),
            ["portrait_variant"] = JsonSerializer.SerializeToElement("happy"),
            ["voice_ref"] = JsonSerializer.SerializeToElement("media/" + new string('a', 64) + ".ogg"),
            ["voice_volume"] = JsonSerializer.SerializeToElement(.4),
            ["text_speed"] = JsonSerializer.SerializeToElement(87),
            ["custom_text_speed"] = JsonSerializer.SerializeToElement(true),
        });
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line]));
        var restored = GraphResourceEnvelopeSerializer.Deserialize(GraphResourceEnvelopeSerializer.Serialize(envelope));
        var saved = restored.Graph!.Nodes.Single();
        Assert.IsFalse(saved.Properties.ContainsKey("text"));
        Assert.IsTrue(line.Properties.ContainsKey("text"));
        var page = CanonicalSessionLineSchema.ReadPages(saved).Single();
        foreach (var field in line.Properties.Where(p => p.Key != "speaker_actor_id"))
            Assert.IsTrue(JsonElement.DeepEquals(field.Value, page[field.Key]), field.Key);
        Assert.AreEqual("line:page:0", page["page_id"].GetString());
        Assert.IsEmpty(CanonicalSessionLineSchema.Validate(saved));
    }

    [TestMethod]
    public void InvalidPagesAndDuplicateIdsAreRejected()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        foreach (var json in new[] { "null", "[null]", "[{\"page_id\":\"a\",\"text\":\"x\"},{\"page_id\":\"a\",\"text\":\"y\"}]", "[{\"page_id\":\"a\",\"text\":\"x\",\"text_speed\":121}]" })
        {
            line.Properties["pages"] = JsonDocument.Parse(json).RootElement.Clone();
            Assert.IsNotEmpty(CanonicalSessionLineSchema.Validate(line), json);
            var clipboard = new GraphClipboardSnapshot(GraphScope.Session, new GraphDocument([line]), [line.Id]);
            var failure = Assert.ThrowsExactly<InvalidOperationException>(() => clipboard.CloneForPaste(GraphScope.Session, out _));
            StringAssert.Contains(failure.Message, "台词句子数据无效");
        }
    }

    [TestMethod]
    public void InvalidPageEditLeavesDocumentAndUndoHistoryUntouched()
    {
        var graph = new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "line")]);
        var edits = new GraphEditSession(graph, GraphScope.Session);
        var before = graph.ToJson();
        foreach (var json in new[] { "null", "[3]", "[{\"page_id\":\"a\",\"text\":1}]" })
        {
            Assert.IsFalse(edits.SetNodeProperty("line", "pages", JsonDocument.Parse(json).RootElement.Clone()));
            Assert.AreEqual(before, graph.ToJson());
            Assert.AreEqual(0, edits.UndoCount);
            Assert.IsTrue(edits.LastValidationIssues.All(issue => issue.Code == "graph.session.line"));
        }
    }

    [TestMethod]
    public void ClipboardCopiesEveryPageWithFreshIdentityAndDetachedSettings()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var pages = Enumerable.Range(0, 3).Select(i => CanonicalSessionLineSchema.CreatePage("page" + i)).ToArray();
        for (var i = 0; i < pages.Length; i++) pages[i]["text"] = JsonSerializer.SerializeToElement("句子" + i);
        line.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
        var clipboard = new GraphClipboardSnapshot(GraphScope.Session, new GraphDocument([line]), ["line"]);
        var pasted = clipboard.CloneForPaste(GraphScope.Session, out _).Nodes.Single();
        var copied = CanonicalSessionLineSchema.ReadPages(pasted);
        Assert.AreEqual(3, copied.Count);
        for (var i = 0; i < copied.Count; i++)
        {
            Assert.AreNotEqual(pages[i]["page_id"].GetString(), copied[i]["page_id"].GetString());
            Assert.AreEqual(pages[i]["text"].GetString(), copied[i]["text"].GetString());
        }
        copied[0]["text"] = JsonSerializer.SerializeToElement("changed");
        Assert.AreEqual("句子0", CanonicalSessionLineSchema.ReadPages(line)[0]["text"].GetString());
    }

    [TestMethod]
    public void MalformedPagesCannotCrashSpeakerChangeOrMutateHistory()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["pages"] = JsonSerializer.SerializeToElement(42);
        var graph = new GraphDocument([line]);
        var edits = new GraphEditSession(graph, GraphScope.Session);
        var before = graph.ToJson();
        Assert.IsFalse(edits.ChangeSessionSpeaker("line", "new_actor"));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(0, edits.UndoCount);
        Assert.IsNotEmpty(edits.LastValidationIssues);
    }

    [TestMethod]
    public void ParameterPasteRoundTripsEveryPageSettingAndPreservesTargetConnections()
    {
        var source = GraphNodeFactory.Create(GraphScope.Session, "line", "source");
        var target = GraphNodeFactory.Create(GraphScope.Session, "line", "target");
        var pages = Enumerable.Range(0, 3).Select(i =>
        {
            var page = CanonicalSessionLineSchema.CreatePage("source_page_" + i);
            page["text"] = JsonSerializer.SerializeToElement("句子" + i);
            page["portrait_variant"] = JsonSerializer.SerializeToElement("portrait_" + i);
            page["voice_ref"] = JsonSerializer.SerializeToElement("media/" + new string((char)('a' + i), 64) + ".ogg");
            page["voice_volume"] = JsonSerializer.SerializeToElement(.25 * i);
            page["text_speed"] = JsonSerializer.SerializeToElement(30 * i);
            page["custom_text_speed"] = JsonSerializer.SerializeToElement(i > 0);
            return page;
        }).ToArray();
        source.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
        var graph = new GraphDocument([source, target], [new GraphConnection("source", "flow_out", "target", "flow_in", GraphInterfaceKind.Flow)]);
        var pasted = GraphClipboardSnapshot.PasteParameters(GraphScope.Session, graph, source, "target", out var removed);
        Assert.IsEmpty(removed);
        Assert.AreEqual("target", pasted.Connections.Single().ToNodeId);
        var roundTrip = GraphResourceEnvelopeSerializer.Deserialize(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", pasted).ToJson()).Graph!;
        var targetPages = CanonicalSessionLineSchema.ReadPages(roundTrip.Nodes.Single(n => n.Id == "target"));
        Assert.HasCount(3, targetPages);
        for (var i = 0; i < pages.Length; i++)
        {
            Assert.AreNotEqual(pages[i]["page_id"].GetString(), targetPages[i]["page_id"].GetString());
            foreach (var field in pages[i].Where(p => p.Key != "page_id"))
                Assert.IsTrue(JsonElement.DeepEquals(field.Value, targetPages[i][field.Key]), field.Key);
        }
        targetPages[0]["text"] = JsonSerializer.SerializeToElement("changed");
        Assert.AreEqual("句子0", CanonicalSessionLineSchema.ReadPages(source)[0]["text"].GetString());
        Assert.HasCount(1, CanonicalSessionLineSchema.ReadPages(target));
    }

    [TestMethod]
    public void SpeakerChangeClearsEveryPortraitAndUndoRestoresAllPageSettings()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("old_actor");
        var pages = new[] { CanonicalSessionLineSchema.CreatePage(), CanonicalSessionLineSchema.CreatePage() };
        foreach (var page in pages) page["portrait_variant"] = JsonSerializer.SerializeToElement("happy");
        pages[1]["voice_volume"] = JsonSerializer.SerializeToElement(.25);
        line.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
        var graph = new GraphDocument([line]);
        var edits = new GraphEditSession(graph, GraphScope.Session);
        Assert.IsTrue(edits.ChangeSessionSpeaker("line", "new_actor"));
        Assert.IsTrue(CanonicalSessionLineSchema.ReadPages(graph.Nodes.Single()).All(p => !p.ContainsKey("portrait_variant")));
        Assert.AreEqual(.25, CanonicalSessionLineSchema.ReadPages(graph.Nodes.Single())[1]["voice_volume"].GetDouble());
        Assert.AreEqual(1, edits.UndoCount);
        Assert.IsTrue(edits.Undo());
        Assert.IsTrue(CanonicalSessionLineSchema.ReadPages(graph.Nodes.Single()).All(p => p["portrait_variant"].GetString() == "happy"));
        Assert.AreEqual("old_actor", graph.Nodes.Single().Properties["speaker_actor_id"].GetString());
        Assert.IsTrue(edits.Redo());
        Assert.IsTrue(CanonicalSessionLineSchema.ReadPages(graph.Nodes.Single()).All(p => !p.ContainsKey("portrait_variant")));
    }

    [TestMethod]
    public void ExportValidatesPortraitVariantOnLaterPage()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        var actors = new ActorRepository(project.Root);
        var actor = actors.CreateIndividual("npc", "NPC"); actor.HomeStoryId = "story"; actors.SaveActor(actor);
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("npc");
        var pages = new[] { CanonicalSessionLineSchema.CreatePage(), CanonicalSessionLineSchema.CreatePage() };
        pages[1]["portrait_variant"] = JsonSerializer.SerializeToElement("missing_later_variant");
        line.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
        var end = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        end.Properties["port_id"] = JsonSerializer.SerializeToElement("done");
        end.Properties["display_name"] = JsonSerializer.SerializeToElement("Done");
        var nodes = new[] { GraphNodeFactory.Create(GraphScope.Session, "start", "start"), line, end };
        var edges = nodes.Zip(nodes.Skip(1), (a, b) => new GraphConnection(a.Id, "flow_out", b.Id, "flow_in", GraphInterfaceKind.Flow));
        store.Sessions.Create(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument(nodes, edges)));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("story")
        { OwnedResources = new CanonicalStoryMembershipSet { Actors = ["npc"], Sessions = ["session"] } });
        var exception = Assert.ThrowsExactly<StoryPackageException>(() =>
            new DgrsStoryPackageExporter(project.Root).Build("story", Path.Combine(project.Root, "story.dgrs")));
        StringAssert.Contains(exception.Message, "头像变体");
    }

    [TestMethod]
    public void StartupCollectionProtectsVoiceOnLaterPages()
    {
        using var project = new TestProjectDirectory();
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var pages = new[] { CanonicalSessionLineSchema.CreatePage(), CanonicalSessionLineSchema.CreatePage() };
        var reference = "media/" + new string('a', 64) + ".ogg";
        pages[1]["voice_ref"] = JsonSerializer.SerializeToElement(reference);
        line.Properties["pages"] = JsonSerializer.SerializeToElement(pages);
        var sessionPath = Path.Combine(project.Root, "resources/canonical/sessions/session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])).ToJson());
        var media = Path.Combine(project.Root, "resources", reference);
        Directory.CreateDirectory(Path.GetDirectoryName(media)!);
        File.WriteAllBytes(media, [1]);
        var orphan = Path.Combine(project.Root, "resources/media", new string('b', 64) + ".ogg");
        File.WriteAllBytes(orphan, [2]);
        Assert.AreEqual(1, ProjectMediaGarbageCollector.CollectAtStartup(project.Root).RuntimeFiles);
        Assert.IsTrue(File.Exists(media));
        Assert.IsFalse(File.Exists(orphan));
    }
}
