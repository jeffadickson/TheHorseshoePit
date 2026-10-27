Vector searches and queue profiling
===================================

This experiment implements explicit AVX2 byte comparisons and reduces queue
overhead. These results belong to a new session: compare engines within this
session, not against the earlier timings in README.rst.

Implemented changes
-------------------

* vector-search.cpp compares 32 input bytes against one or two byte values,
  obtains a match mask, and returns its first set bit. Scalar tails handle
  fewer than 32 bytes; no vector load exceeds the supplied region.
  GREP_VECTOR_SEARCH=1 enables the helpers in the driver and kwset routines.
  Disabling it selects the original runtime/gnulib helpers. Regex algorithms
  and matching-line-count semantics remain unchanged.
* Queue buffers use C++20 overwrite-only allocation, avoiding an initial zero
  fill. The consumer exposes only initialized bytes published by the producer.
* The consumer releases the mutex during copying while reserving its slot.
  The producer cannot reuse that slot until copying finishes.
  Publication/release notifications occur outside the lock.
* Queue indices use increment-and-reset instead of variable modulus.

Both release and profiling builds use -O3 -march=native -mtune=native
-ftree-vectorize. GCC targets this i5-10505 as Skylake, enabling AVX2, loop and
SLP vectorization. Disassembly in results/optimized/vector-instructions.txt
confirms vpcmpeqb and vpmovmskb in the new searches. Whole binaries remain
processor-specific; a helper fallback does not make a native build portable.

The C++20 library and compiler intrinsics were sufficient; no external queue
or SIMD library was added. Approach references:
https://gcc.gnu.org/onlinedocs/gcc/x86-Built-in-Functions.html and
https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html .

Timing results
--------------

All engines used the original 33,554,436-byte ASCII/LF corpus. Each cell had two
warmups and fifteen measured repetitions, in seeded shuffled order. All 425
outputs (50 warmups, 375 measurements) matched independent length, SHA-256
and exit-status checks. Times include startup and stdout hashing. These are
warm-cache shared-host results; other work was active.

.. list-table:: Median elapsed milliseconds
   :header-rows: 1

   * - Task
     - Original
     - Previous queue
     - Optimized queue
     - Vectors without queue
     - Vector queue
   * - Literal lines
     - 150.944
     - 151.148
     - 135.818
     - 150.329
     - 131.900
   * - Matching-line count
     - 144.569
     - 136.626
     - 130.732
     - 130.737
     - 124.239
   * - Regex lines
     - 170.832
     - 168.599
     - 168.695
     - 176.329
     - 184.037
   * - Nonblank lines
     - 274.513
     - 291.514
     - 310.575
     - 262.704
     - 301.726
   * - Absent literal count
     - 108.845
     - 102.976
     - 112.631
     - 116.945
     - 99.873

The combined vector queue lowered medians versus original grep for literal
lines (12.6 percent), matching-line count (14.1 percent), and absent-literal
count (8.2 percent). Regex and nonblank output were slower by 7.7 and 9.9 percent.
Against the optimized queue with library searches, vectors lowered literal
search by 2.9 percent and count by 5.0 percent, but increased regex by 9.1 percent.
Vectors without the queue lowered count by 9.6 percent; literal output was
essentially unchanged.

Quartiles overlap broadly. Original count quartiles were 129.453--154.574 ms;
the vector queue's were 117.169--141.398 ms. Original literal quartiles were
138.919--173.639 ms; the vector queue's were 123.761--189.235 ms. This is a
mixed workload result, not a universal SIMD speedup. Keep both features opt-in.
All quartiles, CPU medians, raw runs and executable hashes are retained in
results/optimized/.

Queue profiling
---------------

profile.exe additionally compiles QUEUE_PROFILE, symbols and frame pointers.
Timing builds contain none of the profiling counters or clocks. The profiler
records producer read/full-queue-wait/lock times, consumer empty-queue-wait,
lock/copy times, shutdown time, calls, bytes, high-water mark and thread CPU
clocks when supported.

These are instrumented wall intervals and thread CPU clocks, not instruction
samples or hardware counters. Thread wall intervals overlap and are not
additive. Instrumentation perturbs execution. Windows CPU readings in this
run were quantized in 15.625 ms steps; zero can represent work below that
resolution. Unsupported clocks are JSON null, retained as unavailable.

Thirty profiles completed: five tasks, vector searches enabled/disabled, and
three repetitions. Every output matched and all byte/queue invariants passed.
One file can create several readers: grep's binary/sparse checks seek and
recreate the queue. ProfileReport.cs sums additive counters across those
lifetimes, takes the high-water maximum, and preserves missing clocks.

