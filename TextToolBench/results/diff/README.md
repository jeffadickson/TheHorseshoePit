# Diff laboratory

This is a separate Windows experiment, not an extra ranking mixed into the historical grep/awk/sed sessions. Open [the offline report](report.html). Raw observations, qualification exclusions and exact tool/input identities are in [runs.jsonl](runs.jsonl), [environment.json](environment.json), [validation.json](validation.json) and [summary.json](summary.json). Verification results are collected in `verification.json` after the campaign.

## Design and interpretation

Thirteen command variants: installed GNU diffutils 3.12 default, `--minimal`, `--speed-large-files`; locally compiled GNU generic/native; uutils diffutils 0.6.0 official Windows release and local generic/native builds; BusyBox diff; Git 2.56.0 Myers, minimal, patience and histogram. Every command generates unified patches with three context lines. GNU/Git/BusyBox force text comparison; uutils operates on raw byte lines. Git user/system configuration, indentation heuristics, external diff, text conversion, color and rename detection are disabled. LC_ALL=C. Exact argument vectors are retained in the environment record and harness source.

There are 30 fixtures: seven book workloads at nominal 1 KiB, 64 KiB and 1 MiB; identical/scattered/long-line workloads at 8 and 32 MiB; three source-code workloads at 64 KiB. Sizes are lower bounds and actual byte lengths are recorded. Workloads cover identity, scattered edits, ten-percent insertion/deletion blocks, moving the first quarter to the end, seven repeating lines with an insertion/deletion, completely different content, and a one-byte change in a single long line. Stable unique prefixes distinguish ordinary lines; the repeated fixture deliberately removes them. The code fixtures cover scattered edits, moved blocks and insert/delete blocks; `source-code-fixture.cs` retains the exact source snapshot used and is reused on subsequent runs even if the reporting code changes. Delete it intentionally only when starting a new input baseline. The existing public-domain War and Peace derivative supplies the book text; these are synthetic workloads, not a representative sample of all books or code repositories.

Qualification applies each output using GNU patch 2.8.1 with binary I/O and zero fuzz, then compares the reconstructed target byte for byte. The same check runs after **every** warmup and measured output. Empty/identical pairs must return exit 0 and no patch; changed pairs must return exit 1 and a usable patch. Different valid patches are allowed. SHA-256 identifies each output but is not an equivalence gate between algorithms. GNU patch is an independent implementation, not a formal proof of universal correctness.

One warmup and seven measured rounds are shuffled sequentially with seed **20261027**. No diff processes run concurrently within the harness. Timing includes fresh process startup, complete raw stdout capture and waiting for exit. Input preparation, hashing, process-memory queries and patch application are outside the stopwatch. Filesystem cache is warm. CPU time is the diff process's total CPU time; peak memory is Windows' native `PeakWorkingSetSize` for that process, read through its retained process handle. It excludes children and is not managed allocations or peak private committed memory. `system-utilization.jsonl` reports whole-machine CPU utilization per round, including verification and unrelated activity; it does not establish exclusive use of the machine.

Tables report median, observed min/max and Tukey lower/upper quartiles (second and sixth of seven ordered samples), median peak working set, patch bytes, added/deleted lines and hunks. **Git memory is unavailable:** the command path is its Windows launcher; wall time includes the child, but raw CPU/memory counters cover the launcher only. Those raw counters remain unchanged for provenance; Git summary memory is null and the HTML shows N/A. Header formats affect byte counts. Changed-line count is edit size, not human readability; moved blocks need not be recognized as moves. Only bounded, LF-terminated randomized cases independently check minimal variants against dynamic-programming edit distance. Do not infer that every algorithm or large fixture produces a globally minimal edit script. Seven measurements do not establish confidence intervals or worldwide winners.

## Observed results

The completed campaign contains **372 qualified cells, 2,604 measured runs and 372 warmups**. All retained outputs reconstructed their targets exactly. Eighteen uutils cells were excluded by the predeclared table-memory policy; no timed values were invented for them.

| Nominal 32 MiB fixture | Lowest observed median | Time |
|---|---|---:|
| Identical files | Git minimal | 96.93 ms |
| One-byte change in one long line | Official uutils 0.6.0 | 249.21 ms |
| Scattered edits | Installed GNU diff 3.12 | 372.04 ms |

These are workload-specific observations, not a worldwide ranking. At 32 MiB the local native GNU medians were about 1.5%, 1.8% and 4.9% lower than its same-source generic control for identical, long-line and scattered fixtures respectively; observed ranges overlap. Processor tuning was not a uniform Rust improvement: native/generic long-line medians were 282.28/277.24 ms, with widely overlapping ranges. Whole-system utilization during measured rounds ranged from about **21.7% to 38.8%**; this was a shared desktop, not an exclusive benchmark host. Consult the complete ranges, raw observations and resource exclusions before treating small differences as meaningful.

