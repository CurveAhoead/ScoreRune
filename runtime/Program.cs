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
        if (result.Count == 0) throw new JsonException("no candidates found");
        return result;
    }

    private static string Bar(double fraction, int width = 20)
    {
        fraction = fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
        int filled = (int)Math.Round(fraction * width);
        return new string('#', filled) + new string('.', width - filled);
    }

    private static string ScorecardMarkdown(Scorecard card)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Scorecard: {card.Label}").AppendLine();
        sb.AppendLine($"- Rubric: `{card.RubricId}`");
        sb.AppendLine($"- Candidate: `{card.CandidateId}`");
        sb.AppendLine($"- **Total: {card.Percent:F2}%**").AppendLine();
        sb.AppendLine("| Criterion | Raw | Weight | Weighted | Bar |");
        sb.AppendLine("|-----------|-----|--------|----------|-----|");
        foreach (var r in card.Results)
            sb.AppendLine($"| {r.Title} | {r.RawScore:F2} | {r.NormalizedWeight:F2} | {r.WeightedScore:F3} | `{Bar(r.RawScore)}` |");
        sb.AppendLine().AppendLine("## Evidence").AppendLine();
        foreach (var r in card.Results)
        {
            sb.AppendLine($"### {r.Title}");
            foreach (var e in r.Evidence) sb.AppendLine($"- {e}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static int CmdScore(string[] args)
    {
        var rubric = LoadRubric(Opt(args, "-r", Opt(args, "--rubric")));
        var candidate = LoadCandidateText(Opt(args, "-c", Opt(args, "--candidate")), Opt(args, "--id"));
        var card = Engine.ScoreCandidate(rubric, candidate);
        string fmt = Opt(args, "-f", Opt(args, "--format", "md"));
        Console.Write(fmt == "json" ? JsonSerializer.Serialize(card, WriteOpts) + "\n"
                                    : ScorecardMarkdown(card));
        return 0;
    }

    private static int CmdRank(string[] args)
    {
        var rubric = LoadRubric(Opt(args, "-r", Opt(args, "--rubric")));
        var candidates = LoadCandidates(Opt(args, "-c", Opt(args, "--candidates")));
        var cards = Engine.RankCandidates(rubric, candidates);
        string fmt = Opt(args, "-f", Opt(args, "--format", "md"));
        if (fmt == "json")
        {
            Console.Write(JsonSerializer.Serialize(cards, WriteOpts) + "\n");
            return 0;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"# Ranking for rubric `{rubric.Id}`").AppendLine();
        sb.AppendLine("| Rank | Candidate | Total |");
        sb.AppendLine("|------|-----------|-------|");
        for (int i = 0; i < cards.Count; i++)
            sb.AppendLine($"| {i + 1} | {cards[i].Label} | {cards[i].Percent:F2}% |");
        Console.Write(sb.ToString().TrimEnd() + "\n");
        return 0;
    }

    private static int CmdValidate(string[] args)
    {
        var rubric = LoadRubric(Opt(args, "-r", Opt(args, "--rubric")));
        Console.WriteLine($"rubric '{rubric.Id}' OK: {rubric.Criteria.Count} criteria, total weight {rubric.TotalWeight():G}");
        string cpath = Opt(args, "-c", Opt(args, "--candidates"));
        if (!string.IsNullOrEmpty(cpath))
        {
            var candidates = LoadCandidates(cpath);
            Console.WriteLine($"candidates OK: {candidates.Count} loaded");
        }
        return 0;
    }
}

// draft note 14
