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
        description="Deterministic rubric engine for AI response review.",
    )
    parser.add_argument("--version", action="version",
                        version=f"scorerune {__version__}")
    sub = parser.add_subparsers(dest="command", required=True)

    p_score = sub.add_parser("score", help="score one candidate")
    p_score.add_argument("-r", "--rubric", required=True, help="rubric JSON path")
    p_score.add_argument("-c", "--candidate", required=True,
                         help="candidate text file path")
    p_score.add_argument("--id", default="", help="candidate id override")
    p_score.add_argument("-f", "--format", choices=("md", "json"), default="md")

    p_rank = sub.add_parser("rank", help="score and rank a batch")
    p_rank.add_argument("-r", "--rubric", required=True, help="rubric JSON path")
    p_rank.add_argument("-c", "--candidates", required=True,
                        help="candidates JSON path")
    p_rank.add_argument("-f", "--format", choices=("md", "json"), default="md")

    p_val = sub.add_parser("validate", help="validate a rubric/candidates")
    p_val.add_argument("-r", "--rubric", required=True, help="rubric JSON path")
    p_val.add_argument("-c", "--candidates", default="",
                       help="optional candidates JSON path")

    return parser


def _cmd_score(args: argparse.Namespace) -> int:
    rubric = load_rubric(args.rubric)
    candidate = load_candidate_text(args.candidate, args.id)
    card = score_candidate(rubric, candidate)
    out = scorecard_json(card) if args.format == "json" else scorecard_markdown(card)
    sys.stdout.write(out)
    return 0


def _cmd_rank(args: argparse.Namespace) -> int:
    rubric = load_rubric(args.rubric)
    candidates = load_candidates(args.candidates)
    ranking = rank_candidates(rubric, candidates)
    out = ranking_json(ranking) if args.format == "json" else ranking_markdown(ranking)
    sys.stdout.write(out)
    return 0


def _cmd_validate(args: argparse.Namespace) -> int:
    rubric = load_rubric(args.rubric)
    sys.stdout.write(
        f"rubric '{rubric.id}' OK: {len(rubric.criteria)} criteria, "
        f"total weight {rubric.total_weight():g}\n"
    )
