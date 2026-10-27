#!/usr/bin/env bash
# Incremental MSYS2 builds; invoke from the existing benchmark's compiler shell.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
: "${BENCH_ROOT:?Set BENCH_ROOT to the existing TextToolBench workspace}"
bench=$(cygpath -u "$BENCH_ROOT")
test -d "$root/source" && test -d "$bench/tools/grep-build/native-o3"
test -d "$root/results" || mkdir "$root/results"
test -d "$root/build" || mkdir "$root/build"
export MSYSTEM=CYGWIN gl_cv_have_weak=no
flags='-O3 -march=native -mtune=native -ftree-vectorize -pipe'
g++ --version > "$root/results/compiler.txt"
gcc -v >> "$root/results/compiler.txt" 2>&1
pacman -Q > "$root/results/packages.txt"
printf '%s\n' "$flags" > "$root/results/flags.txt"
# Confirm the previously measured baseline is up to date, then preserve it.
make -C "$bench/tools/grep-build/native-o3" -j4 > "$root/results/baseline-build.log" 2>&1
cp -p "$bench/tools/grep-build/native-o3/src/grep.exe" "$root/baseline.exe"
if [[ ! -f "$root/reader.o" || "$root/read-ahead.cpp" -nt "$root/reader.o" \
      || "$root/read-ahead.h" -nt "$root/reader.o" ]]; then
    g++ -std=c++20 -D_POSIX_C_SOURCE=200809L $flags -Wall -Wextra -Wpedantic -Werror \
        -pthread -c "$root/read-ahead.cpp" -o "$root/reader.o"
fi
baseline="$bench/tools/grep-build/native-o3"
source="$bench/tools/gnu-source/extracted/grep-3.0"
# Reuse the exact configured gnulib and matcher objects. Only the driver and
# adapter change, avoiding a second configure and any matcher-code differences.
if [[ ! -f "$root/build/grep.o" || "$root/source/src/grep.c" -nt "$root/build/grep.o" \
      || "$root/source/src/read-ahead.h" -nt "$root/build/grep.o" ]]; then
    gcc -DHAVE_CONFIG_H $flags -I"$baseline/src" -I"$source/src" \
        -I"$baseline" -I"$baseline/lib" -I"$source/lib" \
        -c "$root/source/src/grep.c" -o "$root/build/grep.o" \
        > "$root/results/threaded-build.log" 2>&1
fi
if [[ ! -f "$root/threaded.exe" || "$root/build/grep.o" -nt "$root/threaded.exe" \
      || "$root/reader.o" -nt "$root/threaded.exe" || "$root/baseline.exe" -nt "$root/threaded.exe" ]]; then
    gcc $flags -o "$root/threaded.exe" "$root/build/grep.o" "$root/reader.o" \
        "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$baseline/src/kwset.o" \
        "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
        "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
        -liconv -lpcre -lstdc++ -pthread >> "$root/results/threaded-build.log" 2>&1
fi
cp "$baseline/config.log" "$root/results/baseline-config.log"
"$root/threaded.exe" --version > "$root/results/version.txt"
echo 'Original and C++20 reader variants built.'
