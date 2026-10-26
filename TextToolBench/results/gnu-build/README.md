# GNU build provenance

The installed files were matched by SHA-256 to packages downloaded from the official MSYS2 repository. This is stronger than inferring flags from a current Git for Windows recipe: its general MSYS2-packages fork contains different gawk/grep versions.

| Installed tool | Matching package | Recorded compiler | Recorded pacman build configuration |
|---|---|---|---|
| GNU awk 5.4.1 | gawk 5.4.1-1 x86_64 | GCC 15.3.0-1 | pacman 6.1.0-25 |
| GNU grep 3.0 | grep 1:3.0-7 x86_64 | GCC 13.3.0-1 | pacman 6.1.0-10 |

Both corresponding pacman packages contain these defaults:

```text
CFLAGS="-march=nocona -msahf -mtune=generic -O2 -pipe"
CXXFLAGS="-march=nocona -msahf -mtune=generic -O2 -pipe"
LDFLAGS=""
```

The matching package recipes do not override these optimization flags. They do not request `-O3`, native CPU tuning, LTO or PGO. `nocona` is an older x86-64 target with SSE3 capability; it is not an AVX2-specific build. Finding XMM instructions in the installed binary does not establish that a particular text-processing loop is vectorized.

These are verified package metadata, matching recipes and the build-tool configuration from the recorded pacman versions. Original build-machine `config.log` and a complete compiler invocation log were not shipped, so an external CI environment override cannot be ruled out from these files alone. The new controlled grep builds retain their complete configure/build logs separately in `../grep-tuned/`.

## Artifact identities

```text
Installed awk.exe / package usr/bin/awk.exe:
f123d2c120b2ad2158aa8c69d8eeedf14c0d681e68ee76ff103e06ac49986200
Installed grep.exe / package usr/bin/grep.exe:
497202418334f8a48d9f34cd2e1b2c03c4f957e02304b613e10931c0457e8d4a
gawk PKGBUILD SHA256 / gawk .BUILDINFO pkgbuild_sha256sum:
0635cd9b554764360b6d5d72af8d9ff415dcf2c05dbf5f9e4e6d7d9b17c2d8be
grep PKGBUILD SHA256 / grep .BUILDINFO pkgbuild_sha256sum:
106e3f99a885b7d3704574819d13602e28e4a469120ab45c1a0611a88e13f29b
```

The grep recipe matched commit `d9baf80602b1985c72ba7e7b4b89428c920f7bec` in `msys2/MSYS2-packages`. Its preparation sets `gl_cv_have_weak=no`, uses `MSYSTEM=CYGWIN`, runs autoreconf, and configures for the MSYS host without the libiconv/libintl prefix searches. The recipe explains that grep remains at 3.0 to retain Windows line-ending behavior needed by MSYS2 configure tests.

## Sources and metadata

- https://repo.msys2.org/msys/x86_64/gawk-5.4.1-1-x86_64.pkg.tar.zst
- https://repo.msys2.org/msys/x86_64/grep-1~3.0-7-x86_64.pkg.tar.zst
- https://repo.msys2.org/msys/x86_64/pacman-6.1.0-10-x86_64.pkg.tar.zst
- https://repo.msys2.org/msys/x86_64/pacman-6.1.0-25-x86_64.pkg.tar.zst
- https://raw.githubusercontent.com/msys2/MSYS2-packages/d9baf80602b1985c72ba7e7b4b89428c920f7bec/grep/PKGBUILD
- https://raw.githubusercontent.com/msys2/MSYS2-packages/master/gawk/PKGBUILD (saved recipe matched the package hash)
- https://ftp.gnu.org/gnu/gawk/gawk-5.4.1.tar.xz
- https://ftp.gnu.org/gnu/grep/grep-3.0.tar.xz

Original GNU source archives are retained in `upstream/` in the repository package. Extracted source trees and downloaded toolchains are reacquired by the Makefile rather than committed as build caches.
