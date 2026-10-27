#!/usr/bin/env bash
# Refresh clean gcov counters and retain the completed fuzz campaign corpus.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
work="$HOME/texttool-reader-verification"
test -d "$work" && test -d "$root/results"
cd "$work"
g++ -std=c++20 -D_POSIX_C_SOURCE=200809L -pthread -Wall -Wextra -Wpedantic \
    -Werror -g -O0 --coverage read-ahead.cpp native-check.cpp -o covered
for counters in covered-read-ahead.gcda covered-native-check.gcda; do
    if [[ -f "$counters" ]]; then rm -- "$counters"; fi
done
./covered > "$root/results/coverage-check.log"
gcov -b -c covered-read-ahead.gcno > "$root/results/gcov.log"
cp read-ahead.cpp.gcov "$root/results/read-ahead.cpp.gcov"
tar -czf "$root/results/fuzz-corpus.tar.gz" corpus
sha256sum original.cpp native-check.cpp > "$root/results/native-source-hashes.txt"
g++ --version > "$root/results/native-gcc.txt"
clang++ -std=c++20 -D_POSIX_C_SOURCE=200809L -pthread --analyze read-ahead.cpp \
    > "$root/results/static-analysis.log" 2>&1
echo 'Clean coverage, source and corpus evidence retained; static analysis completed.'
