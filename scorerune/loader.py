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
    # Accept either a bare list or an object with a "candidates" array.
    if isinstance(raw, dict):
        raw = raw.get("candidates", [])
    if not isinstance(raw, list):
        raise LoadError("candidate document must be a JSON array or "
                        "an object with a 'candidates' array")
    candidates: list[Candidate] = []
    seen: set[str] = set()
    errors: list[str] = []
    for i, item in enumerate(raw):
        if not isinstance(item, dict):
            errors.append(f"candidate #{i} is not an object")
            continue
        cand = Candidate.from_dict(item)
        if not cand.id:
            errors.append(f"candidate #{i} is missing an id")
        if cand.id in seen:
            errors.append(f"duplicate candidate id '{cand.id}'")
        seen.add(cand.id)
        candidates.append(cand)
    if errors:
        raise LoadError("candidate validation failed:\n  - " + "\n  - ".join(errors))
    if not candidates:
        raise LoadError("no candidates found")
    return candidates


def load_candidate_text(path: str | Path, candidate_id: str = "") -> Candidate:
    """Load a single candidate from a raw text file (not JSON)."""
    p = Path(path)
    if not p.exists():
        raise LoadError(f"file not found: {p}")
    text = p.read_text(encoding="utf-8")
    cid = candidate_id or p.stem
    return Candidate(id=cid, text=text, label=cid)

# draft note 4
