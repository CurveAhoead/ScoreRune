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
build: py-compile dotnet-build

## Rank the example batch with the Python CLI.
demo:
	$(PY) -m scorerune rank -r examples/rubric.json -c examples/candidates.json -f md

## Rank the example batch with the C# runtime.
demo-cs: dotnet-build
	$(DOTNET) run -c Release --no-build --project $(RUNTIME) -- \
		rank -r examples/rubric.json -c examples/candidates.json -f md

## Validate the example rubric and candidates.
validate:
	$(PY) -m scorerune validate -r examples/rubric.json -c examples/candidates.json

## Remove build artifacts.
clean:
	$(DOTNET) clean -c Release $(RUNTIME) || true
	rm -rf runtime/bin runtime/obj
	find scorerune -name __pycache__ -type d -exec rm -rf {} +

# draft note 62
