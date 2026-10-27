#!/usr/bin/env bash
# Refresh current counters while preserving first-experiment evidence.
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
work="$HOME/texttool-reader-optimized-verification"
evidence="$root/results/optimized/native"
test -d "$work" && test -d "$evidence"
cd "$work"
g++ -std=c++20 -D_POSIX_C_SOURCE=200809L -march=native -mtune=native \
    -ftree-vectorize -pthread -Wall -Wextra -Wpedantic -Werror -g -O0 --coverage \
    read-ahead.cpp vector-search.cpp native-check.cpp vector-check.cpp -o covered
for counters in covered-*.gcda; do
    if [[ -f "$counters" ]]; then rm -- "$counters"; fi
done
GREP_VECTOR_SEARCH=1 ./covered > "$evidence/coverage-check.log"
GREP_VECTOR_SEARCH=0 ./covered >> "$evidence/coverage-check.log"
gcov -b -c covered-read-ahead.gcno covered-vector-search.gcno > "$evidence/gcov.log"
cp read-ahead.cpp.gcov vector-search.cpp.gcov "$evidence/"
clang++ -std=c++20 -D_POSIX_C_SOURCE=200809L -march=native -pthread --analyze \
    read-ahead.cpp vector-search.cpp > "$evidence/static-analysis.log" 2>&1
echo 'Current-source clean coverage refreshed; static analysis completed.'
