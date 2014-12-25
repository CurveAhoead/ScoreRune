using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScoreRune.Runtime;

/// <summary>
/// Deterministic scoring engine. The scoring functions are pure and dispatch on
/// <see cref="Criterion.Kind"/>, matching the reference semantics implemented in
/// the Python package so both runtimes agree on any given rubric and candidate.
/// </summary>
public static class Engine
{
    private static readonly Regex WordRe = new(@"[A-Za-z0-9']+", RegexOptions.Compiled);
    private static readonly Regex HeadingRe = new(@"^\s{0,3}#{1,6}\s+\S", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ListRe = new(@"^\s*(?:[-*+]|\d+\.)\s+\S", RegexOptions.Compiled | RegexOptions.Multiline);

    private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

    private static List<string> Words(string text)
    {
        var list = new List<string>();
        foreach (Match m in WordRe.Matches(text)) list.Add(m.Value);
        return list;
    }

    // Values in Criterion.Params arrive as System.Text.Json JsonElement instances.
    // These accessors read them without any third-party JSON library.
    private static string GetStr(Dictionary<string, object> p, string key, string def = "")
    {
        if (!p.TryGetValue(key, out var v) || v is null) return def;
        if (v is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() ?? def : je.ToString();
        return v.ToString() ?? def;
    }

    private static double GetNum(Dictionary<string, object> p, string key, double def)
    {
        if (!p.TryGetValue(key, out var v) || v is null) return def;
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Number)
            return je.GetDouble();
        return double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def;
    }

    private static bool GetBool(Dictionary<string, object> p, string key, bool def = false)
    {
        if (!p.TryGetValue(key, out var v) || v is null) return def;
        if (v is JsonElement je)
            return je.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => bool.TryParse(je.ToString(), out var b) ? b : def,
            };
        return bool.TryParse(v.ToString(), out var pb) ? pb : def;
    }

    private static bool HasKey(Dictionary<string, object> p, string key)
        => p.ContainsKey(key) &&
           !(p[key] is JsonElement je && je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

    private static List<string> GetList(Dictionary<string, object> p, string key)
    {
        var result = new List<string>();
        if (!p.TryGetValue(key, out var v) || v is null) return result;
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
                result.Add(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString());
        }
        else if (v is IEnumerable<object> items)
        {
            foreach (var it in items) result.Add(it?.ToString() ?? "");
        }
        return result;
    }

    private static (double, List<string>) ScorePhrase(string text, Dictionary<string, object> p)
    {
        var phrases = GetList(p, "phrases");
        if (phrases.Count == 0) return (0.0, new() { "no phrases configured" });
        bool cs = GetBool(p, "case_sensitive");
        string hay = cs ? text : text.ToLowerInvariant();
        var found = new List<string>();
        var missing = new List<string>();
        foreach (var ph in phrases)
        {
            string needle = cs ? ph : ph.ToLowerInvariant();
            if (hay.Contains(needle)) found.Add(ph); else missing.Add(ph);
        }
        string mode = GetStr(p, "mode", "all").ToLowerInvariant();
        double score = mode == "any" ? (found.Count > 0 ? 1.0 : 0.0)
                                     : (double)found.Count / phrases.Count;
        var ev = new List<string> { $"found: {(found.Count > 0 ? string.Join(", ", found) : "none")}" };
        if (missing.Count > 0) ev.Add($"missing: {string.Join(", ", missing)}");
        return (Clamp01(score), ev);
    }

    private static (double, List<string>) ScoreLength(string text, Dictionary<string, object> p)
    {
        int n = Words(text).Count;
        int lo = (int)GetNum(p, "min_words", 0);
        int hi = (int)GetNum(p, "max_words", 10_000_000);
        double score;
        if (HasKey(p, "ideal_words"))
        {
            int ideal = (int)GetNum(p, "ideal_words", 0);
            if (n == ideal) score = 1.0;
            else if (n < ideal) score = Clamp01((double)(n - lo) / Math.Max(ideal - lo, 1));
            else score = Clamp01((double)(hi - n) / Math.Max(hi - ideal, 1));
        }
        else
        {
            if (n >= lo && n <= hi) score = 1.0;
            else if (n < lo) score = lo > 0 ? Clamp01((double)n / lo) : 0.0;
            else score = Clamp01(1.0 - (double)(n - hi) / Math.Max(hi, 1));
        }
        return (score, new() { $"word count = {n} (bounds {lo}..{hi})" });
