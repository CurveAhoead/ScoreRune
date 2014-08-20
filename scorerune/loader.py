"""Loading and validation for rubric and candidate documents.

Documents are plain JSON so the standard library covers parsing. The loader
validates structure eagerly and raises LoadError with an aggregated, readable
message rather than letting a malformed rubric surface later as a scoring bug.
"""

