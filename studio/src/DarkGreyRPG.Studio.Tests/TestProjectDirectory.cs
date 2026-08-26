namespace DarkGreyRPG.Studio.Tests;

internal sealed class TestProjectDirectory : IDisposable
{
    public TestProjectDirectory(bool createProjectFile = true)
    {
        Root = Path.Combine(AppContext.BaseDirectory, ".test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "actors"));
        if (createProjectFile)
        {
            File.WriteAllText(
                Path.Combine(Root, "project.json"),
                """
                {
                  "schema_version": 1,
                  "id": "test_project",
                  "display_name": "Test Project"
                }
                """);
        }
    }

    public string Root { get; }

    public string Actors => Path.Combine(Root, "actors");

    public string ActorPath(string id) => Path.Combine(Actors, id + ".json");

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
