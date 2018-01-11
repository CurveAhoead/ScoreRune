# Rubric Authoring Guide

A rubric is a JSON object with an `id`, a `title`, an optional `description`, and
a `criteria` array. Each criterion declares a `weight`, a `kind`, and a `params`
object whose shape depends on the kind. Weights are relative: the engine
normalizes them by their sum, so a rubric with weights `3, 4, 2, 1` behaves the
same as one with `30, 40, 20, 10`.
