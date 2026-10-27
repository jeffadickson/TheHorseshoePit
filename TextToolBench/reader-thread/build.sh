#!/usr/bin/env bash
# Incremental MSYS2 builds; invoke from the existing benchmark's compiler shell.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
: "${BENCH_ROOT:?Set BENCH_ROOT to the existing TextToolBench workspace}"
bench=$(cygpath -u "$BENCH_ROOT")
test -d "$root/source" && test -d "$bench/tools/grep-build/native-o3"
test -d "$root/results" || mkdir "$root/results"
test -d "$root/build" || mkdir "$root/build"
for variant in optimized profile; do
    test -d "$root/results/$variant" || mkdir "$root/results/$variant"
done
export MSYSTEM=CYGWIN gl_cv_have_weak=no
flags='-O3 -march=native -mtune=native -ftree-vectorize -pipe'
gcc -O3 -march=native -mtune=native -ftree-vectorize -Q --help=target > "$root/results/optimized/compiler-target.txt"
gcc -O3 -march=native -mtune=native -ftree-vectorize -Q --help=optimizers > "$root/results/optimized/compiler-optimizers.txt"
g++ --version > "$root/results/optimized/compiler.txt"
gcc -v >> "$root/results/optimized/compiler.txt" 2>&1
pacman -Q > "$root/results/optimized/packages.txt"
printf '%s\n' "$flags" > "$root/results/optimized/flags.txt"
# Confirm the previously measured baseline is up to date, then preserve it.
make -C "$bench/tools/grep-build/native-o3" -j4 > "$root/results/optimized/baseline-build.log" 2>&1
cp -p "$bench/tools/grep-build/native-o3/src/grep.exe" "$root/baseline.exe"
if [[ ! -f "$root/reader.o" || "$root/read-ahead.cpp" -nt "$root/reader.o" \
      || "$root/read-ahead.h" -nt "$root/reader.o" || "$root/queue-profile.h" -nt "$root/reader.o" ]]; then
    g++ -std=c++20 -D_POSIX_C_SOURCE=200809L $flags -Wall -Wextra -Wpedantic -Werror \
        -pthread -c "$root/read-ahead.cpp" -o "$root/reader.o"
fi
baseline="$bench/tools/grep-build/native-o3"
source="$bench/tools/gnu-source/extracted/grep-3.0"
includes=(-DHAVE_CONFIG_H -I"$baseline/src" -I"$source/src"
    -I"$baseline" -I"$baseline/lib" -I"$source/lib")
vectors=(-include "$baseline/config.h" -include "$root/vector-search.h"
    -Dmemchr=grep_vector_memchr -Dmemchr2=grep_vector_memchr2)
# Retain the exact historical executable when present. On a clean checkout,
# reconstruct that control from the frozen f430570 adapter and original driver.
if [[ ! -f "$root/old-queue.exe" ]]; then
    test -d "$root/previous"
    g++ -std=c++20 -D_POSIX_C_SOURCE=200809L $flags -pthread \
        -c "$root/previous/read-ahead.cpp" -o "$root/build/previous-reader.o"
    gcc $flags "${includes[@]}" -c "$root/source/src/grep.c" -o "$root/build/previous-grep.o"
    gcc $flags -o "$root/old-queue.exe" "$root/build/previous-grep.o" "$root/build/previous-reader.o" \
        "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$baseline/src/kwset.o" \
        "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
        "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
        -liconv -lpcre -lstdc++ -pthread
fi
# Reuse the exact configured gnulib and matcher objects. Only the driver and
# adapter change, avoiding a second configure and any matcher-code differences.
if [[ ! -f "$root/build/grep-vector.o" || "$root/source/src/grep.c" -nt "$root/build/grep-vector.o" \
      || "$root/source/src/read-ahead.h" -nt "$root/build/grep-vector.o" \
      || "$root/vector-search.h" -nt "$root/build/grep-vector.o" ]]; then
    gcc $flags "${includes[@]}" "${vectors[@]}" \
        -c "$root/source/src/grep.c" -o "$root/build/grep-vector.o" \
        > "$root/results/optimized/threaded-build.log" 2>&1
fi
if [[ ! -f "$root/build/kwset-vector.o" || "$root/vector-search.h" -nt "$root/build/kwset-vector.o" ]]; then
    gcc $flags "${includes[@]}" "${vectors[@]}" -c "$source/src/kwset.c" -o "$root/build/kwset-vector.o"
fi
if [[ ! -f "$root/vector.o" || "$root/vector-search.cpp" -nt "$root/vector.o" \
      || "$root/vector-search.h" -nt "$root/vector.o" ]]; then
    g++ -std=c++20 $flags -Wall -Wextra -Wpedantic -Werror -c "$root/vector-search.cpp" -o "$root/vector.o"
fi
if [[ ! -f "$root/threaded.exe" || "$root/build/grep-vector.o" -nt "$root/threaded.exe" \
      || "$root/reader.o" -nt "$root/threaded.exe" || "$root/vector.o" -nt "$root/threaded.exe" \
      || "$root/build/kwset-vector.o" -nt "$root/threaded.exe" || "$root/baseline.exe" -nt "$root/threaded.exe" ]]; then
    gcc $flags -o "$root/threaded.exe" "$root/build/grep-vector.o" "$root/reader.o" "$root/vector.o" \
        "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$root/build/kwset-vector.o" \
        "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
        "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
        -liconv -lpcre -lstdc++ -pthread >> "$root/results/optimized/threaded-build.log" 2>&1
fi
# Profiling keeps O3/native/vectorization, but records stage clocks and symbols.
# This separate executable is never used for reported timing comparisons.
if [[ ! -f "$root/profile-reader.o" || "$root/read-ahead.cpp" -nt "$root/profile-reader.o" \
      || "$root/read-ahead.h" -nt "$root/profile-reader.o" || "$root/queue-profile.h" -nt "$root/profile-reader.o" ]]; then
    g++ -std=c++20 -D_POSIX_C_SOURCE=200809L -DQUEUE_PROFILE $flags -g -fno-omit-frame-pointer \
        -Wall -Wextra -Wpedantic -Werror -pthread -c "$root/read-ahead.cpp" -o "$root/profile-reader.o"
fi
if [[ ! -f "$root/profile.exe" || "$root/threaded.exe" -nt "$root/profile.exe" \
      || "$root/profile-reader.o" -nt "$root/profile.exe" ]]; then
    gcc $flags -g -o "$root/profile.exe" "$root/build/grep-vector.o" "$root/profile-reader.o" "$root/vector.o" \
        "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$root/build/kwset-vector.o" \
        "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
        "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
        -liconv -lpcre -lstdc++ -pthread > "$root/results/profile/build.log" 2>&1
fi
cp "$baseline/config.log" "$root/results/optimized/baseline-config.log"
"$root/threaded.exe" --version > "$root/results/optimized/version.txt"
echo 'Original and C++20 reader variants built.'
