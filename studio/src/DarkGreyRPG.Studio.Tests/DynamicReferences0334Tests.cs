using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class DynamicReferences0334Tests
{
    [TestMethod]
    public void ChoicePortLabelRemainsConsistentWithRenamedOption()
    {
        var text = DynamicContentText.Encode([new(Type: "actor_name", ActorId: "Old:actor")]);
        var node = new GraphNode("choice", "choice", "Choice", properties: new Dictionary<string, JsonElement>
        { ["options"] = JsonSerializer.SerializeToElement(new[] { new { option_id = "option", flow_port_id = "flow", display_text = text } }) });
        node.Ports.Add(new("flow", text, false, GraphInterfaceKind.Flow, 0));
        var rename = new ResourceRenameMap();
        rename.Add(DgrResourceKind.Actor, "Old:actor", "New:actor");
        var result = rename.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Session, "Old:session", "Session", new([node]))).Graph!.Nodes[0];
        Assert.AreEqual(result.Properties["options"][0].GetProperty("display_text").GetString(), result.Ports[0].DisplayName);
        CollectionAssert.AreEqual(new[] { "New:actor" }, DynamicContentText.ActorReferences(result.Ports[0].DisplayName).ToArray());
        Assert.AreEqual(text, node.Ports[0].DisplayName);
    }

    [TestMethod]
    public void NameReferencesKeepTheirTypesAcrossCopyAndIdRewrite()
    {
        var text = DynamicContentText.Encode([new(Text: "{Copper}"), new(Type: "actor_name", ActorId: "Old:same"), new(Type: "item_name", ItemId: "Old:same")]);
        var rename = new ResourceRenameMap();
        rename.Add(DgrResourceKind.Actor, "Old:same", "New:actor");
        rename.Add(DgrResourceKind.Item, "Old:same", "New:item");
        var node = new GraphNode("line", "line", "Line", properties: new Dictionary<string, JsonElement> { ["text"] = JsonSerializer.SerializeToElement(text) });
        var rewritten = rename.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Session, "Old:session", "Session", new([node])));
        var result = rewritten.Graph!.Nodes[0].Properties["text"].GetString()!;
        var parts = DynamicContentText.Parse(result);
        Assert.AreEqual("{Copper}", parts[0].Text);
        CollectionAssert.AreEqual(new[] { "New:actor" }, DynamicContentText.ActorReferences(result).ToArray());
        CollectionAssert.AreEqual(new[] { "New:item" }, DynamicContentText.ItemReferences(result).ToArray());
        Assert.AreEqual("{Copper}ActorItem", DynamicContentText.Resolve(result, p => p.Type == "actor_name" ? "Actor" : "Item"));
        Assert.AreEqual(text, node.Properties["text"].GetString());
    }

    [TestMethod]
    public void RenameUpdatesOnlyMarkedReferencesInTaskMetadataAndNodeProse()
    {
        var text = DynamicContentText.Encode([new(Text: "Old:coin"), new(Type: "item_count", ItemId: "Old:coin")]);
        var node = new GraphNode("objective", "objective", "Old:coin", properties: new Dictionary<string, JsonElement>
        { ["description"] = JsonSerializer.SerializeToElement(text) });
        var original = new GraphResourceEnvelope(GraphResourceKind.Task, "Old:task", "Old:coin", new([node]))
        { TaskMetadata = new CanonicalTaskMetadata(text) };
        var rename = new ResourceRenameMap(); rename.Add(DgrResourceKind.Item, "Old:coin", "New:coin");
        var rewritten = rename.Rewrite(original);
        var parts = DynamicContentText.Parse(rewritten.TaskMetadata!.Description);
        Assert.AreEqual("Old:coin", parts[0].Text); Assert.AreEqual("New:coin", parts[1].ItemId);
        Assert.AreEqual(rewritten.TaskMetadata.Description, rewritten.Graph!.Nodes[0].Properties["description"].GetString());
        Assert.AreEqual(text, original.TaskMetadata.Description);
    }

    [TestMethod]
    public void DynamicOnlyItemIsIncludedAndMissingDeclarationCannotOverwriteExport()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var title = GraphNodeFactory.Create(GraphScope.StoryFlow, "title", "title");
        title.Properties["main"] = JsonSerializer.SerializeToElement(DynamicContentText.Encode([new(Type: "item_count", ItemId: "Provider:coin")]));
        store.Stories.Create(new(GraphResourceKind.Story, "Author:story", "Dynamic", new([GraphNodeFactory.CreateStoryStart("start"), title])));
        var items = new DarkGreyRPG.Studio.Core.Items.ItemRepository(project.Root);
        items.SaveItem(items.CreateItem("Provider:coin", "Coin"));
        store.Memberships.Create(new("Author:story", ownedResources: new() { Items = ["Provider:coin"] }));
        var archive = Path.Combine(project.Root, "dynamic.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("Author:story", archive);
        Assert.HasCount(1, result.Manifest.RequiredResources.Items);
        var before = File.ReadAllBytes(archive);
        title.Properties["main"] = JsonSerializer.SerializeToElement(DynamicContentText.Encode([new(Type: "item_count", ItemId: "Provider:missing")]));
        store.Stories.Replace(new(GraphResourceKind.Story, "Author:story", "Dynamic", new([GraphNodeFactory.CreateStoryStart("start"), title])));
        Assert.Throws<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root).Build("Author:story", archive));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(archive));
    }
}
