# frawk bug report: first field corrupted when an incomplete record is rescanned

Affected source: `ezrosent/frawk` commit `d548b159a5a76c12de508f8bf415fdc09e609f0a`, built on Windows MSVC with default features disabled and run with `-B interp`. The original source's dependency lock was stale for its manifest; the resolved lock used in these builds is retained in `dependency-locks/frawk.Cargo.lock`.

## Minimized reproducer

Write 8,190 `x` characters followed by ` y\n`, and run:

```text
frawk -B interp "NF {print $1}" input.txt
```

Expected: the 8,190 `x` characters and one newline. Actual: a space and a newline. `NF` is also wrong (1 instead of 2). A first field of 8,189 characters passes: the failure crosses the initial 8 KiB input-buffer boundary. This is the smallest failure in the reduced monotonic family, with adjacent lengths checked explicitly; it is not a claim of a globally shortest input under all encodings/record layouts.

The original discovery used a **131,072-byte field (128 KiB)**; the failure is not limited to that size. A record with 8,190 `x` bytes plus ` y\n` is 8,193 bytes, exceeding the initial 8,192-byte buffer by one byte.

A POSIX-shell reproducer, using a reference awk to generate the input, is:

```sh
awk 'BEGIN { for (i = 0; i < 8190; ++i) printf "x"; print " y" }' > long-field.txt
frawk -B interp 'NF {print $1}' long-field.txt
frawk -B interp '{print NF}' long-field.txt
```

The first frawk command should emit 8,191 bytes (`x` repeated 8,190 times, then LF), but the affected executable emits two bytes (space, LF). The second should print `2`, but prints `1`. The record itself, `$0`, remains complete. This is an output-correctness bug, not a timing discrepancy or evidence of an out-of-bounds write.

## Cause

`WhitespaceChunkProducer::get_chunk` scans the current buffer and updates `self.1`, the carry describing whitespace state at its tail. When no full record fits, the reader grows the buffer and retains the **whole incomplete prefix** for rescanning. The retry incorrectly uses the state from the previous scan's end rather than the state before its beginning. This loses the first field's opening offset and mispairs the remaining boundaries. `$0` still contains the complete record, confirming that this is scanner bookkeeping rather than file truncation.

The affected control flow in `src/runtime/splitter/chunk.rs` is below, abridged to show the scan and retry:

```rust
let bs = buf.as_bytes();
self.1 = (self.0.find_indexes)(bs, &mut chunk.off, self.1);
// ... determine whether a complete record was found ...
let is_eof = self.0.inner.reset()?;
return match (is_partial, is_eof) {
    // ... branches that emit a completed or final chunk ...
    (true, false) => continue,
};
```

The retry invariant is: **rescanning a retained prefix must start with the same scanner state as its first scan**. `reset()` retains the prefix but does not undo the caller's update to `self.1`. Reusing the tail state effectively treats the beginning of the record as a continuation of its last field.

## Minimal fix

Save `initial_whitespace` before scanning. In the `(true, false)` retry branch, restore it before rescanning the retained prefix. No record was emitted in that branch, so no input-state transition should be committed. Original repositories and benchmark binaries remain intact; the patched executable is separate.

Corrected control flow, with unrelated branches omitted:

```rust
let bs = buf.as_bytes();
let initial_whitespace = self.1;
self.1 = (self.0.find_indexes)(bs, &mut chunk.off, self.1);
// ... determine whether a complete record was found ...
let is_eof = self.0.inner.reset()?;
return match (is_partial, is_eof) {
    // ... unchanged branches that emit a completed or final chunk ...
    (true, false) => {
        // The retained prefix will be scanned again from its beginning.
        self.1 = initial_whitespace;
        continue;
    }
};
```

**[Combined fix and regression-test patch](frawk-long-field-fix.patch)** modifies only `src/runtime/splitter/chunk.rs` and `src/runtime/splitter/batch.rs`. The separate [implementation patch](../patches/frawk-longline.patch) and [test patch](../patches/frawk-longline-tests.patch) are also retained. Patch paths are relative to the upstream repository root.

Copy the combined patch into a checkout of the affected revision, then check and apply it from that checkout:

```powershell
$checkout = (Get-Location).Path
if (!(Test-Path -LiteralPath (Join-Path $checkout 'src/runtime/splitter') -PathType Container)) { throw 'Run from the frawk checkout root' }
$patch = Join-Path $checkout 'frawk-long-field-fix.patch'
if (!(Test-Path -LiteralPath $patch -PathType Leaf)) { throw 'Copy the patch into this checkout first' }
git apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly' }
git apply $patch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed' }
```

The patch does not include a dependency-lock change. The upstream manifest/lock mismatch and the separately retained resolved lock are build-provenance issues, independent of the two source edits.

## Verification

- Boundary sweep: 24 field lengths, four programs each (96 checks), through a 262 KiB field; all patched outputs match. Original failures are retained in `boundaries-original.csv`.
- Seeded CLI property cases: seed **20261105**, 72 inputs × two programs = **144 checks**. They vary long-field length, leading spaces/tabs, prior records, a third field, and missing final newline. Original: **10/144 pass**; patched: **144/144 pass**.
- A targeted cargo-nextest run passed `runtime::splitter::batch::tests::whitespace_splitter`, including new growth-boundary regressions for generic/SSE2/AVX2 kernels where available. One test ran; 284 unrelated tests were skipped. The new unit cases include 1 KiB buffer boundaries, repeated growth and fields after prior records.
- Restoring the original logic is the negative/mutation control: it fails the minimized and randomized cases. No upstream issue or pull request was sent.

## Coverage

The separate instrumented release build passed all 144 property checks. LLVM coverage reports **24.77% line coverage** for the complete `splitter/chunk.rs` file (110/444 lines) and **18.30%** for the instrumented project including generated parser code. These are targeted regression runs, not a whole-project test campaign. The new state-save statement executed 810 times and the restore/continue statements 600 times. Branch coverage was not emitted by the stable Rust coverage configuration, so its zero branch counters are **unavailable**, not 100% or proof of untested branches. Raw LLVM summary, report and merged profile are in `coverage/`; intermediate per-process `.profraw` files are reacquired and excluded from Git.

This fixes the long-line whitespace bug only. The separate Windows Cranelift JIT panic remains an explicit unsupported result in `../frawk-cranelift-compatibility.json`.
