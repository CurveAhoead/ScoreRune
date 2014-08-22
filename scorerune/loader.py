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
