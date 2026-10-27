GNU grep C++20 reader-thread experiment
=======================================

This experiment retains GNU grep 3.0's matcher and line buffering and adds a
C++20 producer feeding a bounded queue. ``grep.patch`` contains the integration;
``read-ahead.cpp`` contains the queue. This is experimental GPL-3.0-or-later
code, consistent with the upstream source license in ``source/COPYING``.

Source and approach
-------------------

The official archive was fetched from
https://ftp.gnu.org/gnu/grep/grep-3.0.tar.xz and matched the existing benchmark
archive: SHA-256
``E2C81DB5056E3E8C5995F0BB5D0D0E1CAD1F6F45C3B2FC77B6E81435AED48AB5``.
Version 3.0 is intentionally the previous benchmark's pinned source, not a claim
about the newest GNU release. No account signup is needed to download it.

The standard C++20 library provides ``std::jthread``, mutexes and condition
variables; an external queue dependency adds little for one producer and one
consumer. The C++ specification describes jthread's stop and join lifecycle:
https://eel.is/c++draft/thread.jthread.class . POSIX ``pread`` lets the producer
read using private offsets; the main thread commits only delivered bytes to
the public file offset before seeks and finalization. This avoids races over
the shared descriptor position. Input must be a named, seekable regular file;
stdin and unsupported inputs retain GNU's original ``safe_read`` path.

The producer fills reusable buffers and the consumer copies bytes into grep's
existing aligned buffer, preserving its sentinels and partial-line handling.
That copy, thread creation, synchronization and shutdown are included in timing.
A sparse-file seek stops and recreates the queue. Stop waits for the outstanding
regular-file read; it cannot cancel an operating-system read that is blocked.
Concurrent modifications to input files are outside the experiment's contract.

``GREP_READ_AHEAD=1`` enables the queue. Defaults are
``GREP_READ_CHUNK=262144`` and ``GREP_READ_SLOTS=3``. Invalid tuning disables
the queue; the permitted maximum reservation is 16 times 8 MiB. No affinity
or explicit CPU-cache prefetch instructions are used.

Measurement
-----------

The exact original file is ``data/32MiB.txt`` in the prior benchmark workspace:
33,554,436 bytes. The harness records its SHA-256. Five tasks cover literal
selected lines, regex selected lines, matching-line counts, nonblank lines and
an absent literal count. Original grep, the modified executable with the
adapter disabled, and queues of 64 KiB, 256 KiB and 1 MiB per slot run in one
shuffled, sequential session. All queues have three slots. There are two
warmups and fifteen measured repetitions per cell, seed 20261026. Each run
starts a new process and hashes stdout. All output lengths, hashes and exit
codes must match the independent ASCII oracle before a timing is accepted.

These are warm-cache, process-startup-inclusive Windows/MSYS2 measurements.
The 32 MiB file fits in RAM. No cache eviction or cold-storage test is performed.
Other development tasks are active on the machine. CPU time is recorded but
has coarse Windows accounting resolution. This experiment measures the complete
read-ahead design, including its larger read batches and extra copy; it does
not isolate thread overlap from read-size changes or demonstrate CPU-cache
residency. No conclusion about large uncached files, Linux grep performance,
newer GNU versions or production deployment follows from these results.

Verification and reproduction
-----------------------------

``requirements.sdoc`` retains stable IDs and references to implementation and
evidence. ``native-check.cpp`` checks bytes, consumed offsets, seeks, queue
wraparound, cancellation, EOF, read errors and configuration fallback against
the original input bytes. ``native-verify.sh`` runs native Linux ASan/UBSan,
gcov source coverage, 5,000 libFuzzer cases with seed 20261026, and five bounded
manual source-fault checks. These C++ fault checks are not a comprehensive
automated mutation campaign. Stryker.NET covers the C# oracle and quantile
logic only. Full harness coverage is measured separately with Coverlet.

The existing benchmark must already have its pinned MSYS2 toolchain and original
``tools/grep-build/native-o3`` build. Preserve that workspace; this extension
does not install or replace system grep. ``build.sh`` incrementally compiles
the helper and uses four make workers. Windows and WSL outputs stay separate.
``native-verify.sh`` places Linux outputs in ``$HOME/texttool-reader-verification``.
See ``Run-Experiment.ps1`` for checked-directory Windows entry points and the
final recorded results and limitations below.

Measured result
---------------

The default queue produced modestly lower medians than original grep, but it
did not produce a uniform improvement over the adapter-disabled control.
Variation was substantial and the quartile ranges overlap. Keep the adapter
opt-in; these data do not justify a default change or a general speed claim.

.. list-table:: Median elapsed milliseconds, 15 measured runs per cell
   :header-rows: 1

   * - Task
     - Original
     - Adapter disabled
     - Queue 64 KiB
     - Queue 256 KiB (default)
     - Queue 1 MiB
   * - Literal lines
     - 242.980
     - 235.496
     - 207.800
     - 234.448
     - 242.638
   * - Regex lines
     - 270.312
     - 285.964
     - 275.242
     - 260.161
     - 273.079
   * - Matching-line count
     - 247.031
     - 216.207
     - 214.745
     - 232.686
     - 264.670
   * - Nonblank lines
     - 530.878
     - 506.993
     - 499.008
     - 484.734
     - 500.230
   * - Absent literal count
     - 189.829
     - 195.585
     - 168.972
     - 177.836
     - 201.220

