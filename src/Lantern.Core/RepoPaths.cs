namespace Lantern.Core;

public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string FilmsJson => Path.Combine(Root, "data", "films.json");

    public static string ChromaData => Path.Combine(Root, "chroma_data");

    private static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "data", "films.json"))
                    || File.Exists(Path.Combine(dir.FullName, ".env.example")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not find the repo root (looked for data/films.json or .env.example).");
    }
}
