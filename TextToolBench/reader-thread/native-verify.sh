#!/usr/bin/env bash
# Native Linux sanitizer, source coverage, libFuzzer and real source mutations.
# Keep all compiler outputs on WSL's native filesystem.
set -euo pipefail
source_root=$(cd "$(dirname "$0")" && pwd)
test -d "$HOME" || exit 1
work="$HOME/texttool-reader-optimized-verification"
test -d "$work" || mkdir "$work"
evidence="$source_root/results/optimized/native"
test -d "$source_root/results/optimized"
test -d "$evidence" || mkdir "$evidence"
cp "$source_root"/read-ahead.{h,cpp} "$source_root"/vector-search.{h,cpp} "$source_root/queue-profile.h" "$source_root/native-check.cpp" "$source_root/vector-check.cpp" "$work/"
cd "$work"
clang++ --version > "$evidence/native-compiler.txt"
flags=(-std=c++20 -D_POSIX_C_SOURCE=200809L -march=native -mtune=native -ftree-vectorize -pthread -Wall -Wextra -Wpedantic -Werror -g)
clang++ "${flags[@]}" -O1 -fsanitize=address,undefined \
    read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o native-check
GREP_VECTOR_SEARCH=1 ./native-check > "$evidence/native-check.log" 2>&1
GREP_VECTOR_SEARCH=0 ./native-check > "$evidence/scalar-check.log" 2>&1
g++ "${flags[@]}" -O0 --coverage read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o covered
for counters in covered-read-ahead.gcda covered-native-check.gcda covered-vector-search.gcda covered-vector-check.gcda; do
    if [[ -f "$counters" ]]; then rm -- "$counters"; fi
done
GREP_VECTOR_SEARCH=1 ./covered > "$evidence/coverage-check.log" 2>&1
GREP_VECTOR_SEARCH=0 ./covered >> "$evidence/coverage-check.log" 2>&1
gcov -b -c covered-read-ahead.gcno covered-vector-search.gcno > "$evidence/gcov.log"
cp read-ahead.cpp.gcov vector-search.cpp.gcov "$evidence/"
clang++ "${flags[@]}" -O1 -DLIBFUZZER -fsanitize=fuzzer,address,undefined \
    read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o fuzz-reader
test -d corpus || mkdir corpus
printf '\001Prince\nwar\n' > corpus/text
head -c 65537 /dev/zero > corpus/boundary
tar -czf "$evidence/fuzz-initial-corpus.tar.gz" corpus
GREP_VECTOR_SEARCH=1 ./fuzz-reader -seed=20261026 -runs=5000 -max_len=131073 -timeout=10 corpus \
    > "$evidence/fuzz.log" 2>&1
# These five small mutations alter production logic, not command-line flags.
# Each must be caught by the unchanged byte/offset regression properties.
cp read-ahead.cpp original.cpp
: > "$evidence/mutations.csv"
for mutation in copy-count logical-offset eof-error seek-origin queue-advance; do
    cp original.cpp read-ahead.cpp
    case "$mutation" in
      copy-count) sed -i 's/std::memcpy(buffer, slot.bytes.get() + consumed_, bytes);/std::memcpy(buffer, slot.bytes.get() + consumed_, bytes - 1);/' read-ahead.cpp ;;
      logical-offset) sed -i 's/delivered_ += static_cast<off_t>(bytes);/delivered_ += static_cast<off_t>(bytes + 1);/' read-ahead.cpp ;;
      eof-error) sed -i 's/if (slot.error) {/if (true) {/' read-ahead.cpp ;;
      seek-origin) sed -i 's/::lseek(fd, offset, whence)/::lseek(fd, offset + 1, whence)/' read-ahead.cpp ;;
      queue-advance) sed -i 's/if (++head_ == slots_.size()) head_ = 0;/head_ = 0;/' read-ahead.cpp ;;
    esac
    if cmp -s original.cpp read-ahead.cpp; then echo "Mutation did not apply: $mutation"; exit 1; fi
    clang++ "${flags[@]}" -O1 read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o "mutant-$mutation"
    set +e
    (ulimit -c 0; GREP_VECTOR_SEARCH=1 timeout 20s "./mutant-$mutation") > "$evidence/mutant-$mutation.log" 2>&1
    status=$?
    set -e
    printf '%s,%s\n' "$mutation" "$status" >> "$evidence/mutations.csv"
    if [[ "$status" == 0 || "$status" == 124 ]]; then echo "Survivor or timeout: $mutation"; exit 1; fi
done
cp original.cpp read-ahead.cpp
cp vector-search.cpp original-vector.cpp
for mutation in vector-first-match vector-tail-bound vector-second-byte; do
    cp original-vector.cpp vector-search.cpp
    case "$mutation" in
      vector-first-match) sed -i 's/std::countr_zero(mask)/std::countl_zero(mask)/' vector-search.cpp ;;
      vector-tail-bound) sed -i 's/while (count >= 32)/while (count > 0)/' vector-search.cpp ;;
      vector-second-byte) sed -i 's/_mm256_cmpeq_epi8(bytes, vb)/_mm256_cmpeq_epi8(bytes, va)/' vector-search.cpp ;;
    esac
    if cmp -s original-vector.cpp vector-search.cpp; then echo "Mutation did not apply"; exit 1; fi
    # Removing use of vb in a mutant can trigger an unused-variable warning;
    # retain diagnostics but do not mislabel that as a behavioral kill.
    clang++ "${flags[@]}" -Wno-error=unused-variable -O1 read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o "mutant-$mutation"
    set +e
    (ulimit -c 0; GREP_VECTOR_SEARCH=1 timeout 20s "./mutant-$mutation") > "$evidence/mutant-$mutation.log" 2>&1
    status=$?
    set -e
    printf '%s,%s\n' "$mutation" "$status" >> "$evidence/mutations.csv"
    if [[ "$status" == 0 || "$status" == 124 ]]; then echo "Survivor or timeout: $mutation"; exit 1; fi
done
cp original-vector.cpp vector-search.cpp
# Memcheck complements ASan by detecting reads of uninitialized queue storage.
g++ "${flags[@]}" -O3 read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o memcheck-reader
GREP_VECTOR_SEARCH=1 valgrind --tool=memcheck --error-exitcode=99 --leak-check=full --track-origins=yes \
    ./memcheck-reader > "$evidence/memcheck.log" 2>&1
# Exercise the diagnostic-only branch separately; these timings are not benchmarks.
g++ "${flags[@]}" -O0 --coverage -DQUEUE_PROFILE read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o profile-covered
for counters in profile-covered-*.gcda; do
    if [[ -f "$counters" ]]; then rm -- "$counters"; fi
done
GREP_VECTOR_SEARCH=1 ./profile-covered > "$evidence/profile-check.log" 2>&1
gcov -b -c profile-covered-read-ahead.gcno > "$evidence/profile-gcov.log"
cp queue-profile.h.gcov "$evidence/"
tar -czf "$evidence/fuzz-corpus.tar.gz" -C "$work" corpus
sha256sum original.cpp vector-search.cpp queue-profile.h native-check.cpp vector-check.cpp > "$evidence/native-source-hashes.txt"
echo 'Native properties, sanitizers, Memcheck, fuzzing, coverage and eight source faults completed.'
