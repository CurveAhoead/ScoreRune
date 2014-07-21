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


@dataclass(frozen=True)
class Criterion:
    """A single scored dimension of a rubric.

    weight     relative importance; the engine normalizes weights per rubric.
    kind       one of EVIDENCE_KINDS.
    params     kind-specific configuration (see engine.py for semantics).
    """

    id: str
    title: str
    weight: float
    kind: str
    params: dict[str, Any] = field(default_factory=dict)
    description: str = ""

    def validate(self) -> list[str]:
        errors: list[str] = []
        if not self.id:
            errors.append("criterion is missing an id")
        if self.weight <= 0:
            errors.append(f"criterion '{self.id}' weight must be positive")
        if self.kind not in EVIDENCE_KINDS:
            errors.append(
                f"criterion '{self.id}' has unknown kind '{self.kind}'; "
                f"expected one of {', '.join(EVIDENCE_KINDS)}"
            )
        return errors

    @staticmethod
    def from_dict(raw: dict[str, Any]) -> "Criterion":
        return Criterion(
            id=str(raw.get("id", "")),
            title=str(raw.get("title", raw.get("id", ""))),
            weight=float(raw.get("weight", 1.0)),
            kind=str(raw.get("kind", "phrase")),
            params=dict(raw.get("params", {})),
            description=str(raw.get("description", "")),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "title": self.title,
            "weight": self.weight,
            "kind": self.kind,
            "params": self.params,
            "description": self.description,
        }

