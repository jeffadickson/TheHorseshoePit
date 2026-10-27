256-byte AVX2 search experiment
===============================

This variant handles 256 input bytes per loop using eight 32-byte AVX2 vectors.
It combines comparison masks to reject an entire block quickly. When a block
contains a match, lane masks are checked in address order to preserve the first
matching byte. Remaining input uses the existing 32-byte loop and scalar tail.
The compile-time GREP_VECTOR_BLOCK setting selects 32 or 256, avoiding an extra
runtime block-size branch.

The 32-byte executable from a01ca55 was preserved. The 256-byte executable links
the same driver, matcher and queue objects with the new vector object. Both use
-O3 -march=native -mtune=native -ftree-vectorize. The queue and vector activation
settings are identical within each pair. These are native processor-specific
builds, not portable-dispatch binaries.

Code size
---------

The vector objects contain both one-byte and two-byte search helpers:

* 32-byte variant: 1,536 bytes of text sections.
* 256-byte variant: 3,040 bytes of text sections.
* Increase: 1,504 bytes, approximately 1.5 KiB.

The complete executable grew from 425,581 to 427,117 bytes. Thus 256 bytes is
the input block size, not a 256-byte CPU register or a large allocation.
Raw section measurements are retained in results/block256/sections.txt and
code-size.json.

In-memory kernel timings
------------------------

Both variants run in one process, in seeded shuffled order. File loading and
independent reference-count construction occur before timing. Each observation
averages sixteen passes over the same 33,554,436-byte corpus. Each cell has two
warmups and fifteen measured observations. All 102 observations passed their
expected absent-byte or newline-count checks.

.. list-table:: Median milliseconds per full corpus pass
   :header-rows: 1

   * - Operation
     - 32-byte blocks
     - 256-byte blocks
   * - One absent byte
     - 2.856
     - 2.879
   * - Either of two absent bytes
     - 3.445
     - 2.937
   * - Find every newline
     - 7.757
     - 7.992

The two-byte absent search improved by 14.7 percent. The single-byte absent
search was essentially unchanged; newline traversal was 3.0 percent slower.
Larger blocks help some rejection-heavy searches and do extra work when a match
is near the start of a block. Quartiles overlap and the host had other work
active; this is finite workload evidence, not a universal speed claim.

These single-digit millisecond scans exclude process startup, file loading,
grep's matching/output processing and stdout hashing. They explain why the
earlier whole-process numbers should not be interpreted as comparison latency.
Raw measurements are in kernel-runs.csv and summaries in kernel-summary.csv.
For the fifteen sorted measured values, the median is the eighth value and
linear-interpolated quartiles average values four/five and eleven/twelve.

Whole-program timings
---------------------

Four engines were interleaved: 32/256-byte searches, with/without the queue.
All used the same original corpus, two warmups and fifteen measured rounds.
All 340 outputs matched independent length, SHA-256 and exit-status references.
This is a new warm-cache shared-host session; compare variants within it.

.. list-table:: Median milliseconds, queue enabled
   :header-rows: 1

   * - Task
     - 32-byte blocks
     - 256-byte blocks
   * - Literal lines
     - 105.606
     - 102.958
   * - Matching-line count
     - 96.471
     - 102.695
   * - Regex lines
     - 125.792
     - 127.666
   * - Nonblank lines
     - 214.964
     - 213.357
   * - Absent literal count
     - 81.021
     - 76.339

The larger block lowered literal and absent-count medians by 2.5 and 5.8 percent,
but matching-line count was 6.5 percent slower and regex was 1.5 percent slower.
Nonblank output was essentially unchanged. Without the queue, literal/regex/
absent medians were modestly lower, count was 3.3 percent slower, and nonblank
output was unchanged. Raw quartiles and CPU medians are retained in summary.csv.

This does not justify replacing the 32-byte default. The 256-byte binary remains
a separate experimental executable. No explicit SIMD copy or NTFS input changes
were made in this experiment.

Verification and reproduction
-----------------------------

StrictDoc READAHEAD-010 traces this variant. Deterministic native verification
ran both block sizes under ASan/UBSan: 345,088 vector reference cases, 1,028
guard-page cases, 144 queue scenarios and negative API cases per variant.
The new tests cover 255/256/257 and 511/512/513-byte boundaries, every match lane,
second-block matches and competing first-match positions.

The 256-byte code also passed a seeded 5,000-input libFuzzer campaign, Memcheck
and Clang static analysis. Three deliberate source faults (block bound, stride
and lane offset) were all detected. These bounded manual faults are not a
comprehensive C++ mutation score. Invalid block-size compilation was rejected
with the expected diagnostic. Initial/final fuzz corpora and source hashes are
retained in results/block256/native/.

Gcov reports 100 percent line coverage and 69/76 branch outcomes (90.79 percent)
for the 256-byte vector module. Reported line counts include template
instantiations. Non-AVX2 builds and all activation/failure combinations were not
fully exercised. The timing harness, kernel harness and shell/PowerShell
orchestration have separate coverage limitations; no hardware-counter or
instruction-sampling profile was collected for this variant.

All 28 C# unit tests passed. The targeted Stryker.NET campaign killed 83 of
91 testable mutants (91.21 percent), with eight survivors, eleven compiler
errors and 385 ignored mutants; there were no timeouts. The surviving scopes
and message/empty-line limitations remain as documented in OPTIMIZATIONS.rst.
Whole-module Coverlet reports, including generated JSON code, recorded 2.37
percent line / 17.93 percent branch coverage for unit tests and 19.98 percent
line / 29.86 percent branch coverage for the integration run. These are separate
unmerged scopes. The C++ kernel timing harness was output-validated but not
coverage-instrumented. Current XML, native gcov records and tool logs are retained.
StrictDoc export and Sphinx with warnings treated as errors passed.

After the normal build, use the existing checked-directory runner::

    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Block256Build
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Block256Verify
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Block256Kernel
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Block256Benchmark
    .\Verify-Harness.ps1 -BenchmarkRoot $benchmark -Block256 -Mutation

The Windows baseline build and original corpus must already exist as described
in README.rst. Linux verification outputs stay under the native WSL filesystem.
No remote CI was enabled or run.
