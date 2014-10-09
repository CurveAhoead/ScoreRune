"""Command line interface for scorerune.

Subcommands
-----------
score    Score a single candidate against a rubric and print a scorecard.
rank     Score a batch of candidates and print a ranking, best first.
validate Load a rubric (and optional candidates) and report validation status.

Output format is selectable with --format {md,json}. All commands read only
local files and emit to stdout, so they compose with shell pipelines.
"""

from __future__ import annotations

import argparse
import sys

from . import __version__
from .engine import rank_candidates, score_candidate
from .loader import (
    LoadError,
    load_candidate_text,
    load_candidates,
    load_rubric,
)
from .report import (
    ranking_json,
    ranking_markdown,
    scorecard_json,
    scorecard_markdown,
)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="scorerune",
