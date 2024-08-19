# CLI Reference

Both runtimes expose the same three subcommands. The Python CLI is invoked as
`python -m scorerune` (or `scorerune` once installed); the C# runtime is invoked
as `dotnet run --project runtime/ScoreRune.Runtime.csproj --`.

## score

Score one candidate text file against a rubric.

```
scorerune score -r RUBRIC.json -c CANDIDATE.txt [--id ID] [-f md|json]
```

