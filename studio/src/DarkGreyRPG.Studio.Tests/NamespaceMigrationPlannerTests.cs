using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class NamespaceMigrationPlannerTests
{
    [TestMethod]
    public void GlobalCustomAndReturnGlobalPreserveOwnershipAndUpdateAllReferences()
    {
        var initial = Project();
        var migrated = NamespaceMigrationPlanner.ChangeGlobal(initial, "First").Result;
        CollectionAssert.AreEqual(new[] { "First:a", "First:b" }, migrated.Graphs.Select(g => g.Id).ToArray());
        Assert.AreEqual("First:actor", migrated.Memberships[1].ReferencedResources.Actors[0]);
        Assert.AreEqual("First:a", migrated.Logic.Connections[0].SourceStoryId);
        Assert.AreEqual("port_a", migrated.Logic.Connections[0].SourcePortId);
        Assert.AreEqual("actor", initial.Actors[0].Id);

        var custom = NamespaceMigrationPlanner.ChangeStory(migrated, "First:b", "Private").Result;
        Assert.IsTrue(custom.Policy!.IsCustom("Private:b"));
        Assert.AreEqual("First:actor", custom.Memberships[1].ReferencedResources.Actors[0]);
        var global = NamespaceMigrationPlanner.ChangeGlobal(custom, "Second").Result;
        CollectionAssert.AreEqual(new[] { "Second:a", "Private:b" }, global.Graphs.Select(g => g.Id).ToArray());
        Assert.AreEqual("Second:actor", global.Memberships[1].ReferencedResources.Actors[0]);
        Assert.AreEqual("Private:own", global.Actors[1].Id);
        var restored = NamespaceMigrationPlanner.ReturnToGlobal(global, "Private:b").Result;
        Assert.IsFalse(restored.Policy!.IsCustom("Second:b"));
        Assert.AreEqual("Second:own", restored.Actors[1].Id);
        Assert.AreEqual("Second:b", restored.Logic.Connections[0].TargetStoryId);
    }

    [TestMethod]
    public void CollisionInvalidNamespaceAndMissingOwnedResourceLeaveInputUntouched()
    {
        var initial = Project();
        Assert.Throws<ArgumentException>(() => NamespaceMigrationPlanner.ChangeGlobal(initial, "../unsafe"));
        var broken = initial with { Actors = [initial.Actors[1]] };
        Assert.Throws<InvalidOperationException>(() => NamespaceMigrationPlanner.ChangeGlobal(broken, "Safe"));
        var migrated = NamespaceMigrationPlanner.ChangeGlobal(initial, "First").Result;
        var collision = migrated with
        {
            Actors = [.. migrated.Actors, new IndividualActorResource { NpcId = "Second:actor", DisplayName = "existing" }],
        };
        Assert.Throws<InvalidOperationException>(() => NamespaceMigrationPlanner.ChangeGlobal(collision, "Second"));
        Assert.AreEqual("First:actor", collision.Actors[0].Id);
        Assert.AreEqual("a", initial.Graphs[0].Id);
        Assert.IsNull(initial.Policy);
    }

    [TestMethod]
    public void SharedDefinitionCanMoveTogetherButCannotSplitAcrossOwnerNamespaces()
    {
        var initial = Project();
        var shared = initial with
        {
            Memberships = [initial.Memberships[0], new("b", new() { Actors = ["actor", "own"] })],
        };
        var together = NamespaceMigrationPlanner.ChangeGlobal(shared, "First").Result;
        Assert.AreEqual("First:actor", together.Memberships[1].OwnedResources.Actors[0]);
        Assert.Throws<InvalidOperationException>(() => NamespaceMigrationPlanner.ChangeStory(together, "First:b", "Private"));
        Assert.AreEqual("First:b", together.Graphs[1].Id);
    }

    private static NamespaceProjectSnapshot Project() => new(
        [new(GraphResourceKind.Story, "a", "A", new GraphDocument()), new(GraphResourceKind.Story, "b", "B", new GraphDocument())],
        [new("a", new() { Actors = ["actor"] }), new("b", new() { Actors = ["own"] }, new() { Actors = ["actor"] })],
        [new IndividualActorResource { NpcId = "actor", DisplayName = "actor" }, new CollectiveActorResource { GroupId = "own", DisplayName = "own" }],
        [], new(1, [new("a", "port_a", "b", "port_b")]), null);
}
