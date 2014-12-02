"""Engine determinism and evidence checks on the bundled example."""

import json
import unittest
from pathlib import Path

from scorerune.engine import score_candidate
from scorerune.loader import load_candidates, load_rubric

EX = Path(__file__).resolve().parent.parent / "examples"


class EngineTests(unittest.TestCase):
    @classmethod
