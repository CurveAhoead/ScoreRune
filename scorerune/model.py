"""Data model for rubrics, candidates, and scoring results.

The model layer is intentionally free of I/O concerns. It defines immutable-ish
value objects that the scoring engine consumes and that the reporters render.
All structures round-trip through plain dictionaries so they serialize to JSON
with the standard library alone.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


# Recognized evidence kinds. A criterion declares exactly one kind and the
# engine dispatches on it. Keeping the set closed makes scoring deterministic
# and lets the loader validate rubrics up front.
EVIDENCE_KINDS = ("phrase", "length", "keyword_density", "structure")


