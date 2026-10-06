using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Stories;
namespace DarkGreyRPG.Studio.Tests;
[TestClass]
public sealed class StoryContentCopy0336Tests
{
    [TestMethod]
    public void AppendPreservesSourceAndExactUndoRedo()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        var source = stories.CreateNew("Source"); var target = stories.CreateNew("Target");
        var actors = new CanonicalStoryActorLifecycleService(store);
        var sourceActor = new ResourceAddress(StoryUid.Parse(source.Id), ResourceKind.Actor, "same").ToKey();
        var targetActor = new ResourceAddress(StoryUid.Parse(target.Id), ResourceKind.Actor, "same").ToKey();
        actors.CreateOwned(source.Id, CanonicalStoryActorKind.Individual, sourceActor, "Source actor");
        actors.CreateOwned(target.Id, CanonicalStoryActorKind.Individual, targetActor, "Target actor");
        var sourceBytes = File.ReadAllBytes(store.Stories.GetPath(source.Id));
        var targetBytes = File.ReadAllBytes(store.Stories.GetPath(target.Id));
        var actorBytes = File.ReadAllBytes(new ActorRepository(project.Root).GetActorPath(targetActor));
        var copy = new StoryContentCopyService();
        var plan = copy.Prepare(project.Root, source.Id, target.Id);
        var copiedActor = plan.ResourceMap.Resolve(ResourceAddress.FromKey(sourceActor));
        Assert.AreNotEqual(targetActor, copiedActor.ToKey());
        copy.Apply(plan);
        Assert.AreEqual(2, store.Stories.Load(target.Id).Graph!.Nodes.Count(node => node.Type == "start"));
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(store.Stories.GetPath(source.Id)));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(new ActorRepository(project.Root).GetActorPath(targetActor)));
        Assert.AreEqual(target.Id, new ActorRepository(project.Root).LoadActor(copiedActor.ToKey()).ToResource().HomeStoryId);
        var applied = File.ReadAllBytes(store.Stories.GetPath(target.Id));
        copy.Undo(plan);
        CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(store.Stories.GetPath(target.Id)));
        Assert.AreEqual(2, new ActorRepository(project.Root).ListActors().Count);
        copy.Redo(plan);
        CollectionAssert.AreEqual(applied, File.ReadAllBytes(store.Stories.GetPath(target.Id)));
        Assert.ThrowsExactly<InvalidOperationException>(() => copy.Prepare(project.Root, source.Id, source.Id));
    }
    [TestMethod]
    public void CopiesUnplacedResourcesTypedReferencesAndEditorLayout()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        var source = stories.CreateNew("Source"); var target = stories.CreateNew("Target"); var external = stories.CreateNew("External");
        string Address(string owner, ResourceKind kind) => new ResourceAddress(StoryUid.Parse(owner), kind, "same").ToKey();
        var actors = new CanonicalStoryActorLifecycleService(store);
        var actor = Address(source.Id, ResourceKind.Actor); var externalActor = Address(external.Id, ResourceKind.Actor);
        actors.CreateOwned(source.Id, CanonicalStoryActorKind.Individual, actor, "Actor");
        actors.CreateOwned(external.Id, CanonicalStoryActorKind.Individual, externalActor, "External actor");
        actors.AddReference(source.Id, externalActor);
        var session = Address(source.Id, ResourceKind.Session);
        new CanonicalStoryResourceLifecycleService(store).CreateOwned(source.Id, GraphResourceKind.Session, session, "Unplaced session");
        var envelope = store.Sessions.Load(session);
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(actor);
        envelope.Graph = new GraphDocument(envelope.Graph!.Nodes.Append(line));
        store.Sessions.Replace(envelope);
        var layouts = new CanonicalGraphLayoutStore(project.Root);
        var sourceNode = source.Graph!.Nodes.First().Id;
        layouts.Save(GraphResourceKind.Story, source.Id, new Dictionary<string, ProjectGraphNodeLayout> { [sourceNode] = new() { X = 50, Y = 80 } });
        layouts.SaveFrames(CanonicalGraphLayoutStore.BuildGraphKey(GraphResourceKind.Story, source.Id), [new("frame", "Frame", 0, 0, 400, 300, [sourceNode])]);
        var service = new StoryContentCopyService();
        var plan = service.Prepare(project.Root, source.Id, target.Id);
        service.Apply(plan);
        var copiedSession = plan.ResourceMap.Resolve(ResourceAddress.FromKey(session)).ToKey();
        var copiedActor = plan.ResourceMap.Resolve(ResourceAddress.FromKey(actor)).ToKey();
        var copiedLine = store.Sessions.Load(copiedSession).Graph!.Nodes.Single(node => node.Type == "line");
        Assert.AreEqual(copiedActor, copiedLine.Properties["speaker_actor_id"].GetString());
        Assert.AreNotEqual("line", copiedLine.Id);
        CollectionAssert.Contains(store.Memberships.Load(target.Id).ReferencedResources.Actors, externalActor);
        var frame = layouts.LoadFrames(CanonicalGraphLayoutStore.BuildGraphKey(GraphResourceKind.Story, target.Id)).Single();
        Assert.AreNotEqual("frame", frame.Id);
        Assert.AreEqual(plan.StoryNodeIds[sourceNode], frame.Members.Single());
        Assert.IsGreaterThan(50d, layouts.Load(GraphResourceKind.Story, target.Id)[plan.StoryNodeIds[sourceNode]].X);
    }

    [TestMethod]
    public void FailureRestoresEveryFile()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        var source = stories.CreateNew("Source"); var target = stories.CreateNew("Target");
        var actor = new ResourceAddress(StoryUid.Parse(source.Id), ResourceKind.Actor, "actor").ToKey();
        new CanonicalStoryActorLifecycleService(store).CreateOwned(source.Id, CanonicalStoryActorKind.Individual, actor, "Actor");
        var writes = 0;
        var service = new StoryContentCopyService(new ProjectFileTransaction((path, bytes) =>
        {
            if (++writes == 2) throw new IOException("injected");
            File.WriteAllBytes(path, bytes);
        }));
        var plan = service.Prepare(project.Root, source.Id, target.Id);
        Assert.ThrowsExactly<IOException>(() => service.Apply(plan));
        foreach (var change in plan.Changes)
        {
            var path = Path.Combine(project.Root, change.RelativePath);
            if (change.ExpectedBytes is null) Assert.IsFalse(File.Exists(path));
            else CollectionAssert.AreEqual(change.ExpectedBytes, File.ReadAllBytes(path));
        }
    }
}
