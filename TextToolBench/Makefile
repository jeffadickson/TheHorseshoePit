# Windows entry points. Recipes use PowerShell only for bootstrap/package operations;
# benchmark orchestration and output verification are implemented in C#.
.DEFAULT_GOAL := help
.NOTPARALLEL:
RUN = powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/manage.ps1
.PHONY: help bootstrap deps source data native build test coverage profile grep-tuned benchmark report requirements all sed sed-test sed-coverage sed-mutation
help:
	$(RUN) -Action help
bootstrap:
	$(RUN) -Action bootstrap
deps:
	$(RUN) -Action deps
source:
	$(RUN) -Action source
build:
	$(RUN) -Action build
data: source build
	$(RUN) -Action data
native: deps source
	$(RUN) -Action native
test: build native data
	$(RUN) -Action test
coverage: build native data
	$(RUN) -Action coverage
profile: build native data
	$(RUN) -Action profile
grep-tuned: deps source
	$(RUN) -Action grep-tuned
benchmark: build native data grep-tuned sed
	$(RUN) -Action benchmark
report: build
	$(RUN) -Action report
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Overview.ps1
requirements:
	$(RUN) -Action requirements
all: benchmark report requirements

.PHONY: overview overview-test overview-mutation
overview:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Overview.ps1 -Native
overview-test:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Overview.ps1 -Mode Test
overview-mutation:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Overview.ps1 -Mode Mutation

# The sed extension acquires pinned official releases and keeps timing separate from instrumentation.
sed:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Run-SedBench.ps1 -Mode Benchmark
sed-test:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Run-SedBench.ps1 -Mode Test
sed-coverage:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Run-SedBench.ps1 -Mode Coverage
sed-mutation:
	powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/Run-SedBench.ps1 -Mode Mutation
