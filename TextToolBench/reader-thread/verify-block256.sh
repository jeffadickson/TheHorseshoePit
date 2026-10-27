#!/usr/bin/env bash
# Native verification is separate from optimized Windows timing binaries.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
test -d "$HOME"
work="$HOME/texttool-block256-verification"
test -d "$work" || mkdir "$work"
test -d "$root/results/block256"
out="$root/results/block256/native"
test -d "$out" || mkdir "$out"
cp "$root"/read-ahead.{h,cpp} "$root/queue-profile.h" "$root"/vector-search.{h,cpp} \
    "$root/native-check.cpp" "$root/vector-check.cpp" "$work/"
cd "$work"
flags=(-std=c++20 -D_POSIX_C_SOURCE=200809L -march=native -mtune=native -ftree-vectorize \
    -pthread -Wall -Wextra -Wpedantic -Werror -g)
sources=(read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp)
clang++ --version > "$out/compiler.txt"
for block in 32 256; do
    clang++ "${flags[@]}" -O1 -DGREP_VECTOR_BLOCK="$block" -fsanitize=address,undefined \
        "${sources[@]}" -o "check-$block"
    GREP_VECTOR_SEARCH=1 "./check-$block" > "$out/check-$block.log" 2>&1
done
g++ "${flags[@]}" -O0 --coverage -DGREP_VECTOR_BLOCK=256 "${sources[@]}" -o covered
for counter in covered-*.gcda; do if [[ -f "$counter" ]]; then rm -- "$counter"; fi; done
GREP_VECTOR_SEARCH=1 ./covered > "$out/coverage-check.log"
GREP_VECTOR_SEARCH=0 ./covered >> "$out/coverage-check.log"
gcov -b -c covered-vector-search.gcno > "$out/gcov.log"
cp vector-search.cpp.gcov "$out/"
clang++ "${flags[@]}" -O1 -DGREP_VECTOR_BLOCK=256 -DLIBFUZZER -fsanitize=fuzzer,address,undefined \
    "${sources[@]}" -o fuzz
test -d corpus || mkdir corpus
printf '\001Prince\nwar\n' > corpus/text
head -c 65537 /dev/zero > corpus/long
tar -czf "$out/initial-corpus.tar.gz" corpus
GREP_VECTOR_SEARCH=1 ./fuzz -seed=20261026 -runs=5000 -max_len=131073 -timeout=10 corpus \
    > "$out/fuzz.log" 2>&1
tar -czf "$out/final-corpus.tar.gz" corpus
cp vector-search.cpp original.cpp
: > "$out/mutations.csv"
for mutation in block-bound block-stride block-lane; do
    cp original.cpp vector-search.cpp
    case "$mutation" in
      block-bound) sed -i 's/while (count >= 256)/while (count >= 32)/' vector-search.cpp ;;
      block-stride) sed -i 's/p += 256;/p += 255;/' vector-search.cpp ;;
      block-lane) sed -i 's/p + 32 + std::countr_zero(m)/p + 64 + std::countr_zero(m)/' vector-search.cpp ;;
    esac
    if cmp -s original.cpp vector-search.cpp; then echo "Mutation failed to apply"; exit 1; fi
    clang++ "${flags[@]}" -O1 -DGREP_VECTOR_BLOCK=256 "${sources[@]}" -o mutant
    set +e
    (ulimit -c 0; GREP_VECTOR_SEARCH=1 timeout 20s ./mutant) > "$out/$mutation.log" 2>&1
    status=$?
    set -e
    printf '%s,%s\n' "$mutation" "$status" >> "$out/mutations.csv"
    if [[ "$status" == 0 || "$status" == 124 ]]; then echo "Survivor/timeout"; exit 1; fi
done
cp original.cpp vector-search.cpp
# Verify initialization discipline and reject unsupported compile-time settings.
g++ "${flags[@]}" -O3 -DGREP_VECTOR_BLOCK=256 "${sources[@]}" -o memcheck
GREP_VECTOR_SEARCH=1 valgrind --tool=memcheck --error-exitcode=99 --leak-check=full \
    ./memcheck > "$out/memcheck.log" 2>&1
if g++ "${flags[@]}" -DGREP_VECTOR_BLOCK=64 -c vector-search.cpp -o invalid.o > "$out/invalid-block.log" 2>&1; then
    echo 'Invalid block size compiled'; exit 1
fi
grep -q 'GREP_VECTOR_BLOCK must be 32 or 256' "$out/invalid-block.log"
clang++ "${flags[@]}" -DGREP_VECTOR_BLOCK=256 --analyze vector-search.cpp > "$out/static-analysis.log" 2>&1
sha256sum original.cpp vector-check.cpp native-check.cpp > "$out/source-hashes.txt"
echo '32/256 regression, guard-page, sanitizer, fuzz, mutation and Memcheck checks passed.'
