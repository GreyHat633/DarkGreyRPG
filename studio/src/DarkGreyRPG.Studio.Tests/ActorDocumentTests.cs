using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorDocumentTests
{
    [TestMethod]
    public void NewDocumentIsDirtyAndValidationTracksEdits()
    {
        var document = ActorDocument.CreateNew("teacher");

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
        var resource = new ActorResource { Id = "teacher", DisplayName = "Teacher" };
        var path = project.ActorPath("teacher");
        File.WriteAllText(path, ActorSerializer.Serialize(resource, ActorIdPolicy.NewResource));
        var document = ActorDocument.FromResource(resource, path);

        document.Notes = "Changed";
        Assert.IsTrue(document.IsDirty);

        document.Notes = string.Empty;
        Assert.IsFalse(document.IsDirty);
    }
}
