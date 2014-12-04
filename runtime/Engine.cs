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
