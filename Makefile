# ScoreRune build helpers. Compile-only; no test suites.

PY ?= python
DOTNET ?= dotnet
RUNTIME := runtime/ScoreRune.Runtime.csproj

.PHONY: all py-compile dotnet-build build demo demo-cs validate clean

all: build

## Byte-compile the Python package.
py-compile:
	$(PY) -m compileall scorerune

## Restore-free Release build of the C# runtime.
dotnet-build:
	$(DOTNET) build -c Release $(RUNTIME)

## Compile both runtimes.
