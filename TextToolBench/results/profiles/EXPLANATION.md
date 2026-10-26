# What the Rust profiles explain

The measurement is owned-process **wall-clock stack sampling**, not an ETW CPU profile. A separate native Rust control loop verifies both hot-function attribution and caller unwinding. Debug information was added to separate release builds; the original benchmark binaries were left intact. Every profiled output was compared to the existing independent reference digest.

Three runs per workload produced the following inclusive observations. Percentages count sampled stacks containing the named function; functions overlap and percentages must not be added or read as exact CPU-time shares.

| Case | Total samples | Relevant caller | Stacks containing caller |
|---|---:|---|---:|
| awk-rs field extraction | 831 | `std::sys::stdio::windows::write` | 658 (79.2%) |
| rawk field extraction | 1338 | `std::sys::stdio::windows::write` | 619 (46.3%) |
| awk-rs line count | 163 | `Interpreter::split_fields` | 108 (66.3%) |
| awk-rs word count | 220 | `Interpreter::split_fields` | 124 (56.4%) |
| rawk word count | 436 | `Evaluator::split_line_into_fields` | 154 (35.3%) |

The field-extraction stacks contain the Rust stdout/line-writer flush path. The two dominant unresolved system leaf offsets also map to `NtWriteFile` and `NtDeviceIoControlFile` in the retained ntdll export table. The caller frames are the stronger application-level evidence: the program is repeatedly entering the Windows stdout write path.

## Controlled buffering experiment

Separate source snapshots add a 64 KiB `BufWriter` around CLI stdout, with an explicit final flush. The original repositories and executables remain unchanged. Both variants use the same compiler, dependency lockfiles and release profiles as their respective baselines.

On 32 MiB field extraction, the medians in this interleaved experiment were:

| Implementation | Original | 64 KiB stdout buffer | Improvement |
|---|---:|---:|---:|
| awk-rs | 4.1666 s | 0.8392 s | 4.96x |
| rawk | 4.5150 s | 2.9339 s | 1.54x |

All three operations were measured, with one warmup and seven repetitions per implementation/operation, and all outputs matched. Ninety-six fixture checks, four malformed-program checks and twelve deliberately wrong programs passed the validation requirements. Raw data are in `../buffering-runs.jsonl`; patch files are in `../patches/`.

This confirms output buffering as a major cause for field extraction, especially in awk-rs. It does not explain all remaining processing time. Eager field allocation in awk-rs and copying/repeated field splitting in rawk remain evidenced by the counting stacks and source, but no allocation-count profiler was run. The short counting cases have far fewer samples, so small percentage differences should not be overinterpreted.
