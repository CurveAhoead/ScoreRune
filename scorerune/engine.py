"""Deterministic scoring engine.

Given a rubric and a candidate, the engine computes a raw score in [0, 1] for
each criterion using a pure function keyed on the criterion kind, then combines
the weighted contributions into a single total. There is no randomness and no
external state, so identical inputs always yield identical scorecards.

Evidence kinds
--------------
phrase          params: phrases (list[str]), mode ("any"|"all"), case_sensitive (bool)
                score  = fraction of required phrases present (mode="all" needs all,
                         mode="any" scores 1.0 as soon as one is found).
length          params: min_words, max_words, ideal_words (optional)
                score  = 1.0 inside [min, max]; linear falloff toward 0 outside.
                         If ideal_words given, peak is at ideal and tapers to the
                         bounds.
keyword_density params: keywords (list[str]), target (float, occurrences per 100
                         words), tolerance (float)
                score  = 1.0 when measured density is within tolerance of target,
                         linear falloff outside.
structure       params: require_lists (bool), require_headings (bool),
                         min_paragraphs (int)
                score  = fraction of satisfied structural requirements.
"""

from __future__ import annotations

import re

from .model import (
    Candidate,
    Criterion,
    CriterionResult,
    Ranking,
    Rubric,
    Scorecard,
)

_WORD_RE = re.compile(r"[A-Za-z0-9']+")
_HEADING_RE = re.compile(r"^\s{0,3}#{1,6}\s+\S", re.MULTILINE)
_LIST_RE = re.compile(r"^\s*(?:[-*+]|\d+\.)\s+\S", re.MULTILINE)


def _words(text: str) -> list[str]:
    return _WORD_RE.findall(text)


def _clamp01(x: float) -> float:
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x


def _score_phrase(text: str, params: dict) -> tuple[float, list[str]]:
    phrases = [str(p) for p in params.get("phrases", [])]
    if not phrases:
        return 0.0, ["no phrases configured"]
    case_sensitive = bool(params.get("case_sensitive", False))
    haystack = text if case_sensitive else text.lower()
    found: list[str] = []
    missing: list[str] = []
    for p in phrases:
        needle = p if case_sensitive else p.lower()
        if needle in haystack:
            found.append(p)
        else:
            missing.append(p)
    mode = str(params.get("mode", "all")).lower()
    if mode == "any":
        score = 1.0 if found else 0.0
    else:
        score = len(found) / len(phrases)
    evidence = [f"found: {', '.join(found) or 'none'}"]
    if missing:
        evidence.append(f"missing: {', '.join(missing)}")
    return _clamp01(score), evidence


def _score_length(text: str, params: dict) -> tuple[float, list[str]]:
    n = len(_words(text))
    lo = int(params.get("min_words", 0))
    hi = int(params.get("max_words", 10_000_000))
    ideal = params.get("ideal_words")
    if ideal is not None:
        ideal = int(ideal)
        if n == ideal:
            score = 1.0
        elif n < ideal:
            span = max(ideal - lo, 1)
            score = _clamp01((n - lo) / span)
        else:
            span = max(hi - ideal, 1)
            score = _clamp01((hi - n) / span)
    else:
        if lo <= n <= hi:
            score = 1.0
        elif n < lo:
            score = _clamp01(n / lo) if lo > 0 else 0.0
        else:
            over = n - hi
            score = _clamp01(1.0 - over / max(hi, 1))
    return score, [f"word count = {n} (bounds {lo}..{hi}"
                   + (f", ideal {ideal})" if ideal is not None else ")")]


def _score_keyword_density(text: str, params: dict) -> tuple[float, list[str]]:
    keywords = [str(k).lower() for k in params.get("keywords", [])]
    words = [w.lower() for w in _words(text)]
    total = len(words) or 1
    hits = sum(1 for w in words if w in keywords)
    density = hits / total * 100.0
    target = float(params.get("target", 2.0))
    tol = float(params.get("tolerance", 1.0)) or 1.0
    diff = abs(density - target)
    score = _clamp01(1.0 - max(diff - tol, 0.0) / max(target, 1.0))
    return score, [f"density = {density:.2f}/100 words "
                   f"(target {target:.2f} +/- {tol:.2f}, hits {hits})"]


def _score_structure(text: str, params: dict) -> tuple[float, list[str]]:
    checks: list[bool] = []
    evidence: list[str] = []
    if params.get("require_headings"):
        ok = _HEADING_RE.search(text) is not None
        checks.append(ok)
        evidence.append(f"headings: {'yes' if ok else 'no'}")
    if params.get("require_lists"):
        ok = _LIST_RE.search(text) is not None
        checks.append(ok)
        evidence.append(f"lists: {'yes' if ok else 'no'}")
    min_paras = int(params.get("min_paragraphs", 0))
    if min_paras > 0:
        paras = [b for b in re.split(r"\n\s*\n", text.strip()) if b.strip()]
        ok = len(paras) >= min_paras
        checks.append(ok)
        evidence.append(f"paragraphs: {len(paras)} (need {min_paras})")
    if not checks:
        return 0.0, ["no structural requirements configured"]
    return sum(1 for c in checks if c) / len(checks), evidence
