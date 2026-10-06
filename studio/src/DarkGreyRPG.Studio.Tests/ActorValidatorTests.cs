using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorValidatorTests
{
    [TestMethod]
    public void ValidNewActorIdHasNoErrors()
    {
        var issues = ActorValidator.ValidateId("ST-2345-6789-ABCD-EFGH~actor~teacher_npc", ActorIdPolicy.NewResource);

        Assert.IsFalse(issues.Any(issue => issue.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Teacher NPC")]
    [DataRow("_teacher")]
    [DataRow("-teacher")]
    [DataRow("teacher.npc")]
    [DataRow("老师")]
    [DataRow("teacher/")]
    public void InvalidNewActorIdHasAnError(string id)
    {
        var issues = ActorValidator.ValidateId(id, ActorIdPolicy.NewResource);

        Assert.IsTrue(issues.Any(issue => issue.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public void LegacyDotIdIsRejectedWithoutCompatibilityFallback()
    {
        var issues = ActorValidator.ValidateId("legacy.actor", ActorIdPolicy.ExistingResource);

        Assert.IsTrue(issues.Any(issue => issue.Severity == ValidationSeverity.Error));
        Assert.IsFalse(issues.Any(issue => issue.Code == "actor.id.legacy_compatible"));
    }

    [TestMethod]
    public void NormalizeIdProducesRuntimeCompatibleSuggestion()
    {
        Assert.AreEqual("teacher_npc", ActorValidator.NormalizeId("  Teacher   NPC  "));
        Assert.AreEqual("teacher", ActorValidator.NormalizeId("__Teacher__"));
    }

    [TestMethod]
    public void EmptyDisplayNameIsRejected()
    {
        var resource = new ActorResource { Id = "teacher", DisplayName = "   " };

        var issues = ActorValidator.Validate(resource, ActorIdPolicy.NewResource);

        Assert.IsTrue(issues.Any(issue => issue.Code == "actor.display_name.required"));
    }
}
