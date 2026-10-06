using DarkGreyRPG.Studio.ViewModels;
namespace DarkGreyRPG.Studio.Wpf.Tests;
[TestClass]
public sealed class CanonicalProjectMigrationUiTests
{
    [TestMethod]
    public void ShellHasNoUserFacingMigrationCommandOrAlias()
    {
        Assert.IsNull(typeof(ShellViewModel).GetProperty("MigrateCanonicalProjectCommand"));
        Assert.IsNull(typeof(ShellViewModel).GetProperty("MigrateProjectCommand"));
    }
}
