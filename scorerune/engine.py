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
