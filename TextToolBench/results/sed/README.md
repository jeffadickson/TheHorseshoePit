# Sed comparison on Windows

The suite compares GNU sed 4.9, BusyBox sed, uutils/sed 0.3.0 and red 1.0.2. The official sd release bundle is tagged v1.1.0 but its executable reports v1.0.0; both facts are retained rather than silently changing the reported version. sd participates only in replacements. The Git and local MSYS2 GNU sed executables have the same SHA-256, so they count as one implementation.

These are official Windows distributions, not builds with identical compiler settings. No CPU tuning, LLVM advantage, or optimization level is inferred from the language. This does not cover every historical sed port, BSD sed, or Linux/WSL builds. None of these benchmark downloads replaces the user's default commands.

## Workloads and measurement

| Workload | sed script | Equivalent sd arguments |
|---|---|---|
| Literal substitution | `s/Prince/PRINCE/g` | `--preview -F Prince PRINCE FILE` |
| Numeric regex substitution | `s/[0-9][0-9]*/#/g` | `--preview '[0-9]+' '#' FILE` |
| Delete blank records | `/^[ \t]*$/d` | Unsupported |
| Select matching records | `-n /Prince/p` | Unsupported |
| First 100 records | `-n '1,100p;100q'` | Unsupported |
| Transliteration | `y/aeiou/AEIOU/` | Unsupported |

The actual scripts, flags, versions, executable hashes and qualified cells are in [environment.json](environment.json). GNU/BusyBox/red use binary mode; uutils has no corresponding option. LC_ALL=C. The five inputs are the existing approximately 1 KiB, 64 KiB, 1 MiB, 8 MiB and 32 MiB ASCII/LF corpus files.

There are 130 cells, each with one warmup and seven measured repeats: **910 measured commands plus 130 warmups**. A separate untimed qualification checks all 130 cells before the shuffled sequential timing loop (seed **20261107**). Wall time includes fresh process startup, output transfer and the SHA-256 sink. Processes are bounded by a 120-second timeout. Input hashes are checked after the run, guarding especially against accidental sd in-place edits. Source mutations, instrumentation and unit testing finish before timing begins.

The independent C# reference maps bytes reversibly with Latin-1, applies only ASCII transformations, and preserves CR, NUL, invalid UTF-8 bytes and a missing final LF. That is a byte convention, not a claim that the book is Latin-1 text. Every retained timing must match its reference byte count and SHA-256. No output newline normalization is allowed. Prefix selection stops after record 100; whole-file MiB/s is therefore intentionally blank for that task in the consolidated report.

See [the consolidated report](../report.html) and [raw observations](runs.jsonl). These are warm-cache Windows command measurements, not cold-disk throughput, a Linux comparison, or a universal ranking.

## Compatibility failures retained

Eleven edge/random fixtures produced **279 passes and seven failures**, recorded in [validation.json](validation.json), with the failing inputs retained as counterexample files.

- BusyBox and red add a final LF when explicitly printing a matching or first record that lacked its terminator: four failures.
- BusyBox differs on the fixture containing NUL and an invalid UTF-8 byte for blank-record deletion and the two selection operations: three failures.

These are compatibility failures, never passes. Every implementation nevertheless qualified on all its timed book-corpus cells. Binary and missing-newline results should not be generalized from the book timings. No shrinking claim is made for these fixed counterexamples; randomized fixtures did not fail.

## Verification and coverage

- **24 NUnit tests passed**, including 1,200 seeded arbitrary-byte idempotence checks, explicit expected examples, 99/100/101-record boundaries and negative task identifiers.
- **10 malformed-program/missing-file checks passed** across the five engines.
- **26 deliberate command mutants were detected**. Each mutant has a conforming unmodified control, preventing a compatibility failure from being counted as a mutation kill.
- **Stryker.NET 5.0.0: 91.67%** for the oracle source. Of 35 executed mutants, 33 were killed and two survived. One additional mutant was uncovered, 20 failed compilation, and 340 were ignored by source/block filters. [JSON report](mutation-report.json) and [HTML report](mutation-report.html) are retained.
- The surviving empty exception-message mutation changes no specified behavior. Changing the record loop's `<` to `<=` processes an extra empty, unterminated record that emits no bytes for these operations; it is observationally equivalent here. The uncovered mutation changes the message in a defensive private switch arm unreachable through the validated public entry point. These dispositions do not rewrite the raw score.
- Oracle unit coverage: **96.29% lines, 97.50% branches**. Fixture-only `SedBench` package coverage: **73.23% lines, 80.95% branches**. They are different scopes. The raw CLI coverage file also lists unrelated assemblies from the shared build directory at zero hits; the package figures exclude those unrelated assemblies. Third-party native tool coverage was not measured, and the timing loop itself is deliberately uninstrumented.
- **Three offline Pester tests passed** for cached digest acceptance, deliberate byte mutation rejection and a missing workspace. Pester 3 command coverage is **59.18% (29/49)**; it does not provide line/branch metrics. Network-failure and archive-extraction branches are not covered by these offline tests; the actual pinned downloads and extraction ran separately.

The reference uses built-in .NET byte/string/regex facilities. NUnit provides independent examples; Coverlet measures managed code and Stryker mutates the oracle. Seeded bounded properties run directly in NUnit rather than using FsCheck or an automatic shrinker. The controller is managed .NET 10, not Native AOT, to retain coverage/mutation compatibility; its process startup is outside the per-tool stopwatch. [Verification summary](verification-summary.json) records scope and versions.

## Reproduction

From an existing project checkout containing the five corpus files:

```powershell
$project = (Get-Location).Path
if (!(Test-Path -LiteralPath $project -PathType Container)) { throw 'Missing checkout' }
if (!(Test-Path -LiteralPath (Join-Path $project 'scripts') -PathType Container)) { throw 'Run from the project root' }
& (Join-Path $project 'scripts/Run-SedBench.ps1') -Mode Test
& (Join-Path $project 'scripts/Run-SedBench.ps1') -Mode Mutation
& (Join-Path $project 'scripts/Run-SedBench.ps1') -Mode Coverage
& (Join-Path $project 'scripts/Run-SedBench.ps1') -Mode Benchmark
& (Join-Path $project 'scripts/Run-SedBench.ps1') -Mode Report
```

Run verification and measurement sequentially, without other heavy workloads. Builds are incremental. The report also requires the earlier benchmark suites' retained raw files. Pinned download URLs/digests are in `scripts/sed-dependencies.json`; mutable BusyBox URLs fail closed if upstream bytes change. Versions, executable hashes and corpus hashes identify the actual timed artifacts.

Primary sources: [GNU sed](https://www.gnu.org/software/sed/), [BusyBox-w32](https://frippery.org/busybox/), [uutils/sed releases](https://github.com/uutils/sed/releases/tag/0.3.0), [red releases](https://github.com/uutils/red/releases/tag/v1.0.2), [sd releases](https://github.com/chmln/sd/releases/tag/v1.1.0), [NUnit installation](https://docs.nunit.org/articles/nunit/getting-started/installation.html), [Stryker.NET configuration](https://stryker-mutator.io/docs/stryker-net/configuration/).
