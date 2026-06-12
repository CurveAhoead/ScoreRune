using System.Text.Json.Serialization;

namespace ScoreRune.Runtime;

/// <summary>
/// Value types describing rubrics, candidates, and scoring results. These mirror
/// the Python <c>scorerune.model</c> structures so scorecards produced by either
/// runtime carry the same fields and semantics.
/// </summary>
public sealed record Criterion
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("weight")] public double Weight { get; init; } = 1.0;
    [JsonPropertyName("kind")] public string Kind { get; init; } = "phrase";
    [JsonPropertyName("params")] public Dictionary<string, object> Params { get; init; } = new();
    [JsonPropertyName("description")] public string Description { get; init; } = "";
}

public sealed record Rubric
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
    [JsonPropertyName("criteria")] public List<Criterion> Criteria { get; init; } = new();

    public double TotalWeight()
    {
        double sum = 0;
        foreach (var c in Criteria) sum += c.Weight;
        return sum;
    }
}

public sealed record Candidate
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("text")] public string Text { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
}

public sealed record CriterionResult
{
    [JsonPropertyName("criterion_id")] public string CriterionId { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("raw_score")] public double RawScore { get; init; }
    [JsonPropertyName("weight")] public double Weight { get; init; }
    [JsonPropertyName("normalized_weight")] public double NormalizedWeight { get; init; }
    [JsonPropertyName("weighted_score")] public double WeightedScore { get; init; }
    [JsonPropertyName("evidence")] public List<string> Evidence { get; init; } = new();
}

public sealed record Scorecard
{
    [JsonPropertyName("candidate_id")] public string CandidateId { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("rubric_id")] public string RubricId { get; init; } = "";
    [JsonPropertyName("total")] public double Total { get; init; }
    [JsonPropertyName("percent")] public double Percent => Math.Round(Total * 100.0, 2);
    [JsonPropertyName("results")] public List<CriterionResult> Results { get; init; } = new();
}

// draft note 45
