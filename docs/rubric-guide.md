# Rubric Authoring Guide

A rubric is a JSON object with an `id`, a `title`, an optional `description`, and
a `criteria` array. Each criterion declares a `weight`, a `kind`, and a `params`
object whose shape depends on the kind. Weights are relative: the engine
normalizes them by their sum, so a rubric with weights `3, 4, 2, 1` behaves the
same as one with `30, 40, 20, 10`.

```json
{
  "id": "my-rubric",
  "title": "My Rubric",
  "criteria": [
    { "id": "c1", "title": "...", "weight": 2.0, "kind": "phrase", "params": {} }
  ]
}
```

## Evidence kinds

### phrase

Checks for literal substrings.

| param | type | default | meaning |
|-------|------|---------|---------|
| `phrases` | list[str] | `[]` | substrings to look for |
| `mode` | `"any"` \| `"all"` | `"all"` | `any` scores 1.0 on first hit; `all` scores the found fraction |
| `case_sensitive` | bool | `false` | match casing exactly |

Score for `all` mode is `found / total`. For `any` mode it is `1.0` when at least
one phrase is present, else `0.0`.
