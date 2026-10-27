# Text-tool benchmark results

[Open the experiment overview](results/overview.html) for the methodology, key findings, cohort-aware interactive charts and verification limitations.

2989 measured runs plus 427 warmups: original suites 1260; mawk/frawk interpreter/fresh GNU awk 315; controlled Rust buffering experiment 84; same-source GNU grep build-flags experiment 420; sed comparison 910. All timed outputs verified by length and SHA-256. Profiles and bug probes are separate diagnostic evidence, not extra benchmark samples.

## Sed implementations across five sizes

GNU sed, BusyBox sed, uutils/sed and red run six workloads; sd runs the two replacements only. Git/MSYS2 sed binaries are byte-identical and are counted once. These are official Windows distributions, not a controlled compiler comparison. [Sed methods, provenance, compatibility and verification](results/sed/README.md). Median milliseconds; unsupported or rejected cells are shown as —.

| Input bytes | Workload | GNU sed | BusyBox | uutils/sed | red | sd |
|---:|---|---:|---:|---:|---:|---:|
| 1071 | sed-head | 51.213 | 61.160 | 40.014 | 38.233 | — |
| 1071 | sed-literal | 50.795 | 54.486 | 43.761 | 44.124 | 50.233 |
| 1071 | sed-nonblank | 52.680 | 74.587 | 39.353 | 38.772 | — |
| 1071 | sed-regex | 56.745 | 57.773 | 38.290 | 49.658 | 39.617 |
| 1071 | sed-select | 56.719 | 54.239 | 54.877 | 44.308 | — |
| 1071 | sed-transliterate | 53.693 | 59.138 | 45.298 | 46.214 | — |
| 65580 | sed-head | 54.179 | 57.050 | 36.411 | 40.486 | — |
| 65580 | sed-literal | 54.240 | 56.045 | 40.777 | 45.683 | 56.069 |
| 65580 | sed-nonblank | 50.206 | 56.126 | 41.530 | 68.757 | — |
| 65580 | sed-regex | 56.832 | 55.414 | 40.982 | 51.142 | 52.203 |
| 65580 | sed-select | 50.288 | 73.203 | 45.327 | 45.020 | — |
| 65580 | sed-transliterate | 57.280 | 57.802 | 40.014 | 54.698 | — |
| 1048616 | sed-head | 59.002 | 62.658 | 39.286 | 46.203 | — |
| 1048616 | sed-literal | 69.726 | 80.691 | 51.270 | 84.263 | 132.238 |
| 1048616 | sed-nonblank | 68.711 | 66.902 | 61.858 | 384.630 | — |
| 1048616 | sed-regex | 79.576 | 69.640 | 65.363 | 167.237 | 114.898 |
| 1048616 | sed-select | 63.586 | 71.623 | 50.598 | 76.189 | — |
| 1048616 | sed-transliterate | 68.744 | 70.322 | 46.601 | 81.225 | — |
| 8388671 | sed-head | 52.845 | 54.288 | 51.412 | 135.941 | — |
| 8388671 | sed-literal | 160.483 | 171.369 | 130.643 | 318.282 | 583.777 |
| 8388671 | sed-nonblank | 163.675 | 179.440 | 174.043 | 2663.325 | — |
| 8388671 | sed-regex | 158.472 | 163.717 | 171.556 | 901.465 | 650.287 |
| 8388671 | sed-select | 106.115 | 141.895 | 84.648 | 275.032 | — |
| 8388671 | sed-transliterate | 138.216 | 200.308 | 113.573 | 404.231 | — |
| 33554436 | sed-head | 54.393 | 58.737 | 48.950 | 406.496 | — |
| 33554436 | sed-literal | 469.133 | 494.126 | 330.390 | 1285.553 | 2364.906 |
| 33554436 | sed-nonblank | 465.934 | 559.143 | 455.239 | 11630.574 | — |
| 33554436 | sed-regex | 429.778 | 521.691 | 542.070 | 3319.666 | 2563.963 |
| 33554436 | sed-select | 298.344 | 353.408 | 196.500 | 1081.944 | — |
| 33554436 | sed-transliterate | 403.361 | 649.517 | 278.695 | 1263.057 | — |

