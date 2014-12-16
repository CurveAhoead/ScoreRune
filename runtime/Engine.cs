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
