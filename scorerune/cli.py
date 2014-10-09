"""Command line interface for scorerune.

Subcommands
-----------
score    Score a single candidate against a rubric and print a scorecard.
rank     Score a batch of candidates and print a ranking, best first.
validate Load a rubric (and optional candidates) and report validation status.

Output format is selectable with --format {md,json}. All commands read only
local files and emit to stdout, so they compose with shell pipelines.
"""
