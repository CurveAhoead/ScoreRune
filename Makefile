# ScoreRune build helpers. Compile-only; no test suites.

PY ?= python
DOTNET ?= dotnet
RUNTIME := runtime/ScoreRune.Runtime.csproj

.PHONY: all py-compile dotnet-build build demo demo-cs validate clean

all: build

## Byte-compile the Python package.
