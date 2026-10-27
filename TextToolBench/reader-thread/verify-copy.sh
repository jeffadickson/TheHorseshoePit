#!/usr/bin/env bash
# Native WSL verification is separate from Windows optimized measurements.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
test -d "$HOME"
work="$HOME/texttool-copy-verification"
test -d "$work" || mkdir "$work"
test -d "$root/results/copy"
out="$root/results/copy/native"
test -d "$out" || mkdir "$out"
cp "$root"/read-ahead.{h,cpp} "$root/queue-profile.h" "$root"/vector-search.{h,cpp} \
    "$root"/memory-copy.{h,cpp} "$root/native-check.cpp" "$root/vector-check.cpp" "$root/copy-check.cpp" "$work/"
cd "$work"
flags=(-std=c++20 -D_POSIX_C_SOURCE=200809L -march=native -mtune=native -ftree-vectorize \
    -pthread -Wall -Wextra -Wpedantic -Werror -g)
clang++ --version > "$out/compiler.txt"
g++ --version > "$out/gcc.txt"
valgrind --version > "$out/valgrind.txt"
uname -a > "$out/platform.txt"
queue=(read-ahead.cpp vector-search.cpp memory-copy.cpp native-check.cpp vector-check.cpp)
queue_coverage() {
    # Separate integration scope: never merge these percentages with the
    # standalone copy-helper or managed orchestration coverage percentages.
    g++ "${flags[@]}" -O0 -DQUEUE_COPY_KIND=1 --coverage "${queue[@]}" -o queue-covered
    for counter in queue-covered-*.gcda; do if [[ -f "$counter" ]]; then rm -- "$counter"; fi; done
    GREP_VECTOR_SEARCH=1 ./queue-covered > "$out/queue-covered.log"
    gcov -b -c queue-covered-read-ahead.gcno > "$out/gcov-queue.log"
    cp read-ahead.cpp.gcov "$out/"
}
if [[ "${1:-}" == --queue-coverage-only ]]; then queue_coverage; exit 0; fi
for kind in 0 1 2; do
    clang++ "${flags[@]}" -O1 -DQUEUE_COPY_KIND="$kind" -fsanitize=address,undefined memory-copy.cpp copy-check.cpp -o "copy-$kind"
    "./copy-$kind" > "$out/copy-$kind.log" 2>&1
    clang++ "${flags[@]}" -O1 -DQUEUE_COPY_KIND="$kind" -fsanitize=address,undefined "${queue[@]}" -o "queue-$kind"
    GREP_VECTOR_SEARCH=1 "./queue-$kind" > "$out/queue-$kind.log" 2>&1
    g++ "${flags[@]}" -O0 -DQUEUE_COPY_KIND="$kind" --coverage memory-copy.cpp copy-check.cpp -o "covered-$kind"
    for counter in covered-"$kind"-*.gcda; do if [[ -f "$counter" ]]; then rm -- "$counter"; fi; done
    "./covered-$kind" > "$out/covered-$kind.log"
    gcov -b -c "covered-$kind-memory-copy.gcno" > "$out/gcov-$kind.log"
    cp memory-copy.cpp.gcov "$out/memory-copy-$kind.cpp.gcov"
    # This fresh named corpus makes reruns reproducible and retains the actual
    # initial/final libFuzzer inputs, rather than silently reusing discoveries.
    corpus="$(mktemp -d "$work/corpus-$kind-XXXXXX")"
    printf '\001Prince\nwar\n' > "$corpus/text"
    head -c 65537 /dev/zero > "$corpus/long"
    tar -czf "$out/initial-corpus-$kind.tar.gz" -C "$corpus" .
    clang++ "${flags[@]}" -O1 -DQUEUE_COPY_KIND="$kind" -DLIBFUZZER -fsanitize=fuzzer,address,undefined \
        memory-copy.cpp copy-check.cpp -o "fuzz-$kind"
    "./fuzz-$kind" -seed=20261026 -runs=5000 -max_len=131073 -timeout=10 "$corpus" > "$out/fuzz-$kind.log" 2>&1
    tar -czf "$out/final-corpus-$kind.tar.gz" -C "$corpus" .
