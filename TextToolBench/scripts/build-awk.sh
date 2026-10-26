#!/usr/bin/env bash
# Keep CPU-specific gawk isolated until validation and installation succeed.
set -euo pipefail
root=$(cygpath -u "${PROJECT_ROOT:?}")
export gl_cv_have_weak=no MSYSTEM=CYGWIN
mkdir -p "$root/tools/awk-build/native-o3" "$root/results/awk-tuned"
cd "$root/tools/awk-build/native-o3"
flags="-O3 -march=native -mtune=native -ftree-vectorize -pipe -fopt-info-vec-all=$PWD/vectorization.log"
printf '%s\n' "$flags" > "$root/results/awk-tuned/flags.txt"
gcc -v > "$root/results/awk-tuned/compiler.txt" 2>&1
# Match GCC's actual target; the unmodified release configure treats a literal
# "msys" host as unknown and silently disables dynamically loaded extensions.
CFLAGS="$flags" "$root/tools/gnu-source/extracted/gawk-5.4.1/configure" --build=x86_64-pc-cygwin --host=x86_64-pc-cygwin --prefix=/usr --libexecdir=/usr/lib --without-libiconv-prefix --without-libintl-prefix > "$root/results/awk-tuned/configure.log" 2>&1
# Object/libtool metadata from a different host configuration cannot be reused.
make clean > "$root/results/awk-tuned/clean.log" 2>&1
make -j4 > "$root/results/awk-tuned/build.log" 2>&1
# GCC 16.2's MSYS target emits a misaligned 256-bit stack store in minrx.c.
# Keep native tuning/vectorization, but constrain vector width in this one file.
printf '%s\n' 'support/minrx.c additionally uses -mprefer-vector-width=128 (verified stack-alignment workaround)' > "$root/results/awk-tuned/regex-flags.txt"
rm -f support/minrx.o
make -C support minrx.o CFLAGS="$flags -mprefer-vector-width=128" AM_CFLAGS="$flags -mprefer-vector-width=128" >> "$root/results/awk-tuned/build.log" 2>&1
make -j4 >> "$root/results/awk-tuned/build.log" 2>&1
cp config.log "$root/results/awk-tuned/config.log"
make -j4 check > "$root/results/awk-tuned/check.log" 2>&1
# Some GNU test recipes ignore make errors, so exit status alone is insufficient.
if grep -E 'Error [0-9]|differ:|FAILED' "$root/results/awk-tuned/check.log"; then
    echo 'Upstream test differences remain; installation is blocked.' >&2
    exit 1
fi
printf 'Upstream make check completed without reported differences.\n' > "$root/results/awk-tuned/upstream-status.txt"
sha256sum gawk.exe > "$root/results/awk-tuned/upstream-executable.sha256"
