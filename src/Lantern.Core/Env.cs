namespace Lantern.Core;

public static class Env
{
    public static void Load()
    {
        var path = Path.Combine(RepoPaths.Root, ".env");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static string RequireOpenAiKey()
    {
        Load();
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
        if (string.IsNullOrEmpty(key) || key == "sk-your-key-here")
        {
            Console.Error.WriteLine(
                "Missing OPENAI_API_KEY. Copy .env.example to .env and put your OpenAI API key in.");
            Environment.Exit(1);
        }

        return key;
    }

    public static string OpenAiModel()
    {
        Load();
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")?.Trim();
        return string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model;
    }
}
