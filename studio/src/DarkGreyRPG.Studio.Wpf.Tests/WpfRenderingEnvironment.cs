namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class WpfRenderingEnvironment
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("DGR_STUDIO_SOFTWARE_RENDERING") == "1")
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            context.WriteLine("WPF validation uses explicitly requested process-local software rendering.");
        }
    }
}