The original campaign exited nonzero **after all eight rounds** because report generation attempted to open a log whose writer was still held. `benchmark.log` retains that failure. Raw data was complete and preserved; reporting was recovered with the separate `--report` command. The final helper closes the producer's writer before invoking the reader, with a direct regression test. `campaign-source/` and `campaign-assembly.json` retain the measured helper identity. Subsequent changes fix the writer lifecycle, clarify the conservative-estimate XML comment, reuse the saved code fixture, improve responsive reporting and mark Git child-process counters unavailable; they do not replace the recorded timings or subprocess outputs.

## Controlled builds and resource exclusions

GNU uses GCC 16.2.0, the same source, the same feature configuration and a clean full-object rebuild for each flag set:

- Generic: `-O2 -march=nocona -msahf -mtune=generic -pipe`.
- Native: `-O3 -march=native -mtune=native -ftree-vectorize -pipe`, plus a vectorization-report output flag.

The GNU 3.12 bundled gnulib header suppressed newlib's required `__need_wint_t` request with this GCC/C23/runtime combination. The first build failed with unknown `wint_t`. Both comparison builds use [the identical small header compatibility patch](../../scripts/diff-stddef-wint.patch), with no diff-algorithm change. `gnu-generic.build.log` retains the failure; `gnu-*.rebuild.log` retains actual compiler commands. Both `gnu-*.config.log` files intentionally describe the shared generic feature configuration; native optimization flags are passed to the clean rebuild, not a second configuration. The preserved generic executable is under `gnu-generic/bin` so rebuilding native objects cannot replace it. Native vectorization diagnostics are in `gnu-native.vectorization.log`; generated vector instructions alone do not prove a speedup.

Rust uses the same locked 0.6.0 source archive and Rust 1.99.1 / LLVM 23.1.1 for the local pair. Both inherit release thin LTO, one codegen unit and abort-on-panic from Cargo.toml. The only additional native flag is `-C target-cpu=native`; generic has empty RUSTFLAGS. Publisher binaries have separately captured hashes and are not assumed to use the same compiler as local builds.

The pinned uutils unified-diff implementation calls `diff::slice` from **diff 0.1.13**. After trimming common prefix/suffix lines, that dependency allocates a full `u32` LCS table:

```rust
let left_diff_size = left.len() - leading_equals - trailing_equals;
let right_diff_size = right.len() - leading_equals - trailing_equals;
let mut table = Vec2::new(0, [left_diff_size + 1, right_diff_size + 1]);
for (i, l) in left_skip.iter().enumerate() {
    for (j, r) in right_skip.iter().enumerate() {
        // Fill the longest-common-subsequence table.
    }
}
```

Its table alone needs `4 × (remaining left lines + 1) × (remaining right lines + 1)` bytes. The harness excludes uutils cells whose predicted table exceeds **256 MiB**, before launching them. The estimate is exact for the benchmark's LF-terminated pairs; mixed final-newline states can conservatively overestimate because the reference splitter retains a terminal empty element before trimming, while uutils removes it first. This is a documented resource policy, not a timed failure or a claim that the implementation cannot finish with more resources. Other allocations are additional, so this is not a total-memory cap. The matrix prediction is independently unit-tested. A separate 20-second wall timeout protects every process. Every exclusion is retained with a reason; there is no invented timing for skipped work.

BusyBox's exploratory default binary-file notice was expected behavior, not a defect; that observation remains in `busybox-default-binary.json`. The final suite uses its supported `-a` option consistently with GNU/Git text-mode patch generation. Any remaining edge failures are retained in final validation rather than normalized away.

## Reproduce on Windows

Requires .NET 10, Git for Windows, the existing corpus inputs, and the shared MSYS2 GCC/make/patch and local Rust toolchains from the existing bootstrap. Network acquisition is hash-pinned. C++ build tools are required for the MSVC Rust target. Nothing is installed as a new default.

```powershell
$project=(Get-Location).Path
foreach($path in @($project,(Join-Path $project 'scripts'),(Join-Path $project 'results'))) {
    if(!(Test-Path -LiteralPath $path -PathType Container)){throw 'Run from the TextToolBench checkout root'}
}
& (Join-Path $project 'scripts/Run-DiffBench.ps1') -Mode Build
& (Join-Path $project 'scripts/Run-DiffBench.ps1') -Mode Test
& (Join-Path $project 'scripts/Run-DiffBench.ps1') -Mode Coverage
& (Join-Path $project 'scripts/Run-DiffBench.ps1') -Mode Mutation
# Run timing separately, after builds/instrumentation finish. Archive prior results before a rerun.
& (Join-Path $project 'scripts/Run-DiffBench.ps1') -Mode Benchmark
```

