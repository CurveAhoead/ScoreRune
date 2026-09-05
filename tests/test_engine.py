"""Engine determinism and evidence checks on the bundled example."""

import json
import unittest
from pathlib import Path

from scorerune.engine import score_candidate
from scorerune.loader import load_candidates, load_rubric

EX = Path(__file__).resolve().parent.parent / "examples"


class EngineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rubric = load_rubric(str(EX / "rubric.json"))
        cls.candidates = load_candidates(str(EX / "candidates.json"))

    def test_scores_in_range(self):
        for cand in self.candidates:
            card = score_candidate(self.rubric, cand)
            self.assertGreaterEqual(card.total, 0.0)
            self.assertLessEqual(card.total, 1.0)

    def test_deterministic(self):
        first = [score_candidate(self.rubric, c).total for c in self.candidates]
        second = [score_candidate(self.rubric, c).total for c in self.candidates]
        self.assertEqual(first, second)

    def test_criterion_ids_present(self):
        card = score_candidate(self.rubric, self.candidates[0])
        ids = {r.criterion_id for r in card.results}
        expected = {cr.id for cr in self.rubric.criteria}
        self.assertEqual(ids, expected)


if __name__ == "__main__":
    unittest.main()


