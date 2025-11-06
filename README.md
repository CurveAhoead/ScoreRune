<!-- Banner -->
<p align="center">
  <img src="assets/scorecard.svg" alt="ScoreRune scorecard banner" width="640"/>
</p>

# ScoreRune

**ScoreRune is a deterministic rubric engine for reviewing AI responses.** You
describe what a good answer looks like as a set of weighted criteria, hand it one
or more candidate responses, and ScoreRune returns a reproducible scorecard: a
number in `[0, 1]`, a weighted breakdown per criterion, and human-readable
evidence for every decision. The same rubric and the same candidate always
produce the same score — there is no sampling, no model call, and no hidden
state.

The project ships two runtimes that agree bit-for-bit on totals: a Python 3.11
package (`scorerune`) and a .NET 9 console app (`runtime/ScoreRune.Runtime.csproj`).
Both depend only on their platform's standard library. There is nothing to
download, nothing to pin, and nothing that reaches the network.

---

## Why a deterministic engine

Evaluating AI output with another AI is convenient but fragile: scores drift
between runs, prompts silently change behavior, and you cannot diff two review
sessions with confidence. ScoreRune takes the opposite stance. It treats "quality"
as a set of *mechanically checkable* signals — did the reply acknowledge the
issue, did it stay inside a sensible length window, did it use a list — and turns
those signals into arithmetic. That trade buys three properties that matter for
review pipelines:

- **Reproducibility.** A scorecard is a pure function of `(rubric, candidate)`.
  Re-running it in CI, on a teammate's laptop, or six months later gives the same
  answer.