Make targets: `diff-build`, `diff-test`, `diff-coverage`, `diff-mutation`, `diff`, `diff-report`. `diff-build` uses the existing shared dependency/source bootstrap; clean-machine `make all` is not newly claimed verified. Build helpers are incremental and sources/archives/patches/locks are retained. The C# harness remains managed for instrumentation; Native AOT was not measured or claimed. Linux execution is unverified: process memory instrumentation here uses Windows APIs.

For optional non-rendering HTML checks, use the existing jsdom development setup described in SETUP.md, then run `node --test scripts/Test-DiffReport.cjs`. These tests do not render CSS or establish visual layout correctness. Browser local-file policy previously blocked preview; no alternate browser surface or server workaround was used. Rendered layout, mobile and print appearance remain unverified.

## Verification scope

`model-tests-final.log`, `unit-tests/model.trx` and the associated Cobertura file retain NUnit/FsCheck evidence. Thirty NUnit tests pass, including writer-lifecycle recovery, null Git memory, statistics, malformed JSON and incomplete/duplicate-round rejection. FsCheck runs 1,000 bounded cases with replay seed `(20261027,17)` against an independent longest-common-subsequence reference, with shrinking enabled. The seeded CLI campaign is a bounded custom generator, **not coverage-guided fuzzing**; base64 counterexamples remain in validation. Patch corruption mutations require a conforming original control. Stryker.NET source mutations apply only to DiffModel, not the third-party tools: score **95.69%**, 103 killed, 8 timed out, 5 survived, 47 compile errors and 404 ignored. The score includes timeouts and is not a proof; its baseline was the 20 model tests before the report tests were added. Source hashes and final line/branch/mutation results are in `verification.json`; coverage is not proof of correctness. Generated public XML API documentation is retained in [api/DiffBench.xml](api/DiffBench.xml).

Upstream uutils nextest initially ran 12/283 tests: 8 passed, 4 failed because permutation tests tried to create children of an absent `target` directory when using a separate Cargo target directory, then fail-fast left 271 unrun. The bounded retry deliberately excluded permutation tests: **251 passed, 32 skipped** (`rust-upstream-bounded.log`). It is not a full upstream pass, and those skipped permutation campaigns were not verified. GNU's full upstream check suite and native third-party code coverage were not run; every qualified CLI result is instead checked by independent patch reconstruction. The PowerShell acquisition/build wrappers and shell build recipe have no line/branch coverage measurement.

Primary references: [GNU performance tradeoffs](https://www.gnu.org/software/diffutils/manual/html_node/diff-Performance.html), [Git algorithms](https://git-scm.com/docs/diff-algorithm-option), [uutils source](https://github.com/uutils/diffutils), [diff 0.1.13 source](https://docs.rs/diff/0.1.13/src/diff/lib.rs.html), [FsCheck](https://fscheck.github.io/FsCheck/QuickStart.html). Requirement **BENCH-012** traces the implementation and retained evidence.

## Library choices

The subsequent methods-documentation update expands the in-page HTML contract without rerunning timing or altering the retained observations. Its current report/model unit-test coverage and HTML/content/link checks are recorded separately in [methods-documentation-verification.json](../methods-documentation-verification.json). The earlier CLI/report coverage and Stryker results above remain historical verification of the benchmark implementation; they are not fresh coverage claims for later documentation source changes. [methods-data-integrity.json](../methods-data-integrity.json) confirms unchanged raw runs, summaries and environment metadata.

Final measured coverage is **100% lines / 100% branches for DiffModel** in the NUnit/FsCheck run, and **69.89% lines / 55.33% branches for the complete helper module** under CLI conformance plus report-mode instrumentation. These are different scopes, not interchangeable scores. The actual timing campaign was uninstrumented. The final unit coverage also exercises the producer/reader regression and Git's null memory values; the CLI-only coverage does not exercise the full timing loop. Exact scopes, hashes and artifact paths are in `verification.json`. `scripts/Collect-DiffEvidence.ps1` collects this retained session's evidence without rerunning timings.

The production helper uses .NET's Process, System.Text.Json, SHA-256 and file APIs instead of custom process, serialization or hashing implementations. A small Win32 interop call is necessary because the managed Process.PeakWorkingSet64 property rejects an already-exited process; the retained native handle can still supply the OS peak counter. GNU patch supplies the independent patch application, avoiding a home-grown patch parser. NUnit, FsCheck and Coverlet reuse the established .NET test stack; FsCheck provides seeded generation and shrinking. The optional jsdom check reuses the existing development dependency and never enters the benchmark runtime. The small custom insertion/deletion dynamic-programming routine is deliberately an independent, bounded test oracle for minimality, not another production diff implementation; FsCheck challenges it against a separately implemented longest-common-subsequence identity. No coverage-guided fuzzing or universal correctness proof is claimed.
