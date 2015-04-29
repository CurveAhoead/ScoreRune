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
