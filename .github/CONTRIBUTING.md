# Contributing to ScoreRune

Thanks for looking at ScoreRune. The Python package is standard-library only;
the C# runtime targets net9.0 with no package references.

## Ground rules

- No new runtime dependencies on either side.
- Determinism is the product: the same rubric + candidate always produce the
  same scorecard. No wall-clock, no randomness, no dict-order output.
- Evidence kinds are additive-only. Existing field names are frozen as of 1.0.0.
- Python and C# must stay semantically identical; every scoring change is