Known edge incompatibilities remain failures, even when that implementation matches every timed ASCII/LF corpus cell. Every retained timing is checked byte-for-byte by output length and SHA-256; no newline normalization is used. Replacement-only sd uses preview mode; input hashes are rechecked to guard against accidental in-place edits.

Sed verification: 279 edge/random equivalence passes and seven retained compatibility failures; ten malformed-program/missing-file checks; 26 command mutants detected with conforming controls; 24 oracle unit tests including 1,200 seeded byte-property cases. Stryker.NET 5.0.0 oracle score 91.67%: 33 killed, two survived, one uncovered; dispositions are in the sed methods. Fixture-only SedBench package coverage is 73.23% lines / 80.95% branches; oracle unit scope is 96.29% / 97.50%. These scopes do not measure the third-party binaries. Three offline download-script Pester tests passed, with 59.18% command coverage; line/branch metrics are unavailable in that Pester version.

See [SETUP.md](SETUP.md) for the Windows Makefile workflow and dependency/source acquisition. Historical measurements retain their original host metadata. Current portability changes affect tool discovery, not the measured text algorithms. Fresh controls are labelled separately so medians from different sessions are never silently combined.

## Same-source GNU grep build flags: 32 MiB median milliseconds

| Task | Rebuilt generic O2 | Rebuilt native O3 | Installed grep | O2 / O3 |
|---|---:|---:|---:|---:|
| literal | 112.03 | 109.35 | 101.88 | 1.025× |
| regex | 137.02 | 125.96 | 126.83 | 1.088× |
| count | 104.86 | 99.24 | 92.23 | 1.057× |
| nonblank | 219.70 | 209.31 | 211.16 | 1.050× |

Both new grep 3.0 builds use GCC 16.2.0, the same MSYS2 runtime and configure choices. Generic flags are -O2 -march=nocona -msahf -mtune=generic -pipe. Tuned flags are -O3 -march=native -mtune=native -ftree-vectorize -pipe with vectorization diagnostics. GCC resolves native to Skylake/AVX2 on this i5-10505, and reports 32-byte vectorization in DFA code. This does not prove those loops dominate the benchmark. Installed grep is a separate GCC 13.3.0/runtime reference, not a controlled compiler-flags comparison. Seven repetitions, warm cache, fresh processes, seed 20261104; all startup/stdout hashing costs included. Small differences should be judged with the retained ranges. Complete flags, compiler, target features and configure/build logs are in results/grep-tuned.

## Profile-driven Rust stdout buffering: 32 MiB median milliseconds

| Implementation / task | Fresh original | 64 KiB buffer | Original / buffered |
|---|---:|---:|---:|
| rawk-stefan / field | 4514.99 | 2933.91 | 1.54× |
| rawk-stefan / words | 1759.27 | 1850.39 | 0.95× |
| rawk-stefan / lines | 403.94 | 409.71 | 0.99× |
| awk-rs-quinn / field | 4166.64 | 839.22 | 4.96× |
| awk-rs-quinn / words | 819.62 | 858.93 | 0.95× |
| awk-rs-quinn / lines | 780.18 | 744.86 | 1.05× |

Only CLI stdout buffering changed in these separately built variants. Patches and paired measurements are retained. Original source trees and executables remain unchanged. [Profiling explanation](results/profiles/EXPLANATION.md) contains wall-clock stack-sampling evidence and its limitations. It is not an ETW CPU-time or allocation-count profile. Buffering explains a substantial part of field-output overhead, while counting profiles still expose copying and field allocation.

## mawk and frawk bytecode interpreter: median milliseconds

