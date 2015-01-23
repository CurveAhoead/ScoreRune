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

