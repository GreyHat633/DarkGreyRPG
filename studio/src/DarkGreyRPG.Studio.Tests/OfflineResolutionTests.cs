using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class OfflineResolutionTests
{
    [TestMethod]
    public void MissingCanonicalMemberDiagnosticNamesConsumerKindAndResourceAddress()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story A"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            referencedResources: new CanonicalStoryMembershipSet { Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~boss"] }));

        var snapshot = new CanonicalStoryWorkspaceLoader(store).Load("ST-2345-6789-ABCD-EFGH");
        var issue = snapshot.ValidationErrors.Single();

        Assert.AreEqual("story.workspace.member.missing", issue.Code);
        StringAssert.Contains(issue.Message, "ST-2345-6789-ABCD-EFGH");
        StringAssert.Contains(issue.Message, "Actor");
        StringAssert.Contains(issue.Message, "ST-JKLM-NPQR-STUV-WXYZ~actor~boss");
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ~actor~boss", issue.NodeId);
    }

    [TestMethod]
    public void ProjectValidationIncludesOfflineProviderCatalogDiagnostics()
    {
        using var project = new TestProjectDirectory();
        var references = Path.Combine(project.Root, "references");
        Directory.CreateDirectory(references);
        File.WriteAllText(Path.Combine(references, "broken.dgrs"), "not a zip");

        var service = new ProjectService();
        service.OpenProject(project.Root);
        var issue = service.ValidateProject().Single(item => item.Code == "provider.invalid");

        Assert.AreEqual("references", issue.Field);
        StringAssert.Contains(issue.NodeId, "broken.dgrs");
    }

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string displayName)
        => new(kind, id, displayName, new GraphDocument([new GraphNode("node", "unknown", "Node")]));
}