| Input bytes | Task | mawk | frawk interpreter | Fresh GNU awk |
|---:|---|---:|---:|---:|
| 1071 | field | 60.49 | 84.29 | 95.48 |
| 1071 | lines | 69.06 | 82.30 | 89.66 |
| 1071 | words | 56.16 | 77.40 | 89.81 |
| 65580 | field | 66.44 | 71.66 | 92.83 |
| 65580 | lines | 61.95 | 70.31 | 100.80 |
| 65580 | words | 67.57 | 73.43 | 92.98 |
| 1048616 | field | 84.55 | 94.10 | 100.96 |
| 1048616 | lines | 64.41 | 76.06 | 99.84 |
| 1048616 | words | 79.51 | 79.73 | 104.11 |
| 8388671 | field | 157.28 | 141.47 | 251.76 |
| 8388671 | lines | 65.64 | 121.87 | 118.50 |
| 8388671 | words | 135.81 | 128.79 | 180.70 |
| 33554436 | field | 424.78 | 304.18 | 622.31 |
| 33554436 | lines | 107.63 | 244.01 | 220.02 |
| 33554436 | words | 320.96 | 294.06 | 463.45 |

mawk is the author's native MinGW64 1.3.4 20260302 build, using BINMODE=3 for LF stdout. frawk 0.4.8 is built with default features disabled and a retained resolved dependency lock. **The Windows Cranelift JIT panics and has no successful timings here; these are bytecode-interpreter results.** Two long-field conformance checks failed in the original frawk and are recorded, not counted as passes. Every timed book-corpus output still matches exactly. The separate [long-field bug fix](results/frawk-bug/README.md) passes the minimized and randomized regressions; the benchmark retains the original frawk binary. These three tools were interleaved with seed 20261101, one warmup and seven repeats across five sizes.

