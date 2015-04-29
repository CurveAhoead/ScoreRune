# Roadmap

ScoreRune aims to be a small, dependency-free rubric engine that gives the same
answer every time, whether you run it from Python or .NET. The completed
milestones below map directly to code in this repository; the unchecked items
describe where the engine is headed.

## Completed

- [x] **Deterministic scoring core** — `scorerune/engine.py` and
  `runtime/Engine.cs` implement pure scorers for `phrase`, `length`,
  `keyword_density`, and `structure`, with per-rubric weight normalization and a
  clamped total in `[0, 1]`.
- [x] **Rubric and candidate model with validation** — `scorerune/model.py`
  defines the value objects and `scorerune/loader.py` validates rubrics and
  candidate batches eagerly, raising a single aggregated `LoadError`.
- [x] **CLI with Markdown and JSON reporting** — `scorerune/cli.py` and
  `scorerune/report.py` provide `score`, `rank`, and `validate` subcommands with
  scorecards, ASCII bars, evidence sections, and stable JSON output.
- [x] **Cross-runtime parity (Python + .NET)** — `runtime/Program.cs` mirrors the
  CLI and produces identical totals to the Python engine on the shared example
  rubric and candidate batch (verified: 90.00%, 89.60%, 0.00%).
