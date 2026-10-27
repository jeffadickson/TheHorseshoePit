Queue memory-copy instruction experiment
========================================

AVX2 copying did not establish a repeatable whole-grep improvement. The
isolated copy median was 3.3% lower, and copy with immediate consumption was
1.4% lower, with overlapping quartiles. Queue profiles did not consistently
reduce the copy interval either. Keep memcpy as the default.

This experiment compares the existing runtime memcpy with explicit cached
AVX2 copying and rep movsb. The normal queue retains memcpy. These separate
binaries isolate the copy choice; they retain the same 32-byte vector-search
objects, GNU grep matcher objects, 3 queue slots and 256 KiB chunks.

Measured results
----------------

Whole-grep median milliseconds, 15 measurements per cell:

.. list-table:: Original corpus, same-session release executables
   :header-rows: 1

   * - Task
     - memcpy
     - AVX2
     - rep movsb
   * - Literal
     - 134.146
     - 119.667
     - 120.244
   * - Count
     - 107.344
     - 131.477
     - 115.069
   * - Regex
     - 152.832
     - 141.925
     - 143.102
   * - Nonblank
     - 233.884
     - 288.176
     - 232.156
   * - Absent literal
     - 84.760
     - 86.400
     - 84.443

All 255 release outputs matched: 30 warmup and 225 measured runs. The host
showed substantial interference. For example, count quartiles were
100.989..143.434 ms for memcpy and 102.769..158.878 ms for AVX2. Nonblank
quartiles were 220.247..329.027 versus 215.496..401.233 ms. Apparent median
wins and losses in this small sample do not establish stable differences.
Full quartiles, CPU observations and raw runs are in results/copy.

In-process median milliseconds per 32 MiB corpus pass:

.. list-table:: 16 passes per observation, 15 observations per cell
   :header-rows: 1

   * - Kernel
     - memcpy
     - AVX2
     - rep movsb
   * - Copy
     - 3.653
     - 3.533
     - 3.868
   * - Copy and consume
     - 9.545
     - 9.410
     - 9.340

The copy quartiles were 3.602..4.003 ms for memcpy and 3.338..4.107 ms for
AVX2. Copy-and-consume quartiles were 9.244..10.108 and 9.061..10.028 ms.
All 102 kernel observations passed their reference checks, with 90 measured.
The slight median changes do not support a substantial-copy-speedup claim.

Median diagnostic copy interval, milliseconds per complete input scan:

.. list-table:: Seven repetitions per task/engine
   :header-rows: 1

   * - Task
     - memcpy
     - AVX2
     - rep movsb
   * - Literal
     - 2.820
     - 3.053
     - 3.147
   * - Count
     - 2.724
     - 2.800
     - 2.465
   * - Regex
     - 3.841
     - 3.539
     - 3.358
   * - Nonblank
     - 3.799
     - 4.807
     - 4.396
   * - Absent literal
     - 1.708
     - 2.522
     - 2.208

All 105 profiles passed output and counter validation. Median reader lifetime
count remained four per file; 34,013,188 bytes were read versus 33,554,436
delivered, leaving 448 KiB discarded. Consumer wait remained below 0.5 ms in
the task/engine medians, while producer full-queue wait ranged about 15..173
ms. The reader still keeps up in these warm-cache tests. Avoiding unnecessary
reader restarts and eliminating a copy remain separate, unimplemented
candidates; this experiment does not establish their performance.

Implementation and rationale
----------------------------

* memcpy: the original Reader::read call is retained at compile-time setting 0.
* AVX2: eight unaligned 32-byte loads/stores handle each 256-byte block, followed
  by complete 32-byte vectors and a standard-library tail of at most 31 bytes.
* rep: an inline rep movsb copies the exact byte count. Its operands and memory
  clobber expose register updates and memory effects to GCC/Clang.

The contract is nonoverlapping initialized source/destination regions, with no
alignment requirement and no access beyond the count. A zero count permits
null pointers. This is not an overlap-safe memmove replacement. The source
slot remains reserved until copying finishes, as in the existing queue.

The standard library remains preferable as the default facility. The custom
helpers exist to isolate instruction choices against this particular MSYS2
runtime. No new dependency is needed. Cached stores suit immediate consumption
of the destination. Intel documents length/alignment sensitivity of SIMD versus
enhanced REP copying; see the `Intel optimization reference manual
<https://cdrdv2-public.intel.com/821612/248966-Optimization-Reference-Manual-V1-050.pdf>`_.
No assumption of a universal instruction winner is made.

The actual objects were disassembled: AVX2 emits 256-bit vmovdqu loads/stores;
the rep helper emits rep movsb. Assembly and section sizes are retained under
results/copy. These entire executables target the current processor, rather
than offering portable runtime CPU dispatch.

Measurement method
------------------

Production flags are C++20, -O3 -march=native -mtune=native -ftree-vectorize
-pipe, using MSYS2 GCC 16.2.0 on an Intel Core i5-10505 (6 cores/12 threads).
Diagnostic binaries add QUEUE_PROFILE, symbols and frame pointers. They are
separate from the release timing binaries.

The corpus is the original 33,554,436-byte 32MiB.txt, SHA-256
22E722D4DDB1F74CD43F1AAB654B9571B2E2BD5FC3A9E6F3E2CB09D57C47A1B3.
Warm-cache process runs include startup and stdout hashing. Two warmups and
15 measured rounds per task/engine are shuffled sequentially with seed
20261026. Every output length, hash and exit status is checked against an
independent ASCII oracle. This host has other active work; quartiles and raw
observations are retained. No cold-storage or hardware-counter claim is made.

