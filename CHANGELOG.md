# Changelog

All notable changes to ScoreRune are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- planning: per-criterion calibration curves for noisy graders

## [1.0.0] - 2026-01-20

### Added
- stable scorecard schema (frozen field names, additive-only)
- parallel C# runtime (net9.0) producing byte-compatible totals on shared inputs
- `validate` subcommand for rubric and candidate sanity checks
- performance pass on dispatch-based scorers

### Verified
- `python -m compileall scorerune` succeeds.
- `dotnet build -c Release` succeeds with 0 warnings and 0 errors.
- Python and C# runtimes rank the example batch identically.

## [0.6.0] - 2024-05-06

### Added
- `rank` command with stable tie-break on candidate id
- ASCII score bars and per-criterion evidence sections in the markdown report
- weight normalization per rubric (total weight no longer required to be 1.0)

## [0.5.0] - 2022-07-19

### Added
- `structure` evidence kind (section/heading shape checks)
- keyword_density evidence with configurable thresholds
