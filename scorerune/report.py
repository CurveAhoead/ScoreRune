"""Rendering of scorecards and rankings into Markdown or JSON.

The reporters are pure string producers so callers decide where output goes.
JSON output uses sorted keys and a fixed indent to keep results diff-stable.
"""

from __future__ import annotations

import json

from .model import Ranking, Scorecard


def _bar(fraction: float, width: int = 20) -> str:
    fraction = 0.0 if fraction < 0 else 1.0 if fraction > 1 else fraction
    filled = round(fraction * width)
    return "#" * filled + "." * (width - filled)


def scorecard_markdown(card: Scorecard) -> str:
    lines: list[str] = []
    lines.append(f"# Scorecard: {card.label}")
    lines.append("")
    lines.append(f"- Rubric: `{card.rubric_id}`")
    lines.append(f"- Candidate: `{card.candidate_id}`")
    lines.append(f"- **Total: {card.percent():.2f}%**")
    lines.append("")
    lines.append("| Criterion | Raw | Weight | Weighted | Bar |")
    lines.append("|-----------|-----|--------|----------|-----|")
    for r in card.results:
        lines.append(
            f"| {r.title} | {r.raw_score:.2f} | {r.normalized_weight:.2f} "
            f"| {r.weighted_score:.3f} | `{_bar(r.raw_score)}` |"
        )
    lines.append("")
    lines.append("## Evidence")
    lines.append("")
    for r in card.results:
        lines.append(f"### {r.title}")
        for e in r.evidence:
            lines.append(f"- {e}")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def ranking_markdown(ranking: Ranking) -> str:
    lines: list[str] = []
    lines.append(f"# Ranking for rubric `{ranking.rubric_id}`")
    lines.append("")
