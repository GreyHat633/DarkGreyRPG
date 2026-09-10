using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class DgrResourceIdTests
{
    [TestMethod]
    public void ValidationPreservesCaseAndRejectsUnsafeInput()
    {
        Assert.IsTrue(DgrResourceId.IsValidNamespace("Author"));
        Assert.IsTrue(DgrResourceId.IsFullId("Author:boss"));
        Assert.IsFalse(DgrResourceId.IsFullId("boss"));
        Assert.IsTrue(DgrResourceId.IsFullId("Author:Boss"));
        Assert.IsFalse(DgrResourceId.IsFullId("Author:boss/extra"));
        Assert.IsFalse(DgrResourceId.IsFullId(" Author:boss"));
        Assert.IsFalse(DgrResourceId.IsFullId("作者:boss"));
        Assert.IsFalse(DgrResourceId.IsFullId("Author:boss:extra"));
    }

    [TestMethod]
    public void CompatibilityAcceptsBareHistoricalLengths()
    {
        Assert.IsTrue(DgrResourceId.IsCompatibleId("legacy.actor"));
        Assert.IsTrue(DgrResourceId.IsCompatibleId(new string('a', 200)));
        Assert.IsFalse(DgrResourceId.IsCompatibleId("legacy actor"));
        Assert.IsFalse(DgrResourceId.IsCompatibleId("_legacy"));
    }

    [TestMethod]
    public void FullAndBarePathsUseStableFilesystemSafeProjections()
    {
        Assert.AreEqual("Author:boss", DgrResourceId.Qualify("Author", "boss"));
        Assert.AreEqual("boss", DgrResourceId.LocalId("Author:boss"));
        Assert.AreEqual("Author", DgrResourceId.Namespace("Author:boss"));
        Assert.AreEqual(string.Empty, DgrResourceId.Namespace("legacy.actor"));
        Assert.AreEqual("x417574686f72/x626f7373.json", DgrResourceId.RelativeJsonPath("Author:boss"));
        Assert.AreEqual("x417574686f72_x626f7373.dgrs", DgrResourceId.PackageFileName("Author:boss"));
        Assert.AreEqual("legacy.actor.json", DgrResourceId.RelativeJsonPath("legacy.actor"));
        Assert.AreEqual("legacy.actor.dgrs", DgrResourceId.PackageFileName("legacy.actor"));
        Assert.AreEqual("x434f4e/x636f6e.json", DgrResourceId.RelativeJsonPath("CON:con"));
        Assert.AreNotEqual(DgrResourceId.RelativeJsonPath("A:boss"), DgrResourceId.RelativeJsonPath("a:boss"));
        Assert.IsFalse(DgrResourceId.RelativeJsonPath("A:boss").Contains(":", StringComparison.Ordinal));
        Assert.IsFalse(DgrResourceId.RelativeJsonPath("A:boss").Contains("\\", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FullLimitsAndInvalidDerivationsAreEnforced()
    {
        var full = DgrResourceId.Qualify(new string('a', 32), new string('b', 63));
        Assert.AreEqual(96, full.Length);
        Assert.IsTrue(DgrResourceId.IsFullId(full));
        Assert.IsFalse(DgrResourceId.IsFullId(full + "x"));
        Assert.ThrowsExactly<ArgumentException>(() => DgrResourceId.Qualify(" Author", "boss"));
        Assert.AreEqual("Author:Boss", DgrResourceId.Qualify("Author", "Boss"));
        Assert.AreNotEqual(DgrResourceId.RelativeJsonPath("Author:Boss"), DgrResourceId.RelativeJsonPath("Author:boss"));
        Assert.ThrowsExactly<ArgumentException>(() => DgrResourceId.LocalId("Author/boss"));
        Assert.ThrowsExactly<ArgumentException>(() => DgrResourceId.Namespace("Author:boss:extra"));
    }

    [TestMethod]
    public void NamespacePolicyUsesOrdinalFullStoryOverrides()
    {
        var policy = new NamespacePolicy(
            "global",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Author:story"] = "custom",
            });

        Assert.AreEqual("global", policy.GlobalNamespace);
        Assert.AreEqual("custom", policy.EffectiveNamespace("Author:story"));
        Assert.AreEqual("global", policy.EffectiveNamespace("Author:other"));
        Assert.IsTrue(policy.IsCustom("Author:story"));
        Assert.IsFalse(policy.IsCustom("Author:other"));
        Assert.ThrowsExactly<ArgumentException>(() => policy.EffectiveNamespace("story"));
        Assert.ThrowsExactly<ArgumentException>(() => new NamespacePolicy("global", new Dictionary<string, string>
        {
            ["story"] = "custom",
        }));
    }
}
