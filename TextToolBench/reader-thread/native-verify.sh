#!/usr/bin/env bash
# Native Linux sanitizer, source coverage, libFuzzer and real source mutations.
# Keep all compiler outputs on WSL's native filesystem.
set -euo pipefail
source_root=$(cd "$(dirname "$0")" && pwd)
test -d "$HOME" || exit 1
work="$HOME/texttool-reader-verification"
test -d "$work" || mkdir "$work"
test -d "$source_root/results" || mkdir "$source_root/results"
cp "$source_root"/read-ahead.{h,cpp} "$source_root/native-check.cpp" "$work/"
cd "$work"
clang++ --version > "$source_root/results/native-compiler.txt"
flags=(-std=c++20 -D_POSIX_C_SOURCE=200809L -pthread -Wall -Wextra -Wpedantic -Werror -g)
clang++ "${flags[@]}" -O1 -fsanitize=address,undefined \
    read-ahead.cpp native-check.cpp -o native-check
./native-check > "$source_root/results/native-check.log" 2>&1
g++ "${flags[@]}" -O0 --coverage read-ahead.cpp native-check.cpp -o covered
for counters in covered-read-ahead.gcda covered-native-check.gcda; do
    if [[ -f "$counters" ]]; then rm -- "$counters"; fi
done
./covered > "$source_root/results/coverage-check.log" 2>&1
gcov -b -c covered-read-ahead.gcno > "$source_root/results/gcov.log"
cp read-ahead.cpp.gcov "$source_root/results/read-ahead.cpp.gcov"
clang++ "${flags[@]}" -O1 -DLIBFUZZER -fsanitize=fuzzer,address,undefined \
    read-ahead.cpp native-check.cpp -o fuzz-reader
test -d corpus || mkdir corpus
printf '\001Prince\nwar\n' > corpus/text
head -c 65537 /dev/zero > corpus/boundary
./fuzz-reader -seed=20261026 -runs=5000 -max_len=131073 -timeout=10 corpus \
    > "$source_root/results/fuzz.log" 2>&1
# These five small mutations alter production logic, not command-line flags.
# Each must be caught by the unchanged byte/offset regression properties.
cp read-ahead.cpp original.cpp
: > "$source_root/results/mutations.csv"
for mutation in copy-count logical-offset eof-error seek-origin queue-advance; do
    cp original.cpp read-ahead.cpp
    case "$mutation" in
      copy-count) sed -i 's/std::memcpy(buffer, slot.bytes.data() + consumed_, bytes);/std::memcpy(buffer, slot.bytes.data() + consumed_, bytes - 1);/' read-ahead.cpp ;;
      logical-offset) sed -i 's/delivered_ += static_cast<off_t>(bytes);/delivered_ += static_cast<off_t>(bytes + 1);/' read-ahead.cpp ;;
      eof-error) sed -i 's/if (slot.error) {/if (true) {/' read-ahead.cpp ;;
      seek-origin) sed -i 's/::lseek(fd, offset, whence)/::lseek(fd, offset + 1, whence)/' read-ahead.cpp ;;
      queue-advance) sed -i 's/head_ = (head_ + 1) % slots_.size();/head_ = 0;/' read-ahead.cpp ;;
    esac
    if cmp -s original.cpp read-ahead.cpp; then echo "Mutation did not apply: $mutation"; exit 1; fi
    clang++ "${flags[@]}" -O1 read-ahead.cpp native-check.cpp -o "mutant-$mutation"
    set +e
    (ulimit -c 0; timeout 20s "./mutant-$mutation") > "$source_root/results/mutant-$mutation.log" 2>&1
    status=$?
    set -e
    printf '%s,%s\n' "$mutation" "$status" >> "$source_root/results/mutations.csv"
    if [[ "$status" == 0 || "$status" == 124 ]]; then echo "Survivor or timeout: $mutation"; exit 1; fi
done
cp original.cpp read-ahead.cpp
tar -czf "$source_root/results/fuzz-corpus.tar.gz" -C "$work" corpus
sha256sum original.cpp native-check.cpp > "$source_root/results/native-source-hashes.txt"
echo 'Native properties, sanitizers, fuzzing, coverage and five source mutants completed.'
