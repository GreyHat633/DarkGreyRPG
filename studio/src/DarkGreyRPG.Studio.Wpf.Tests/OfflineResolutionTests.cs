using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class OfflineResolutionTests
{
    [TestMethod]
    public void ProviderBackedResourceExposesReadOnlySourceAndIdentity()
    {
        var provider = new OfflineProviderResource(
            DgrResourceKind.Actor,
            "B:boss",
            "真正的 Boss",
            "{\"schema_version\":1}",
            new OfflinePackageIdentity("package-b", "2.0.0"),
            "OB",
            "fingerprint");
        var item = new CanonicalStoryActorItem(
            new ActorResourceInfo("B:boss", "真正的 Boss", "provider.dgrs", [], "individual"),
            CanonicalStoryWorkspaceMembershipKind.Referenced)
        { Provider = provider };

        Assert.IsTrue(item.IsReferenced);
        Assert.IsTrue(item.IsReadOnly);
        Assert.AreEqual("package-b@2.0.0", item.ProviderPackageText);
        Assert.AreEqual("[引用] ", item.SourceText);
        Assert.AreEqual("B:boss", item.Id);
        Assert.AreEqual("真正的 Boss", item.DisplayName);
    }
}
