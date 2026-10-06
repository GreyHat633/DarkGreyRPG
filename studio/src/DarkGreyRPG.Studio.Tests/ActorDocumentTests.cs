using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorDocumentTests
{
    [TestMethod]
    public void NewDocumentIsDirtyAndValidationTracksEdits()
    {
        var document = ActorDocument.CreateNew("ST-2345-6789-ABCD-EFGH~actor~teacher");

        Assert.IsTrue(document.IsNew);
        Assert.IsTrue(document.IsDirty);
        Assert.AreEqual(0, document.ValidationErrors.Count);

        document.DisplayName = " ";

        Assert.IsTrue(document.ValidationErrors.Any(issue => issue.Code == "actor.display_name.required"));
    }

    [TestMethod]
    public void SavedDocumentBecomesDirtyAndCanReturnToBaseline()
    {
        using var project = new TestProjectDirectory();
        var resource = new IndividualActorResource { HomeStoryId = "ST-2345-6789-ABCD-EFGH", NpcId = "ST-2345-6789-ABCD-EFGH~actor~teacher", DisplayName = "Teacher" };
        var path = new ActorRepository(project.Root).GetActorPath(resource.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ActorSerializer.Serialize(resource, ActorIdPolicy.NewResource));
        var document = ActorDocument.FromResource(resource, path);

        document.DisplayName = "Changed";
        Assert.IsTrue(document.IsDirty);

        document.DisplayName = "Teacher";
        Assert.IsFalse(document.IsDirty);
    }
}
