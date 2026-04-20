# CLI Reference

Both runtimes expose the same three subcommands. The Python CLI is invoked as
`python -m scorerune` (or `scorerune` once installed); the C# runtime is invoked
as `dotnet run --project runtime/ScoreRune.Runtime.csproj --`.

## score

Score one candidate text file against a rubric.

```
scorerune score -r RUBRIC.json -c CANDIDATE.txt [--id ID] [-f md|json]
```

- `-r, --rubric` — path to the rubric JSON (required).
- `-c, --candidate` — path to a plain-text candidate (required).
- `--id` — override the candidate id (defaults to the file stem).
- `-f, --format` — `md` (default) or `json`.

## rank

Score a batch of candidates and print them best-first.

```
scorerune rank -r RUBRIC.json -c CANDIDATES.json [-f md|json]
```

- `-c, --candidates` — a JSON array of candidate objects, or an object with a
  `candidates` array. Each candidate has `id`, `text`, and optional `label`.

Ordering is deterministic: descending total, with candidate id as a stable
tie-break.

## validate

Load a rubric (and optionally candidates) and report status.

```
scorerune validate -r RUBRIC.json [-c CANDIDATES.json]
```

## Exit codes

| code | meaning |
|------|---------|
| `0` | success |
| `2` | load or validation error (message on stderr) |

## Piping

Because all commands write to stdout, they compose with normal shell tooling:

```
scorerune rank -r examples/rubric.json -c examples/candidates.json -f json > ranking.json
```

# draft note 20
