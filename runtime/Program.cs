using System.Text;
using System.Text.Json;

namespace ScoreRune.Runtime;

/// <summary>
/// Console entry point for the C# runtime. Mirrors the Python CLI subcommands
/// (<c>score</c>, <c>rank</c>, <c>validate</c>) using only the .NET base class
/// library. Rubric and candidate documents are JSON; output is Markdown or JSON.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
    };

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: scorerune <score|rank|validate|version> [options]");
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "score" => CmdScore(args),
                "rank" => CmdRank(args),
                "validate" => CmdValidate(args),
                "version" or "--version" => PrintVersion(),
                _ => Unknown(args[0]),
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"error: invalid JSON: {ex.Message}");
            return 2;
        }
    }

    private static int PrintVersion()
    {
        Console.WriteLine("scorerune-runtime 1.0.0");
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"error: unknown command '{cmd}'");
        return 2;
    }

    private static string Opt(string[] args, string name, string def = "")
    {
        for (int i = 1; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return def;
    }

    private static Rubric LoadRubric(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"file not found: {path}");
        var rubric = JsonSerializer.Deserialize<Rubric>(File.ReadAllText(path), ReadOpts)
                     ?? throw new JsonException("rubric document is empty");
        if (rubric.Criteria.Count == 0)
            throw new JsonException($"rubric '{rubric.Id}' has no criteria");
        return rubric;
    }

    private static Candidate LoadCandidateText(string path, string id)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"file not found: {path}");
        string text = File.ReadAllText(path);
        string cid = string.IsNullOrEmpty(id) ? Path.GetFileNameWithoutExtension(path) : id;
        return new Candidate { Id = cid, Text = text, Label = cid };
    }

    private static List<Candidate> LoadCandidates(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"file not found: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        JsonElement array = root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("candidates", out var arr) ? arr : root;
        var result = new List<Candidate>();
        foreach (var el in array.EnumerateArray())
        {
            var c = el.Deserialize<Candidate>(ReadOpts);
            if (c is not null) result.Add(c);
        }