- **Auditability.** Every criterion attaches evidence strings ("found: sorry,
  understand"; "word count = 65 (bounds 40..180)"). A reviewer can see *why* a
  candidate scored the way it did.
- **Portability.** Because the logic is arithmetic over strings, it ports cleanly.
  The Python and C# engines are line-for-line equivalents, so you can score in
  whichever runtime your pipeline already uses.

ScoreRune is not a replacement for human judgment or for model-graded evaluation.
It is the fast, cheap, repeatable first pass: gate obvious failures, rank a batch
of drafts, and produce a scorecard you can attach to a pull request.

---

## Architecture

ScoreRune is organized as a thin pipeline. Documents come in as JSON or plain
text, the engine scores each criterion with a pure function, weights are
normalized and combined, and a reporter renders the result. The same shape is
implemented twice, once per runtime.

<p align="center">
  <img src="assets/pipeline.svg" alt="ScoreRune scoring pipeline" width="720"/>
</p>

```
                        ┌─────────────────────────────────────────────┐
                        │                  loader                      │
   rubric.json  ───────▶│  parse + validate → Rubric                   │
   candidates.json ────▶│  parse + validate → [Candidate]              │
                        └───────────────────────┬─────────────────────┘
                                                 │
                                                 ▼
                        ┌─────────────────────────────────────────────┐
                        │                  engine                      │
                        │  for each criterion:                         │
                        │    scorer = dispatch[kind]                   │
                        │    raw    = scorer(text, params)   ∈ [0,1]   │
                        │    nw     = weight / Σ weights               │
                        │    part   = raw · nw                         │
                        │  total = Σ part                     ∈ [0,1]  │
                        └───────────────────────┬─────────────────────┘
                                                 │
                                                 ▼
                        ┌─────────────────────────────────────────────┐
                        │                  report                      │
                        │  Scorecard / Ranking  →  Markdown | JSON     │
                        └─────────────────────────────────────────────┘
```

### Module map (Python)

| module | responsibility |
|--------|----------------|
| `scorerune/model.py` | value objects: `Criterion`, `Rubric`, `Candidate`, `CriterionResult`, `Scorecard`, `Ranking`, plus `to_dict`/`from_dict` and validation |
| `scorerune/loader.py` | JSON/text loading with eager validation and a single aggregated `LoadError` |
| `scorerune/engine.py` | the four pure scorers, `score_criterion`, `score_candidate`, `rank_candidates` |
| `scorerune/report.py` | Markdown and JSON renderers with ASCII score bars |
| `scorerune/cli.py` | `argparse`-based `score` / `rank` / `validate` subcommands |

### Module map (.NET)

| file | responsibility |
|------|----------------|
| `runtime/Model.cs` | `record` types mirroring the Python model, annotated for `System.Text.Json` |
| `runtime/Engine.cs` | the four scorers and ranking logic, reading `params` as `JsonElement` |
| `runtime/Program.cs` | console entry point with the same subcommands and output formats |

The engines are deliberately kept in lockstep. When a scorer changes in one
runtime, the other must change identically — the parity check below is how that
invariant is verified.

---

## The scoring model

A **rubric** is a named list of **criteria**. Each criterion has a `weight` and a
`kind`. The kind selects one of four pure scoring functions:

| kind | signal it measures |
|------|--------------------|
| `phrase` | presence of literal substrings, in `any` or `all` mode |
| `length` | word count against a `[min, max]` window, optionally peaked at an ideal |
| `keyword_density` | keyword occurrences per 100 words against a target ± tolerance |
| `structure` | Markdown-ish features: headings, lists, paragraph count |

Each scorer returns a raw value in `[0, 1]` and a list of evidence strings. The
engine normalizes weights by their sum, so the weighted contribution of a
criterion is `raw · (weight / Σ weights)`, and the total is the sum of those
contributions — itself always in `[0, 1]`.

Ranking sorts scorecards by descending total, breaking ties on candidate id so
the order is stable and reproducible regardless of input ordering.

Full semantics for every `params` field live in
[`docs/rubric-guide.md`](docs/rubric-guide.md).

---

## Installation

ScoreRune needs no third-party packages. To run from source you only need the
interpreters/SDKs you already have.

```bash
# Python 3.11+ — run straight from the checkout
python -m scorerune --version

# Optional: install so the `scorerune` command is on PATH
python -m pip install .
```

```bash
# .NET 9 — restore is framework-only, no NuGet packages are pulled
dotnet build -c Release runtime/ScoreRune.Runtime.csproj
```

---

## Usage

### Rank a batch of candidates

The repository ships a support-reply rubric and three drafts. Rank them:

```bash
python -m scorerune rank -r examples/rubric.json -c examples/candidates.json
```

```
# Ranking for rubric `support-reply-v1`

| Rank | Candidate | Total |
|------|-----------|-------|
| 1 | Draft A (structured) | 90.00% |
| 2 | Draft C (wordy, no list) | 89.60% |
| 3 | Draft B (terse) | 0.00% |
```

Below the table, ScoreRune prints a full scorecard per candidate with a weighted
table, ASCII bars, and an evidence section. Draft A wins because it acknowledges
the issue, gives next steps, sits near the ideal length, and uses a list. Draft C
scores nearly as well on content but loses the structure criterion because it is
a single paragraph with no list. Draft B fails everything — it is too short and
mentions none of the expected phrases.

### Score a single response

```bash
python -m scorerune score -r examples/rubric.json -c examples/candidate-a.txt
```

```
# Scorecard: candidate-a

- Rubric: `support-reply-v1`
- Candidate: `candidate-a`
- **Total: 90.00%**

| Criterion | Raw | Weight | Weighted | Bar |
|-----------|-----|--------|----------|-----|
| Acknowledges the customer's issue | 1.00 | 0.30 | 0.300 | `####################` |
| Provides concrete next steps | 1.00 | 0.40 | 0.400 | `####################` |
| Reasonable reply length | 0.50 | 0.20 | 0.100 | `##########..........` |
| Readable structure | 1.00 | 0.10 | 0.100 | `####################` |
```

### JSON output for pipelines

```bash
python -m scorerune rank -r examples/rubric.json -c examples/candidates.json -f json > ranking.json
```

The JSON is emitted with sorted keys and a fixed indent so two runs diff cleanly.
Each scorecard carries `total`, `percent`, and a `results` array with per-criterion
`raw_score`, `normalized_weight`, `weighted_score`, and `evidence`.

### The .NET runtime

The C# app takes the same flags and produces the same numbers:

```bash
dotnet run -c Release --project runtime/ScoreRune.Runtime.csproj -- \
  rank -r examples/rubric.json -c examples/candidates.json
```

```
# Ranking for rubric `support-reply-v1`

| Rank | Candidate | Total |
|------|-----------|-------|
| 1 | Draft A (structured) | 90.00% |
| 2 | Draft C (wordy, no list) | 89.60% |
| 3 | Draft B (terse) | 0.00% |
```

---

## Writing your own rubric

A minimal rubric is a JSON object with a `criteria` array. Here is a rubric that
rewards a technical answer for citing a source, staying concise, and using code
formatting:

```json
{
  "id": "tech-answer-v1",
  "title": "Technical Answer Quality",
  "criteria": [
    {
      "id": "cites-source",
      "title": "Cites a source",
      "weight": 3.0,
      "kind": "phrase",
      "params": { "phrases": ["http", "docs", "reference", "see"], "mode": "any" }
    },
    {
      "id": "concise",
      "title": "Concise",
      "weight": 2.0,
      "kind": "length",
      "params": { "min_words": 20, "max_words": 120, "ideal_words": 60 }
    },
    {
      "id": "uses-code",
      "title": "Uses code formatting",
      "weight": 2.0,
      "kind": "structure",
      "params": { "require_lists": true }
    }
  ]
}
```

Validate it before use:

```bash
python -m scorerune validate -r tech-answer-v1.json
# rubric 'tech-answer-v1' OK: 3 criteria, total weight 7
```

The full field reference, including `keyword_density` and every default value, is
in [`docs/rubric-guide.md`](docs/rubric-guide.md). The command reference is in
[`docs/cli.md`](docs/cli.md).

---

## How scoring is combined (worked example)

Take the support rubric with weights `3, 4, 2, 1` (sum = 10) and Draft A:

| criterion | raw | normalized weight | contribution |
|-----------|-----|-------------------|--------------|
| acknowledges-issue | 1.00 | 0.30 | 0.300 |
| provides-next-steps | 1.00 | 0.40 | 0.400 |
| length-window | 0.50 | 0.20 | 0.100 |
| readable-structure | 1.00 | 0.10 | 0.100 |
| **total** | | | **0.900 → 90.00%** |

The length criterion scores `0.50` because Draft A has 65 words while the ideal is
90 with a lower bound of 40; the triangular curve places 65 halfway up the rising
edge. Nudging the draft toward 90 words would lift the total without touching any
other criterion — exactly the kind of targeted feedback the evidence section is
meant to enable.

---

## Determinism and cross-runtime parity

ScoreRune's central guarantee is that identical inputs yield identical outputs,
across both runtimes. This is enforced by construction:

- Scorers are pure functions of `(text, params)` with no clocks, randomness, or
  I/O.
- Weight normalization and clamping are the same arithmetic in both languages.
- Ranking uses the same comparison and the same tie-break (`candidate_id`,
  ordinal).
- JSON output uses sorted keys and fixed indentation.

The example batch scores `90.00%`, `89.60%`, `0.00%` in that order under both the
Python engine and the .NET runtime. Run the two `demo` targets in the `Makefile`
and compare — the ranking tables are identical.

---

## Repository layout

```
scorerune/
├── scorerune/                 Python package
│   ├── __init__.py            public API surface
│   ├── __main__.py            `python -m scorerune`
│   ├── model.py               value objects + validation
│   ├── loader.py              JSON/text loading
│   ├── engine.py              pure scorers + ranking
│   ├── report.py              Markdown/JSON renderers
│   └── cli.py                 argparse CLI
├── runtime/                   .NET 9 runtime
│   ├── Model.cs
│   ├── Engine.cs
│   ├── Program.cs
│   └── ScoreRune.Runtime.csproj
├── examples/                  rubric + candidate fixtures
├── docs/                      rubric guide + CLI reference
├── assets/                    animated SVGs referenced above
├── .github/workflows/ci.yml   compile + smoke-run both runtimes
├── Makefile                   build/demo/validate helpers
├── pyproject.toml
├── CHANGELOG.md
├── ROADMAP.md
└── LICENSE                    Apache-2.0
```

---

## Build and verify

```bash
make build       # byte-compile Python + Release-build .NET
make demo        # Python ranking of the example batch
make demo-cs     # .NET ranking of the example batch
make validate    # validate the example rubric + candidates
```

On Windows without `make`:

```powershell
python -m compileall scorerune
dotnet build -c Release runtime/ScoreRune.Runtime.csproj
```

---

## Design notes

- **Closed set of evidence kinds.** Keeping the kind set small and closed lets the
  loader validate a rubric up front and keeps the two engines easy to keep in
  sync. New kinds are added deliberately (see the roadmap).
- **Weights are relative.** Normalizing by the sum means authors never have to
  make weights add to any particular number; they express *relative* importance.
- **Evidence is first-class.** Every scorer returns strings a human can read. The
  score tells you *what*; the evidence tells you *why*.
- **No network, no packages.** Both runtimes use only their standard library, so
  ScoreRune runs anywhere the interpreter or SDK is installed.

## License

Apache-2.0. See [LICENSE](LICENSE).

---

## Milestones

- [x] **v0.1** - core scoring engine, pure functions (2014)
- [x] **v0.3** - weighted criteria, phrase evidence (2018)
- [x] **v0.5** - structure + keyword_density evidence, JSON reports (2022)
- [x] **v0.6** - `rank` command, stable tie-breaks (2024)
- [x] **v1.0** - frozen scorecard schema, byte-compatible C# runtime (2026)
- [ ] **v1.1** - calibration curves for noisy graders (in progress)

---

## License

Apache-2.0 - see [LICENSE](LICENSE).
\n\n<!-- docs pass by JasonBui588: rank tie-break example -->\n