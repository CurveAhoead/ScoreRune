"""Loading and validation for rubric and candidate documents.

Documents are plain JSON so the standard library covers parsing. The loader
validates structure eagerly and raises LoadError with an aggregated, readable
message rather than letting a malformed rubric surface later as a scoring bug.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .model import Candidate, Rubric


class LoadError(Exception):
    """Raised when a document is missing, malformed, or fails validation."""


def _read_json(path: str | Path) -> Any:
    p = Path(path)
    if not p.exists():
        raise LoadError(f"file not found: {p}")
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise LoadError(f"invalid JSON in {p}: {exc}") from exc


def load_rubric(path: str | Path) -> Rubric:
    raw = _read_json(path)
    if not isinstance(raw, dict):
        raise LoadError("rubric document must be a JSON object")
    rubric = Rubric.from_dict(raw)
    errors = rubric.validate()
    if errors:
        raise LoadError("rubric validation failed:\n  - " + "\n  - ".join(errors))
    return rubric


def load_candidates(path: str | Path) -> list[Candidate]:
    raw = _read_json(path)
