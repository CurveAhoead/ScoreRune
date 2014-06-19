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