Downloaded [War and Peace from Project Gutenberg](https://www.gutenberg.org/cache/epub/2600/pg2600.txt) (3,359,610 bytes). Derivatives use ASCII substitutions and LF line endings, repeat the book as needed, and stop at a complete line near 1, 8 or 32 MiB. These are synthetic scaling inputs, not three independent corpora.

Windows build 26200; Intel Core i5-10505, 6 cores/12 logical processors, 15.74 GiB RAM; .NET 10.0.14; PowerShell 7.6.5; GNU grep 3.0, sed 4.9, awk 5.4.1 from Git for Windows. GNU tools run natively through the MSYS runtime, not WSL.

## Small files: median milliseconds

| Size | Task | ripgrep | GNU grep | PS pipeline | PS reader | Select-String |
|---|---|---:|---:|---:|---:|---:|
| 1071 bytes | count | 57.08 | 67.58 | 723.76 | 659.61 | 660.41 |
| 1071 bytes | literal | 62.48 | 65.25 | 644.22 | 638.47 | 714.37 |
| 1071 bytes | regex | 51.20 | 67.82 | 712.51 | 617.46 | 642.62 |
| 65580 bytes | count | 53.05 | 68.23 | 762.62 | 636.98 | 686.84 |
| 65580 bytes | literal | 51.53 | 62.55 | 717.99 | 671.76 | 709.52 |
| 65580 bytes | regex | 54.11 | 59.24 | 759.70 | 656.78 | 713.83 |

Small files are contiguous slices of the existing ASCII/LF corpus near its first Prince match, ending at a complete line around 1 KiB and 64 KiB. All five implementations were interleaved in the same session, with seed 20261029, one warmup and seven measured repetitions. The timer includes starting a fresh process and hashing stdout. These results describe whole-command latency; they do not isolate startup or represent commands inside an already running PowerShell session. Exact sizes, source offset, match counts and SHA-256 values are retained in results/small-environment.json.

## Both rawk implementations versus fresh GNU awk: median milliseconds

| Input bytes | Task | stefanalfbo/rawk | quinnjr/awk-rs | GNU awk |
|---:|---|---:|---:|---:|
| 1071 | field | 55.49 | 50.17 | 75.47 |
| 1071 | lines | 50.28 | 53.61 | 82.60 |
| 1071 | words | 62.68 | 57.63 | 72.05 |
| 65580 | field | 69.24 | 61.61 | 75.39 |
| 65580 | lines | 51.07 | 50.75 | 73.22 |
| 65580 | words | 57.06 | 55.40 | 74.22 |
| 1048616 | field | 213.35 | 162.51 | 78.35 |
| 1048616 | lines | 66.50 | 94.80 | 73.47 |
| 1048616 | words | 141.03 | 83.83 | 78.49 |
| 8388671 | field | 1255.48 | 944.98 | 227.37 |
| 8388671 | lines | 163.37 | 215.33 | 94.00 |
| 8388671 | words | 466.89 | 260.27 | 162.24 |
| 33554436 | field | 4784.01 | 3663.37 | 513.44 |
| 33554436 | lines | 426.34 | 694.64 | 166.82 |
| 33554436 | words | 1655.08 | 920.30 | 350.36 |

These are [stefanalfbo/rawk](https://github.com/stefanalfbo/rawk) and [quinnjr/rawk](https://github.com/quinnjr/rawk), whose binary is awk-rs. Both are unmodified release builds using cargo build --release --locked and the checked-in release profiles. Rust is local to tools/, with no permanent shell PATH change. results/rawk-environment.json records compiler version, source commits, lockfile/executable hashes and clean-source status. The first rawk has no --version option, so its manifest version identifies it. All three tools ran sequentially in interleaved order with seed 20261030. Scripts are identical: NF {print $1}; {n+=NF} END {print n+0}; END {print NR+0}. Each cell has one warmup and seven repetitions. This is a native Windows MSVC versus Git/MSYS comparison, including startup and stdout hashing, with a 120-second per-process timeout. No upstream Rust source was modified or instrumented; benchmark fixture coverage is separate.

## Largest file: median seconds

| Workload | GNU | PS pipeline | PS reader | Pipeline / GNU | Reader / GNU |
|---|---:|---:|---:|---:|---:|
| count | 0.097 | 10.024 | 4.989 | 103.2× | 51.4× |
| field | 0.383 | 9.303 | 5.723 | 24.3× | 14.9× |
| lines | 0.114 | 5.997 | 3.564 | 52.5× | 31.2× |
| literal | 0.109 | 10.278 | 5.160 | 94.0× | 47.2× |
| nonblank | 0.227 | 15.853 | 8.269 | 70.0× | 36.5× |
| regex | 0.132 | 9.085 | 3.844 | 68.6× | 29.0× |
| replace | 0.384 | 13.048 | 10.430 | 34.0× | 27.2× |
| words | 0.256 | 15.076 | 11.065 | 58.9× | 43.2× |

### Select-String, largest file

| Workload | Select-String seconds | Select-String / GNU |
|---|---:|---:|
| count | 0.725 | 7.5× |
| literal | 1.202 | 11.0× |
| regex | 1.120 | 8.5× |

### Ripgrep versus contemporaneous GNU grep, largest file

| Workload | ripgrep seconds | Fresh grep seconds | grep / ripgrep |
|---|---:|---:|---:|
| count | 0.1962 | 0.2235 | 1.14× |
| literal | 0.1632 | 0.2993 | 1.83× |
| nonblank | 0.4300 | 0.5374 | 1.25× |
| regex | 0.1704 | 0.3342 | 1.96× |

Installed winget package BurntSushi.ripgrep.MSVC: ripgrep 15.2.0 (rev e89fff89ac). The executable path and SHA-256 are recorded in results/ripgrep-environment.json.

Ripgrep and fresh GNU grep were interleaved sequentially with seed 20261028, one warmup and seven measured repeats per cell. This gives a comparison made in the same session; the earlier GNU and PowerShell measurements are retained and can be affected by time drift. Ripgrep uses --no-config, explicit case-sensitive text output, and --include-zero with -c to count matching lines rather than occurrences. Patterns and input files are identical. It keeps its default I/O strategy; GNU grep runs through the Git for Windows MSYS runtime, while ripgrep is the native MSVC build.


## Method and interpretation

Each invocation is a fresh process, with startup, parsing/JIT, input reading, computation, stdout pipe transfer and a common C# SHA-256 sink included. Console rendering and network download are excluded. One warmup per cell precedes seven measured rounds. Each round shuffles the 72 cells using seed 20261026; runs are sequential. The OS file cache is warm and was not flushed. This measures end-to-end command usage, not an already running interactive PowerShell session or cold-disk throughput.

PowerShell pipeline uses Get-Content and Where-Object/ForEach-Object. Reader uses a PowerShell while loop with .NET StreamReader and dispatches a switch per line; neither is a compiled C# implementation. These are representative scripts, not a claim about the fastest achievable PowerShell. Both use buffered UTF-8/LF stdout writers. Select-String reads the file directly, using -SimpleMatch where applicable; its match objects are written as lines or counted with Measure-Object. Its extension ran after the main matrix, with its own warmups and shuffle seed 20261027, so cross-suite ratios can be affected by time drift. GNU grep handles literal/regex/nonblank/count; sed handles replacement; awk handles fields/words/lines. Case-sensitive ASCII semantics are explicit. Regex is war|peace; literal is Prince; replacement is Prince → Count; fields split on spaces/tabs.

Raw per-run measurements include CPU time. summary.csv/json retain medians, observed min/max and quartiles (order-statistic bounds, not confidence intervals). Seven samples are enough to show large differences but weak evidence for small rankings. Other processes, scheduler activity, antivirus and power management were not controlled. Ratios describe these implementations and this Windows host, not all possible implementations or Linux performance. Output hashing has a shared cost and can matter for output-heavy workloads.

## Verification and coverage

The independent C# oracle verifies every timed output. Validation additionally includes empty input, blank/tab lines, repeated matches, case distinctions, a missing final newline, a 131,072-character line and three randomized 100-line fixtures with seed 20261026. The missing-newline sed replacement difference is detected explicitly and excluded from equivalence counts. Missing input and an unknown oracle operation must fail. Three real PowerShell mutations (wrong literal case, case-insensitive regex, wrong replacement) must all be killed by the output checks. Randomized fixtures are bounded fuzzing; no failure required shrinking/minimization. This is targeted mutation testing, not an exhaustive mutation score.

Coverlet validation-run C# coverage: 73.33% lines (77/105), 76.42% branches (107/140). The full benchmark was deliberately uninstrumented to avoid timing distortion. The matrix-generation/timing path is therefore not counted as test coverage. PowerShell scripts and GNU binaries have no instrumented line/branch measurements; cross-implementation checks cover behavior, not those coverage metrics.

Select-String adds 18 independent fixture checks including three seeded fuzz fixtures. PowerShell script code coverage is unmeasured; the checks verify observable output.

The ripgrep extension adds 64 independent equivalence checks (eight fixtures × four tasks × two tools), including three seeded fuzz fixtures, plus four negative checks and three killed argument mutants. The mutants change the literal, enable case-insensitive regex matching, and count occurrences instead of matching lines. Native GNU and ripgrep binaries have no instrumented code coverage. Fixture runs are instrumented separately from timed runs.

The small-file extension adds 60 fixture checks across all five engines: empty input, case/repeated matches, blank/no-match input and one 100-line randomized fixture with seed 20261029. It reuses the native negative/mutation checks and independent oracle. Small-suite generation and timing are excluded from test-only coverage; every timed small output is still verified.

The rawk extension adds 72 independent equivalence checks, seven negative checks (missing files, malformed programs and an invalid harness task), and nine deliberately incorrect programs detected across the three interpreters. Fixtures cover empty input, whitespace, tabs, missing final newline, a 131,072-character field and three 100-line bounded fuzz inputs with seed 20261030. These are targeted mutations and bounded fuzzing, not exhaustive conformance testing. Native rawk, awk-rs and GNU awk coverage is unmeasured. The fixture-only C# coverage omits metadata, matrix execution and timeout handling; timed output equivalence is checked separately without instrumentation.

Supplemental C# fixture coverage: 73.80% lines (31/42), 59.09% branches (26/44).

Ripgrep extension C# fixture coverage: 72.72% lines (88/121), 75.00% branches (99/132).

Rawk comparison C# fixture coverage: 65.47% lines (55/84), 60.97% branches (50/82).

mawk/frawk C# fixture coverage: 44.60% lines (62/139), 44.44% branches (80/180).

Buffering C# fixture coverage: 41.53% lines (54/130), 40.62% branches (65/160).

Grep build comparison C# fixture coverage: 71.73% lines (33/46), 79.76% branches (67/84).

Profiler control-test C# coverage: 79.20% lines (80/101), 56.97% branches (49/86).

Report-generator execution coverage (not a test suite): 100.00% lines (140/140), 95.78% branches (159/166).

Stable requirements BENCH-001 through BENCH-004 are maintained in requirements.sdoc; comments map requirements to implementation and evidence. StrictDoc HTML export is in results/requirements.

Before generating the report, run the small-file extension with `dotnet run --project RipgrepBench.csproj -c Release -- --small`. For fixture-only verification, add `--test`. BENCH-005 and BENCH-006 trace the native-tool and small-file extensions.

For the rawk extension (BENCH-007), build the retained source trees at the commits recorded in results/rawk-environment.json. Commands: `cargo build --release --locked --manifest-path tools/stefanalfbo-rawk/Cargo.toml -p rawk-cli` and `cargo build --release --locked --manifest-path tools/quinnjr-rawk/Cargo.toml`. The local Rust toolchain uses RUSTUP_HOME=tools/rustup and CARGO_HOME=tools/cargo; add the absolute tools/cargo/bin directory to PATH for that shell. Then run `dotnet run --project RawkBench.csproj -c Release` before generating the report. Add `-- --test` to run only fixture verification. The source repositories and local compiler remain available for reproduction.

## Reproduce

```powershell
winget install --id BurntSushi.ripgrep.MSVC --exact
Invoke-WebRequest https://www.gutenberg.org/cache/epub/2600/pg2600.txt -OutFile source.txt
dotnet build Bench.csproj -c Release
dotnet bin/Release/net10.0/Bench.dll --test
dotnet bin/Release/net10.0/Bench.dll
dotnet run --project Supplement.csproj -c Release
dotnet run --project RipgrepBench.csproj -c Release
dotnet run --project RipgrepBench.csproj -c Release -- --small
dotnet run --project Analyze.csproj -c Release
```

GNU executables are expected in C:\Program Files\Git\usr\bin. Set BENCH_PWSH to override PowerShell and BENCH_RG to override ripgrep. By default the ripgrep runner resolves the installed winget package, not Codex's bundled rg.exe. Ripgrep-only validation: dotnet run --project RipgrepBench.csproj -c Release -- --test. Restore .NET 10 SDK and the recorded native tool versions to reproduce the environment. The retained source hash allows detecting future Gutenberg changes. Raw JSONL includes warmups with Round=-1; exclude them from statistics.


## GNU grep reader-thread experiment

The [C++20 reader-thread experiment](reader-thread/README.rst) retains GNU grep's matcher and compares the original executable, an adapter-disabled control, and three bounded queue sizes on the same 32 MiB corpus. All 425 timed outputs passed validation. Default-queue medians improved modestly against original grep, but broad variation and mixed results against the disabled control do not justify enabling it by default. Source, StrictDoc requirements, reproduction scripts, raw timings, coverage and fuzz/mutation evidence are in `reader-thread/`.

The later [AVX2 and queue profiling experiment](reader-thread/OPTIMIZATIONS.rst) records the implemented vector searches, reduced queue overhead, current timing comparisons, and producer/consumer profiling. Keep its measurements separate from the first session.


The [256-byte block experiment](reader-thread/BLOCK256.rst) compares eight-vector search blocks with the preserved 32-byte build, including in-memory kernel timings and code size.

