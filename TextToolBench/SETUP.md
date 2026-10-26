# TextToolBench setup

The retained results were measured on Windows using .NET 10 and native Windows/MSYS tools. Historical environment files identify their exact binaries, source revisions, flags and corpus hashes. Do not replace those labels with newly installed tool versions.

The repository package includes a Windows Makefile and `scripts/manage.ps1` for dependency/source acquisition and the original suites. `make help` lists those targets. `make source` retrieves the public-domain corpus and pinned GNU archives; `make data` prepares the deterministic corpus sizes. The large files are scaling derivatives of one book, not independent texts.

The sed extension also has a standalone PowerShell entry point, `scripts/Run-SedBench.ps1`, with `Benchmark`, `Test`, `Coverage`, `Mutation` and `Report` modes. It validates cached downloads against `scripts/sed-dependencies.json`, builds incrementally with .NET 10, and never installs those comparison tools as defaults. It requires Git for Windows' GNU sed and the five existing corpus inputs. See [the sed methods](results/sed/README.md) for checked commands, dependency versions and coverage scope.

Reacquisition requires network access. Run benchmark suites sequentially and keep instrumentation, compilation and mutation testing separate from the timing campaign. Preserve raw JSONL files before rerunning a suite: the harnesses intentionally create a fresh result file rather than mixing sessions. The consolidated report requires complete seven-repeat matrices and matching output hashes.

The tuned awk/grep user installation is a separate experiment: see [its installation and rollback evidence](results/awk-tuned/README.md). The frawk fix is also separate from the original executable used in historical measurements. No successful Windows frawk JIT timings are claimed.

A clean-machine make all bootstrap has not been exercised end to end. The recorded measurement and verification campaigns ran on this Windows host. The MSYS2 base archive is pinned, but subsequent pacman packages may advance; regenerated compiler/runtime metadata must identify any new run rather than inheriting the historical labels.

