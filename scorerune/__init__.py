"""scorerune - a deterministic rubric engine for AI response review.

Public API surface:

    from scorerune import (
        Rubric, Criterion, Candidate, Scorecard, Ranking,
        score_candidate, rank_candidates,
        load_rubric, load_candidates,
    )
"""

from .model import (
    Candidate,
    Criterion,
    CriterionResult,
    Ranking,
    Rubric,
    Scorecard,
)
from .engine import rank_candidates, score_candidate, score_criterion
from .loader import LoadError, load_candidate_text, load_candidates, load_rubric

__version__ = "1.0.0"
