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
    }

    private static (double, List<string>) ScoreKeywordDensity(string text, Dictionary<string, object> p)
    {
        var keywords = GetList(p, "keywords").ConvertAll(k => k.ToLowerInvariant());
        var words = Words(text).ConvertAll(w => w.ToLowerInvariant());
        int total = words.Count == 0 ? 1 : words.Count;
        int hits = words.FindAll(keywords.Contains).Count;
        double density = (double)hits / total * 100.0;
        double target = GetNum(p, "target", 2.0);
        double tol = GetNum(p, "tolerance", 1.0);
        if (tol == 0) tol = 1.0;
        double diff = Math.Abs(density - target);
        double score = Clamp01(1.0 - Math.Max(diff - tol, 0.0) / Math.Max(target, 1.0));
        return (score, new() { $"density = {density:F2}/100 words (target {target:F2} +/- {tol:F2}, hits {hits})" });
    }

    private static (double, List<string>) ScoreStructure(string text, Dictionary<string, object> p)
    {
        var checks = new List<bool>();
        var ev = new List<string>();
        if (GetBool(p, "require_headings"))
        {
            bool ok = HeadingRe.IsMatch(text);
            checks.Add(ok); ev.Add($"headings: {(ok ? "yes" : "no")}");
        }
        if (GetBool(p, "require_lists"))
        {
            bool ok = ListRe.IsMatch(text);
            checks.Add(ok); ev.Add($"lists: {(ok ? "yes" : "no")}");
        }
        int minParas = (int)GetNum(p, "min_paragraphs", 0);
        if (minParas > 0)
        {
            var paras = Regex.Split(text.Trim(), @"\n\s*\n");
            int count = 0;
            foreach (var b in paras) if (b.Trim().Length > 0) count++;
            bool ok = count >= minParas;
            checks.Add(ok); ev.Add($"paragraphs: {count} (need {minParas})");
        }
        if (checks.Count == 0) return (0.0, new() { "no structural requirements configured" });
        int satisfied = checks.FindAll(c => c).Count;
        return ((double)satisfied / checks.Count, ev);
    }

    public static CriterionResult ScoreCriterion(Criterion c, string text, double normalizedWeight)
    {
        (double raw, List<string> ev) = c.Kind switch
        {
            "phrase" => ScorePhrase(text, c.Params),
            "length" => ScoreLength(text, c.Params),
            "keyword_density" => ScoreKeywordDensity(text, c.Params),
            "structure" => ScoreStructure(text, c.Params),
            _ => (0.0, new List<string> { $"unknown kind '{c.Kind}'" }),
        };
        raw = Clamp01(raw);
        return new CriterionResult
        {
            CriterionId = c.Id,
            Title = c.Title,
            RawScore = Math.Round(raw, 6),
            Weight = c.Weight,
            NormalizedWeight = Math.Round(normalizedWeight, 6),
            WeightedScore = Math.Round(raw * normalizedWeight, 6),
            Evidence = ev,
        };
    }

    public static Scorecard ScoreCandidate(Rubric rubric, Candidate candidate)
    {
        double totalWeight = rubric.TotalWeight();
        if (totalWeight == 0) totalWeight = 1.0;
        var results = new List<CriterionResult>();
        double total = 0;
        foreach (var c in rubric.Criteria)
        {
            double nw = c.Weight / totalWeight;
            var r = ScoreCriterion(c, candidate.Text, nw);
            results.Add(r);
            total += r.WeightedScore;
        }
        return new Scorecard
        {
            CandidateId = candidate.Id,
            Label = string.IsNullOrEmpty(candidate.Label) ? candidate.Id : candidate.Label,
            RubricId = rubric.Id,
            Total = Clamp01(total),
            Results = results,
        };
    }

    public static List<Scorecard> RankCandidates(Rubric rubric, List<Candidate> candidates)
    {
        var cards = new List<Scorecard>();
        foreach (var c in candidates) cards.Add(ScoreCandidate(rubric, c));
        cards.Sort((a, b) =>
        {
            int byScore = b.Total.CompareTo(a.Total);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.CandidateId, b.CandidateId);
        });
        return cards;
    }
}

// draft note 12
