#!/usr/bin/env bash
# Build comparable generic and native variants; never replace Git's installed grep.
set -euo pipefail
root=$(cygpath -u "${PROJECT_ROOT:?Set PROJECT_ROOT to the TextToolBench directory}")
source_dir="$root/tools/gnu-source/extracted/grep-3.0"
mkdir -p "$root/tools/gnu-source/extracted" "$root/tools/grep-build" "$root/results/grep-tuned"
if [[ ! -f "$source_dir/configure" ]]; then
    tar -xf "$root/upstream/grep-3.0.tar.xz" -C "$root/tools/gnu-source/extracted"
fi
# Match MSYS2 grep 1:3.0-7's preparation and configure environment.
export gl_cv_have_weak=no MSYSTEM=CYGWIN
if [[ ! -f "$source_dir/.texttool-autoreconf" ]]; then
    (cd "$source_dir" && autoreconf -fi) >"$root/results/grep-tuned/autoreconf.log" 2>&1
    touch "$source_dir/.texttool-autoreconf"
fi
gcc -v >"$root/results/grep-tuned/compiler.txt" 2>&1
gcc -Q -march=native -mtune=native --help=target >"$root/results/grep-tuned/native-target.txt" 2>&1
pacman -Q >"$root/results/grep-tuned/msys-packages.txt"
for variant in generic-o2 native-o3; do
    build="$root/tools/grep-build/$variant"
    mkdir -p "$build"
    if [[ "$variant" == generic-o2 ]]; then
        flags='-O2 -march=nocona -msahf -mtune=generic -pipe'
    else
        flags="-O3 -march=native -mtune=native -ftree-vectorize -pipe -fopt-info-vec-all=$build/vectorization.log"
    fi
    printf '%s\n' "$flags" >"$root/results/grep-tuned/$variant.flags.txt"
    (
        cd "$build"
        CFLAGS="$flags" "$source_dir/configure" --build=x86_64-pc-msys --prefix=/usr \
            --without-libiconv-prefix --without-libintl-prefix
        make -j2
        ./src/grep.exe --version
    ) >"$root/results/grep-tuned/$variant.build.log" 2>&1
    cp "$build/config.log" "$root/results/grep-tuned/$variant.config.log"
    if [[ -f "$build/vectorization.log" ]]; then
        cp "$build/vectorization.log" "$root/results/grep-tuned/vectorization.log"
    fi
done
printf 'Both GNU grep 3.0 variants built successfully.\n'
