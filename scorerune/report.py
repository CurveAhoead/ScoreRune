"""Rendering of scorecards and rankings into Markdown or JSON.

The reporters are pure string producers so callers decide where output goes.
JSON output uses sorted keys and a fixed indent to keep results diff-stable.
"""

