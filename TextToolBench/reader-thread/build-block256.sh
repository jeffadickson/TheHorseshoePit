#!/usr/bin/env bash
# Build an isolated variant without replacing the measured 32-byte executable.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
: "${BENCH_ROOT:?Set BENCH_ROOT to the existing benchmark workspace}"
bench=$(cygpath -u "$BENCH_ROOT")
test -d "$root/build" && test -d "$bench/tools/grep-build/native-o3"
test -d "$root/results/block256" || mkdir "$root/results/block256"
flags=(-O3 -march=native -mtune=native -ftree-vectorize -pipe)
if [[ ! -f "$root/vector-256.o" || "$root/vector-search.cpp" -nt "$root/vector-256.o" \
      || "$root/vector-search.h" -nt "$root/vector-256.o" ]]; then
    g++ -std=c++20 "${flags[@]}" -DGREP_VECTOR_BLOCK=256 -Wall -Wextra -Wpedantic -Werror \
        -c "$root/vector-search.cpp" -o "$root/vector-256.o"
fi
if [[ ! -f "$root/threaded-256.exe" || "$root/vector-256.o" -nt "$root/threaded-256.exe" \
      || "$root/reader.o" -nt "$root/threaded-256.exe" ]]; then
    baseline="$bench/tools/grep-build/native-o3"
    gcc "${flags[@]}" -o "$root/threaded-256.exe" "$root/build/grep-vector.o" "$root/reader.o" "$root/vector-256.o" \
        "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$root/build/kwset-vector.o" \
        "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
        "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
        -liconv -lpcre -lstdc++ -pthread
fi
g++ --version > "$root/results/block256/compiler.txt"
printf '%s\n' '-std=c++20 -O3 -march=native -mtune=native -ftree-vectorize -DGREP_VECTOR_BLOCK=256' \
    > "$root/results/block256/flags.txt"
objdump -h "$root/vector.o" "$root/vector-256.o" > "$root/results/block256/sections.txt"
objdump -d "$root/vector-256.o" > "$root/results/block256/vector-256.asm"
# Both kernels live in one process for interleaved, startup-free measurements.
for block in 32 256; do
    if [[ ! -f "$root/build/kernel-$block.o" || "$root/vector-search.cpp" -nt "$root/build/kernel-$block.o" \
          || "$root/vector-search.h" -nt "$root/build/kernel-$block.o" ]]; then
        g++ -std=c++20 "${flags[@]}" -DGREP_VECTOR_BLOCK="$block" \
            -Dgrep_vector_memchr="grep${block}_memchr" -Dgrep_vector_memchr2="grep${block}_memchr2" \
            -c "$root/vector-search.cpp" -o "$root/build/kernel-$block.o"
    fi
done
if [[ ! -f "$root/kernel.exe" || "$root/vector-kernel.cpp" -nt "$root/kernel.exe" \
      || "$root/build/kernel-32.o" -nt "$root/kernel.exe" || "$root/build/kernel-256.o" -nt "$root/kernel.exe" ]]; then
    g++ -std=c++20 "${flags[@]}" -Wall -Wextra -Wpedantic -Werror "$root/vector-kernel.cpp" \
        "$root/build/kernel-32.o" "$root/build/kernel-256.o" -o "$root/kernel.exe"
fi
echo '256-byte variant built; original 32-byte executable preserved.'