Default-queue medians were 3.5--8.7 percent lower than original grep. Against
the adapter-disabled control, literal output was essentially unchanged,
matching-line count was 7.6 percent slower, and the remaining medians were
4.4--9.1 percent lower. The 64 KiB queue lowered several medians but increased
the regex median by 1.8 percent relative to original grep. The 1 MiB queue
offered no consistent benefit.

For example, original literal-search quartiles were 211.233--299.765 ms and
the default queue's were 188.491--261.081 ms. Original matching-line count
quartiles were 177.607--307.395 ms, versus 196.752--276.267 ms for the default
queue. These overlapping ranges and the control differences prevent a strong
attribution of small median differences to useful I/O overlap.

Default-queue median CPU times increased for absent count (109.375 to
125.000 ms), matching-line count (156.250 to 171.875 ms), and nonblank output
(234.375 to 265.625 ms). Literal and regex median CPU times were unchanged at
the recorded resolution. Extra CPU work accompanies some of the wall-time
improvements. ``results/summary.csv`` includes wall-time quartiles and CPU medians;
``results/runs.jsonl`` contains 425 validated runs, including 50 warmups and
375 measured runs. There were zero timed output or exit-status mismatches.

A future experiment could isolate batching with a synchronous reader using
the same buffer sizes, then test files exceeding available RAM on an otherwise
idle machine. Those experiments have not been run here.

Verification outcome and limits
-------------------------------

* Windows integration: 150 independent oracle comparisons, 193 differential
  comparisons and 30 negative checks passed. The 425 timing outputs also passed.
* Native adapter: 144 deterministic byte/offset scenarios plus negative API
  cases passed under ASan/UBSan. libFuzzer completed 5,000 inputs, seed 20261026,
  with no detected failure. The final corpus is retained in
  ``results/fuzz-corpus.tar.gz``. No counterexample was found to minimize.
* Five manual production-source faults were all detected by the unchanged
  native checks. Each exited 134 (assertion abort), with no timeouts. These are
  bounded fault-injection checks, not a comprehensive C++ mutation score.
* Adapter-only gcov: 96/97 lines (98.97 percent), and 80/118 branch outcomes
  taken at least once (67.80 percent). The separate "branches executed" number
  is 98.31 percent and must not be substituted for branch-outcome coverage.
  The allocation/thread-creation exception handler was not exercised. Native
  Linux coverage is not Windows coverage; the GNU C integration and matcher
  were not instrumented. Clang's static analyzer emitted no diagnostics.
* C# unit tests: 18 passed, zero failed or skipped. Stryker.NET 5.0.0 targeted
  the oracle and quantile code: 31 killed, 3 survived, zero timeouts, 7 compiler
  errors, 245 ignored, score 91.18 percent over 34 testable targeted mutants.
  Two survivors remove empty-line handling that does not affect the fixed
  benchmark patterns; one changes exception-message text. All remain recorded
  as survivors; no formal equivalence claim is made.
* Coverlet integration coverage for handwritten ``Experiment.cs`` was 118/158
  source lines (74.68 percent) and 56/74 recorded source-line branch outcomes
  (75.68 percent). The timing loop was exercised in the uninstrumented run,
  rather than the coverage run. Including generated JSON code, the console
  report's whole-module figures are 22.01 percent line and 41.39 percent branch.
  Unit-only collector coverage was 15/158 handwritten lines (9.49 percent),
  20/76 branch outcomes (26.32 percent); including generated code, 0.97 percent
  line and 10.63 percent branch. Collector and console branch mappings differ;
  these scopes are reported separately and were not merged. PowerShell
  orchestration and the shell build scripts were not coverage-instrumented.
* Real StrictDoc export passed. Sphinx 9.2.0 built these engineering notes with
  warnings treated as errors. The timing harness was successfully compiled
  with .NET 10 Native AOT; coverage used a separate managed build.

Reproduction entry points
-------------------------

From this extension directory, use the existing benchmark workspace as the
``BenchmarkRoot`` argument. Check it before use, for example in PowerShell::

    $benchmark = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex/2026-10-26/benchmark-sed-and-grep-and-awk'
    if (!(Test-Path -LiteralPath $benchmark -PathType Container)) { throw 'Set the existing benchmark path' }
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Build
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Benchmark
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode NativeVerify
    .\Verify-Harness.ps1 -BenchmarkRoot $benchmark -Mutation
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Requirements

The path example identifies this chat's existing benchmark workspace; adjust the
variable for another checkout. The scripts check their directories before use.
Build and verification outputs are local, and no remote CI was enabled or run.
For Sphinx, create a local Python virtual environment under ``build/docs-env``
after checking the parent directory, install the pinned ``Sphinx==9.2.0``, and
run ``python -m sphinx -W --keep-going -b html docs docs/_build/html`` using that
environment. Python is used only for the required documentation tools.
