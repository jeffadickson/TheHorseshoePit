# TextToolBench setup

The retained results were measured on Windows using .NET 10 and native Windows/MSYS tools. Historical environment files identify their exact binaries, source revisions, flags and corpus hashes. Do not replace those labels with newly installed tool versions.

The repository package includes a Windows Makefile and `scripts/manage.ps1` for dependency/source acquisition and the original suites. `make help` lists those targets. `make source` retrieves the public-domain corpus and pinned GNU archives; `make data` prepares the deterministic corpus sizes. The large files are scaling derivatives of one book, not independent texts.

The sed extension also has a standalone PowerShell entry point, `scripts/Run-SedBench.ps1`, with `Benchmark`, `Test`, `Coverage`, `Mutation` and `Report` modes. It validates cached downloads against `scripts/sed-dependencies.json`, builds incrementally with .NET 10, and never installs those comparison tools as defaults. It requires Git for Windows' GNU sed and the five existing corpus inputs. See [the sed methods](results/sed/README.md) for checked commands, dependency versions and coverage scope.

Reacquisition requires network access. Run benchmark suites sequentially and keep instrumentation, compilation and mutation testing separate from the timing campaign. Preserve raw JSONL files before rerunning a suite: the harnesses intentionally create a fresh result file rather than mixing sessions. The consolidated report requires complete seven-repeat matrices and matching output hashes.

The tuned awk/grep user installation is a separate experiment: see [its installation and rollback evidence](results/awk-tuned/README.md). The frawk fix is also separate from the original executable used in historical measurements. No successful Windows frawk JIT timings are claimed.

## Offline HTML overview

Open `results/overview.html` for the narrative overview and interactive cohort-aware charts. All 427 summaries are embedded; there are no external scripts, fonts or runtime data requests. Relative evidence links resolve inside the checkout.

From the project root, check that the scripts directory exists, then rebuild:

```powershell
$project=(Get-Location).Path
if (!(Test-Path -LiteralPath (Join-Path $project 'scripts') -PathType Container)) { throw 'Run from the checkout root' }
& (Join-Path $project 'scripts/Build-Overview.ps1') -Native
```

`-Native` builds and runs a Windows x64 Native AOT helper using .NET 10 and the C++ build tools. Omit it for a managed build. The two implementations were checked for byte-identical HTML output. `make overview` selects the native build; `make report` also regenerates the overview after the detailed tables. `make overview-test` runs the test suite, and `make overview-mutation` selects the dedicated Stryker configuration.

Optional non-rendering interaction tests require Node.js compatible with jsdom 30.1.3 (the tested runtime is Node 24.19.0). Install `scripts/overview-dom-package.json` into a local `tools/overview-dom-tests` directory with the retained `scripts/overview-dom-lock.yaml` as its pnpm lockfile, then run `scripts/Test-OverviewDom.cjs` from the project root using Node's test runner. jsdom is a development-only DOM implementation; it is not a browser or a renderer. The generator and final HTML do not need Node. See `results/overview-verification/README.md` for exact verification scope and the visual-preview limitation.

A clean-machine make all bootstrap has not been exercised end to end. The retained campaigns ran on this Windows host. The MSYS2 base is pinned but later pacman packages may advance; record actual compiler/runtime metadata for any new run.

## Diff extension

The separate [diff laboratory](results/diff/report.html) compares GNU, Rust uutils, BusyBox and four Git algorithms. It verifies patches by independent application, rather than requiring identical patch text. See [methods and verification](results/diff/README.md).

After the existing dependency bootstrap, run `make diff-build` to acquire hash-pinned sources and build generic/native variants; `make diff-test`, `make diff-coverage` and `make diff-mutation` verify the helper separately. `make diff` runs the uninstrumented campaign and generates its standalone report. Do not overlap timing with builds or other benchmark suites. Existing result files are overwritten on an explicit rerun; archive a session first. These targets do not change installed defaults.

