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