For matching-line count with vectors, median diagnostic measurements were:

* Four reader lifetimes and 257 consumer copies.
* 33,554,436 bytes delivered; 34,013,188 bytes read, including discarded prefetch.
* Producer reads: 14.128 ms; producer full-queue wait: 42.913 ms.
* Consumer empty-queue wait: 0.398 ms; copies: 3.547 ms.
* Producer/consumer lock intervals: 0.044/0.068 ms; high-water mark: three slots.

The producer often waited for space while the consumer rarely waited for data.
This points toward consumer processing and reader lifecycle overhead as next
investigation targets in these warm-cache runs. It does not establish behavior
for larger uncached files. Copying was measurable, but this experiment did not
implement explicit SIMD bulk copying or zero-copy ownership.

Raw profiles are in results/profile/observations.jsonl and per-metric medians
in results/profile/summary.csv. The profiler does not yet eliminate reader
restarts or discarded prefetch.

Verification and reproduction
-----------------------------

StrictDoc IDs READAHEAD-006 through READAHEAD-009 trace vector behavior, slot
ownership, profiling and comparisons. Earlier evidence in results/ belongs to
the first experiment. Current native evidence is in results/optimized/native/.

Native checks cover 144 queue scenarios and negative API cases, 245,760 vector
reference cases and 260 guard-page cases with vectors both enabled and disabled.
ASan/UBSan, Memcheck and a 5,000-input libFuzzer campaign supplement them. Eight
bounded source faults challenge copy, offset, queue, first-match, second-byte
and tail behavior. All were detected. They are manual fault-injection checks,
not a comprehensive C++ mutation score. Initial/final corpora are retained.
There were no detected sanitizer/fuzz failures or Memcheck errors.

Current gcov source coverage (Linux, not Windows) was:

.. list-table:: Native coverage
   :header-rows: 1

   * - Scope
     - Line coverage
     - Branch outcomes taken
   * - Queue adapter
     - 104/105 (99.05 percent)
     - 96/138 (69.57 percent)
   * - Vector searches
     - 73/73 (100 percent)
     - 28/30 (93.33 percent)
   * - Profiling header
     - 37/37 (100 percent)
     - 7/14 (50 percent)

Allocation/thread-creation failure and unavailable CPU-clock paths remain
incompletely exercised. GNU C integration/matcher code and shell/PowerShell
orchestration were not coverage-instrumented. Clang static analysis emitted no
diagnostics for the current queue and vector modules.
ThreadSanitizer was not run, so these checks are not a race-detector result.
The profiled campaign used full scans. An immediate quiet-mode process exit
can join through destruction without emitting the normal end-of-reader record.

All 28 C# tests passed. Targeted Stryker.NET testing of the oracle, quantile,
profile validation and profile aggregation killed 83 of 91 testable mutants
(91.21 percent): eight survived, eleven produced compiler errors, 366 were
ignored, and none timed out. Two survivors remove empty-line handling that does
not change the fixed benchmark patterns; six change exception messages. They
remain recorded as survivors, without a formal equivalence claim. Mutation
findings led to additional empty-profile, zero-counter and overflow checks.

Coverlet source-focused coverage is reported separately, without merging scopes:

.. list-table:: Handwritten C# source coverage, percent lines / branches
   :header-rows: 1

   * - Run
     - Experiment.cs
     - ProfileReport.cs
   * - Unit tests
     - 15.15 / 33.33
     - 30.77 / 40.00
   * - Integration checks
     - 62.12 / 51.69
     - 0 / 0
   * - Profile report generation
     - 6.06 / 2.54
     - 97.44 / 83.33

The timing and profile-collection loops ran in the Native AOT helper, outside
coverage instrumentation. Whole-module reports include generated JSON code;
raw XML and source-focused counts are in results/optimized/ and results/profile/.
Collector/console branch mappings differ, so the separate scopes are not
equivalent or additive. StrictDoc export and Sphinx with warnings treated as
errors passed for the current requirements and engineering notes.

Use the checked-directory commands in README.rst, with the current entry points::

    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Build
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Benchmark
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode Profile
    .\Run-Experiment.ps1 -BenchmarkRoot $benchmark -Mode NativeVerify
    .\Verify-Harness.ps1 -BenchmarkRoot $benchmark -Mutation

An existing old-queue.exe is retained unchanged. A clean build reconstructs it
using the frozen f430570 adapter in previous/. Fresh builds must record new
hashes and rerun measurements. kwset is now recompiled with vector helper calls;
the other matcher objects and gnulib remain shared with the configured baseline.
No remote CI was enabled or dispatched.