done
queue_coverage
clang++ "${flags[@]}" -O1 -DQUEUE_COPY_KIND=1 -DLIBFUZZER -fsanitize=fuzzer,address,undefined "${queue[@]}" -o queue-fuzz
queue_corpus="$(mktemp -d "$work/queue-corpus-XXXXXX")"
printf '\001Prince\nwar\n' > "$queue_corpus/text"
head -c 65537 /dev/zero > "$queue_corpus/long"
tar -czf "$out/initial-queue-corpus.tar.gz" -C "$queue_corpus" .
GREP_VECTOR_SEARCH=1 ./queue-fuzz -seed=20261026 -runs=5000 -max_len=131073 -timeout=10 "$queue_corpus" > "$out/queue-fuzz.log" 2>&1
tar -czf "$out/final-queue-corpus.tar.gz" -C "$queue_corpus" .
cp memory-copy.cpp original-copy.cpp
: > "$out/mutations.csv"
for mutation in block-bound block-stride store-lane tail rep-count; do
    cp original-copy.cpp memory-copy.cpp
    kind=1
    case "$mutation" in
      block-bound) sed -i 's/while (count >= 256)/while (count >= 32)/' memory-copy.cpp ;;
      block-stride) sed -i 's/count -= 256;/count -= 255;/' memory-copy.cpp ;;
      store-lane) sed -i 's/(d + 224), i)/(d + 192), i)/' memory-copy.cpp ;;
      tail) sed -i 's/if (count) std::memcpy(d, s, count);/if (count > 1) std::memcpy(d, s, count - 1);/' memory-copy.cpp ;;
      rep-count) sed -i '/asm volatile/i\    --count;' memory-copy.cpp; kind=2 ;;
    esac
    if cmp -s original-copy.cpp memory-copy.cpp; then echo 'Mutation failed to apply'; exit 1; fi
    clang++ "${flags[@]}" -O1 -DQUEUE_COPY_KIND="$kind" memory-copy.cpp copy-check.cpp -o mutant
    set +e
    (ulimit -c 0; timeout 30s ./mutant) > "$out/$mutation.log" 2>&1
    status=$?
    set -e
    printf '%s,%s\n' "$mutation" "$status" >> "$out/mutations.csv"
    if [[ "$status" == 0 || "$status" == 124 ]]; then echo 'Survivor/timeout'; exit 1; fi
done
cp original-copy.cpp memory-copy.cpp
# Memcheck observes inline assembly too; ASan cannot instrument rep movsb.
for kind in 1 2; do
    g++ "${flags[@]}" -O3 -DQUEUE_COPY_KIND="$kind" memory-copy.cpp copy-check.cpp -o "memcheck-$kind"
    valgrind --tool=memcheck --error-exitcode=99 --leak-check=full "./memcheck-$kind" > "$out/memcheck-$kind.log" 2>&1
    clang++ "${flags[@]}" -DQUEUE_COPY_KIND="$kind" --analyze memory-copy.cpp > "$out/static-analysis-$kind.log" 2>&1
done
if g++ "${flags[@]}" -DQUEUE_COPY_KIND=3 -c memory-copy.cpp -o invalid.o > "$out/invalid-kind.log" 2>&1; then
    echo 'Invalid copy kind compiled'; exit 1
fi
grep -q 'QUEUE_COPY_KIND must be' "$out/invalid-kind.log"
sha256sum original-copy.cpp read-ahead.cpp copy-check.cpp native-check.cpp > "$out/source-hashes.txt"
echo 'Copy/queue regression, protected boundaries, coverage, fuzz, mutation, Memcheck and analyzer checks passed.'
