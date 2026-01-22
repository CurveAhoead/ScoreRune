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


@dataclass(frozen=True)
class Rubric:
    """A named collection of weighted criteria."""

    id: str
    title: str
    criteria: tuple[Criterion, ...]
    description: str = ""

    def validate(self) -> list[str]:
        errors: list[str] = []
        if not self.id:
            errors.append("rubric is missing an id")
        if not self.criteria:
            errors.append(f"rubric '{self.id}' has no criteria")
        seen: set[str] = set()
        for c in self.criteria:
            if c.id in seen:
                errors.append(f"rubric '{self.id}' has duplicate criterion id '{c.id}'")
            seen.add(c.id)
            errors.extend(c.validate())
        return errors

    def total_weight(self) -> float:
        return sum(c.weight for c in self.criteria)

    @staticmethod
    def from_dict(raw: dict[str, Any]) -> "Rubric":
        criteria = tuple(Criterion.from_dict(c) for c in raw.get("criteria", []))
        return Rubric(
            id=str(raw.get("id", "")),
            title=str(raw.get("title", raw.get("id", ""))),
            criteria=criteria,
            description=str(raw.get("description", "")),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "title": self.title,
            "description": self.description,
            "criteria": [c.to_dict() for c in self.criteria],
        }


@dataclass(frozen=True)
class Candidate:
    """A single AI response to be reviewed against a rubric."""

    id: str
    text: str
    label: str = ""

    @staticmethod
    def from_dict(raw: dict[str, Any]) -> "Candidate":
        return Candidate(
            id=str(raw.get("id", "")),
            text=str(raw.get("text", "")),
            label=str(raw.get("label", raw.get("id", ""))),
        )

    def to_dict(self) -> dict[str, Any]:
        return {"id": self.id, "text": self.text, "label": self.label}


@dataclass(frozen=True)
class CriterionResult:
    """Outcome of scoring one criterion against one candidate."""

    criterion_id: str
    title: str
    raw_score: float          # 0.0 .. 1.0 before weighting
    weight: float
    normalized_weight: float  # weight / rubric total
    weighted_score: float     # raw_score * normalized_weight
    evidence: list[str]

    def to_dict(self) -> dict[str, Any]:
        return {
            "criterion_id": self.criterion_id,
            "title": self.title,
            "raw_score": round(self.raw_score, 6),
            "weight": self.weight,
            "normalized_weight": round(self.normalized_weight, 6),
            "weighted_score": round(self.weighted_score, 6),
            "evidence": self.evidence,
        }


@dataclass(frozen=True)
class Scorecard:
    """Aggregate result for one candidate against a rubric."""

    candidate_id: str
    label: str
    rubric_id: str
    total: float  # 0.0 .. 1.0
    results: list[CriterionResult]

    def percent(self) -> float:
        return round(self.total * 100.0, 2)

    def to_dict(self) -> dict[str, Any]:
        return {
            "candidate_id": self.candidate_id,
            "label": self.label,
            "rubric_id": self.rubric_id,
            "total": round(self.total, 6),
            "percent": self.percent(),
            "results": [r.to_dict() for r in self.results],
        }


@dataclass(frozen=True)
class Ranking:
    """Ordered scorecards for a batch of candidates."""

    rubric_id: str
    scorecards: list[Scorecard]

    def to_dict(self) -> dict[str, Any]:
        return {
            "rubric_id": self.rubric_id,
            "ranking": [
                {"rank": i + 1, "candidate_id": s.candidate_id,
                 "label": s.label, "percent": s.percent()}
                for i, s in enumerate(self.scorecards)
            ],
            "scorecards": [s.to_dict() for s in self.scorecards],
        }
