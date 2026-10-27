#!/usr/bin/env bash
# Separate names preserve every earlier timing executable and evidence cohort.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
: "${BENCH_ROOT:?Set BENCH_ROOT to the existing benchmark workspace}"
bench=$(cygpath -u "$BENCH_ROOT")
test -d "$root/build" && test -d "$bench/tools/grep-build/native-o3"
test -d "$root/results/copy" || mkdir "$root/results/copy"
out="$root/results/copy"
test -d "$out/profile" || mkdir "$out/profile"
flags=(-O3 -march=native -mtune=native -ftree-vectorize -pipe)
baseline="$bench/tools/grep-build/native-o3"
for pair in memcpy:0 avx2:1 rep:2; do
    kind=${pair%:*}; number=${pair#*:}
    object="$root/build/copy-$kind.o"
    if [[ ! -f "$object" || "$root/memory-copy.cpp" -nt "$object" || "$root/memory-copy.h" -nt "$object" ]]; then
        g++ -std=c++20 "${flags[@]}" -DQUEUE_COPY_KIND="$number" -Dqueue_memory_copy="copy_$kind" \
            -Wall -Wextra -Wpedantic -Werror -c "$root/memory-copy.cpp" -o "$object"
    fi
    for mode in release profile; do
        extra=(); suffix=''
        if [[ "$mode" == profile ]]; then extra=(-DQUEUE_PROFILE -g -fno-omit-frame-pointer); suffix='-profile'; fi
        reader="$root/build/copy-$kind-reader$suffix.o"
        memory="$root/build/copy-$kind-memory.o"
        if [[ ! -f "$reader" || "$root/read-ahead.cpp" -nt "$reader" || "$root/read-ahead.h" -nt "$reader" \
              || "$root/queue-profile.h" -nt "$reader" || "$root/memory-copy.h" -nt "$reader" ]]; then
            g++ -std=c++20 -D_POSIX_C_SOURCE=200809L "${flags[@]}" "${extra[@]}" \
                -DQUEUE_COPY_KIND="$number" -Wall -Wextra -Wpedantic -Werror -pthread \
                -c "$root/read-ahead.cpp" -o "$reader"
        fi
        if [[ ! -f "$memory" || "$root/memory-copy.cpp" -nt "$memory" || "$root/memory-copy.h" -nt "$memory" ]]; then
            g++ -std=c++20 "${flags[@]}" -DQUEUE_COPY_KIND="$number" -Wall -Wextra -Wpedantic -Werror \
                -c "$root/memory-copy.cpp" -o "$memory"
        fi
        exe="$root/copy-$kind$suffix.exe"
        if [[ ! -f "$exe" || "$reader" -nt "$exe" || "$memory" -nt "$exe" \
              || "$root/build/grep-vector.o" -nt "$exe" || "$root/build/kwset-vector.o" -nt "$exe" || "$root/vector.o" -nt "$exe" ]]; then
            gcc "${flags[@]}" -o "$exe" "$root/build/grep-vector.o" "$reader" "$memory" "$root/vector.o" \
                "$baseline/src/dfasearch.o" "$baseline/src/kwsearch.o" "$root/build/kwset-vector.o" \
                "$baseline/src/pcresearch.o" "$baseline/src/searchutils.o" \
                "$baseline/lib/libgreputils.a" -lintl "$baseline/lib/libgreputils.a" \
                -liconv -lpcre -lstdc++ -pthread
        fi
    done
    objdump -d "$root/build/copy-$kind-memory.o" > "$out/copy-$kind.asm"
done
if [[ ! -f "$root/copy-kernel.exe" || "$root/copy-kernel.cpp" -nt "$root/copy-kernel.exe" \
      || "$root/build/copy-memcpy.o" -nt "$root/copy-kernel.exe" || "$root/build/copy-avx2.o" -nt "$root/copy-kernel.exe" \
      || "$root/build/copy-rep.o" -nt "$root/copy-kernel.exe" ]]; then
    g++ -std=c++20 "${flags[@]}" -Wall -Wextra -Wpedantic -Werror "$root/copy-kernel.cpp" \
        "$root/build/copy-memcpy.o" "$root/build/copy-avx2.o" "$root/build/copy-rep.o" -o "$root/copy-kernel.exe"
fi
g++ --version > "$out/compiler.txt"
printf '%s\n' '-std=c++20 -O3 -march=native -mtune=native -ftree-vectorize -pipe; search block=32; queue slots=3; chunk=262144' > "$out/flags.txt"
gcc -O3 -march=native -mtune=native -ftree-vectorize -Q --help=target > "$out/compiler-target.txt"
objdump -h "$root/build/copy-"*-memory.o > "$out/sections.txt"
echo 'Copy variants and isolated copy/consume kernel built.'