Profiling uses seven shuffled repetitions per task/engine, separately from
release timings. Queue byte totals and high-water invariants are checked on
every observation. Thread wall intervals overlap and are not additive CPU
shares. Windows thread CPU readings have coarse resolution; zero does not
establish zero work.

The in-process kernel excludes corpus loading and reference construction.
It copies the corpus through a reused 256 KiB destination, either alone or
with immediate newline counting. Each observation averages 16 passes; two
warmups and 15 measurements per cell are shuffled with the same seed. The
last destination is checked after timing; the consume arm additionally checks
the aggregate independent newline count. The standalone native checks verify
every copied byte and the surrounding sentinels more thoroughly.

Verification and limitations
----------------------------

Each copy choice passed 103,074 independent byte-reference cases, 2,056
protected-boundary cases, and the null/zero case under ASan/UBSan. Cases cover
all source/destination alignments modulo 64 around vector boundaries, every
length through 513 with sampled alignments, and larger buffers through 8 MiB.
The protected regions cover both starting/ending source and destination
boundaries. Each queue choice also passed 144 queue scenarios, negative API
checks, 345,088 vector reference cases and 1,028 search guard-page cases.

libFuzzer ran 5,000 inputs per copy choice and another 5,000 against the AVX2
queue integration, all with seed 20261026. Initial/final corpora and logs are
retained. Five deliberately faulty copies were caught: block bound (exit
139), block stride, store lane, scalar tail and rep count (each exit 134).
The AVX2 and rep helpers passed Memcheck with zero errors/leaks and Clang
static analysis without diagnostics. Invalid copy setting 3 was rejected.
ASan cannot instrument the inline rep assembly accesses; protected pages,
sentinels and Memcheck provide the additional checks for that path.

Linux/WSL verification and Windows/MSYS2 performance are separate scopes.
Native gcov results, without merging scopes, are:

* AVX2 helper: 25/25 source lines and 8/8 branch directions (100% each).
* memcpy and rep helper builds: each 3/3 lines and 2/2 branch directions.
* Queue integration with AVX2 copying: 104/105 lines (99.05%) and 97/140 branch
  directions (69.29%). Allocation/thread failure and exceptional library paths
  remain incompletely exercised. No ThreadSanitizer campaign was run.

The managed harness passed 29 NUnit tests, including selected-cohort profile
summary generation and missing-clock preservation. Its integration checks
passed 90 independent oracle comparisons, 97 differential comparisons and
18 negative checks. Whole-module unit coverage, including generated serializer
code, is 69/1,778 lines (3.88%) and 70/308 branch directions (22.72%). Separate
managed integration coverage is 354/1,778 lines (19.91%) and 89/306 branch
directions (29.08%). These scopes are not merged. Native AOT measurement loops,
the diagnostic header in this cohort and the native kernel harness are not
coverage instrumented. Coverage is not proof of correctness.

Stryker.NET 5.0.0 killed 83/91 testable mutants (91.21%), with 8 survivors,
11 compile errors and 418 ignored mutants. Scope is the oracle, quantile,
profile validation and counter aggregation, not process dispatch. The two
empty-line survivors appear equivalent within these fixed patterns, without
a formal equivalence claim; the other six change diagnostic strings. Raw
mutation evidence is retained. The profile loop now closes its log writer
before summary reading; the initial Windows sharing failure was corrected
and the completed profile path was rerun.

Reproduction and handoff
------------------------

READAHEAD-011 traces to memory-copy.cpp/.h, Reader::read, copy-check.cpp,
copy-kernel.cpp, the build/verification scripts and results/copy. The earlier
cohorts are preserved. hashes.csv records source, compiler/runtime, matcher
object and executable identities; environment.json records the actual timed
executables, corpus and method. Base revision is 95467bc, with a source overlay
recorded by hashes. Unrelated diff-benchmark changes were already present and
were preserved. No remote push or CI dispatch is part of this experiment.

From this extension directory in the existing benchmark checkout layout::

    $extension = (Get-Location).Path
    if (!(Test-Path -LiteralPath $extension -PathType Container)) { throw 'Missing extension' }
    $benchmarkRoot = [IO.Path]::GetFullPath((Join-Path $extension '../../../..'))
    if (!(Test-Path -LiteralPath $benchmarkRoot -PathType Container)) { throw 'Missing benchmark root' }
    & ./Run-Experiment.ps1 -BenchmarkRoot $benchmarkRoot -Mode CopyBuild
    & ./Verify-Harness.ps1 -BenchmarkRoot $benchmarkRoot -Copy -Mutation
    & ./Run-Experiment.ps1 -BenchmarkRoot $benchmarkRoot -Mode CopyVerify
    & ./Run-Experiment.ps1 -BenchmarkRoot $benchmarkRoot -Mode CopyBenchmark
    & ./Run-Experiment.ps1 -BenchmarkRoot $benchmarkRoot -Mode CopyProfile
    & ./Run-Experiment.ps1 -BenchmarkRoot $benchmarkRoot -Mode CopyKernel
    & ./Collect-CopyEvidence.ps1 -BenchmarkRoot $benchmarkRoot

CopyBuild reuses the existing configured GNU grep/gnulib and matcher objects;
run the normal Build mode first when reconstructing those from a clean source
checkout. Ubuntu-24.04 with GCC, Clang, libFuzzer, gcov and Valgrind is needed
for CopyVerify. StrictDoc export uses the real requirements.sdoc file; Sphinx
builds docs/index.rst including this report. Keep the memcpy default unless a
representative workload establishes a repeatable improvement.
