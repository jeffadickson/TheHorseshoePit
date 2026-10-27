#!/usr/bin/env bash
# Same-source compiler experiments; runtime and compiler are shared within each language pair.
set -euo pipefail
export PATH=/usr/bin:$PATH
root=$(cygpath -u "${PROJECT_ROOT:?Set PROJECT_ROOT}")
[[ -d "$root/tools/diff-suite" && -d "$root/results/diff" && -d "$root/upstream" ]]
export PATH=/usr/bin:$PATH MSYSTEM=CYGWIN
source_dir="$root/tools/diff-suite/diffutils-3.12"
if [[ ! -f "$source_dir/configure" ]]; then tar -xf "$root/upstream/diffutils-3.12.tar.xz" -C "$root/tools/diff-suite"; fi
# GCC 16's C23 stddef handling and this bundled gnulib suppress newlib's required wint_t request.
# Both variants use the same small header compatibility patch, never an algorithm change.
if ! grep -q 'STDDEF_NOT_IDEMPOTENT@ || defined __need_wint_t' "$source_dir/lib/stddef.in.h"; then
  (cd "$source_dir"; patch -p1 < "$root/scripts/diff-stddef-wint.patch")
fi
gcc -v >"$root/results/diff/gcc.txt" 2>&1
gcc -Q -march=native -mtune=native --help=target >"$root/results/diff/native-target.txt" 2>&1
configured="$root/tools/diff-suite/gnu-generic"
mkdir -p "$configured/bin"
for variant in generic native; do
  output="$root/tools/diff-suite/gnu-$variant"
  mkdir -p "$output/src"
  if [[ "$variant" == generic ]]; then flags='-O2 -march=nocona -msahf -mtune=generic -pipe';
  else flags="-O3 -march=native -mtune=native -ftree-vectorize -pipe -fopt-info-vec-optimized=$output/vectorization.log"; fi
  printf '%s\n' "$flags" >"$root/results/diff/gnu-$variant.flags.txt"
  artifact="$output/src/diff.exe"
  if [[ "$variant" == generic ]]; then artifact="$configured/bin/diff.exe"; fi
  if [[ ! -x "$artifact" ]]; then
    (
      cd "$configured"
      if [[ ! -f Makefile ]]; then CFLAGS='-O2 -march=nocona -msahf -mtune=generic -pipe' "$source_dir/configure" --build=x86_64-pc-cygwin --host=x86_64-pc-cygwin --disable-nls; fi
      # Share feature detection, then clean all objects so every translation unit uses the selected flags.
      make clean
      printf 'Rebuild all objects with CFLAGS=%s\n' "$flags"
      make -j4 V=1 CFLAGS="$flags"
      cp src/diff.exe "$artifact"
    ) >"$root/results/diff/gnu-$variant.rebuild.log" 2>&1
  fi
  cp "$configured/config.log" "$root/results/diff/gnu-$variant.config.log"
  if [[ -f "$output/vectorization.log" ]]; then cp "$output/vectorization.log" "$root/results/diff/gnu-$variant.vectorization.log"; fi
  "$artifact" --version | head -1
done
