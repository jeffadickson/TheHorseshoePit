# Native GNU default tools

The native build scripts use `-O3 -march=native -mtune=native -ftree-vectorize -pipe` with GCC 16.2.0. These binaries target this machine's CPU and must be rebuilt for a different processor. GNU awk source is 5.4.1; GNU grep source is 3.0. Configuration/build logs and GNU awk upstream test output are retained here; grep evidence is in `../grep-tuned/`.

`scripts/InstallTunedTools.ps1` installs isolated copies as `%LOCALAPPDATA%/Programs/TextToolBench/native-o3/bin/{awk,gawk,grep}.exe`, with the MSYS2 runtime DLLs beside them. It verifies output against the existing Git GNU tools before prepending this directory to the user PATH. It does not overwrite the Git executables. Existing applications may need restarting to inherit the new user PATH.

The previous user PATH is saved locally in `user-path-before-install.txt` for rollback. Remove the added directory from the user PATH to restore command lookup. Historical benchmarks remain associated with their recorded binary hashes; installation alone does not remeasure performance.

Verification uses a managed .NET 10 helper so Coverlet can instrument it; it is not Native AOT. The helper covers 40 deterministic/bounded randomized inputs (seed 20261106), including 8 KiB/131 KiB/262 KiB boundaries, blank records and missing final newline, five AWK operations and three grep patterns. Malformed AWK/regex and a deliberate AWK count mutation are checked. Helper coverage does not measure the native GNU source or the PowerShell installation script; no native source coverage is claimed.
## Installed and verified

Both tools are installed as user defaults. GNU awk 5.4.1 retains the requested -O3/native/vectorization flags; support/minrx.c additionally uses -mprefer-vector-width=128 after an observed misaligned 256-bit stack store caused regex crashes. Other source files retain native vector widths. The crashing instruction/backtrace and initial failure logs are retained as negative evidence. GNU grep 3.0 retains its previously verified native-O3 build.

The final verifier passed 338 checks (including three operations across all five corpus sizes), plus the separately executed installed intdiv extension check. Coverlet measured 37/37 lines (100%) and 30/42 branches (71.42%) in the managed verifier. No native GNU or PowerShell script coverage was measured. Manual semantic mutation detection ran; Stryker.NET was not run for this CLI verifier.

The upstream make -j4 check run is QUALIFIED, not a complete pass: three differences were reported (pty1, pty2, timeout). The timeout test passed a serial rerun. The two PTY cases remain unverified in this noninteractive host; the installed GNU control reported /dev/tty unavailable and hung until stopped. Charset tests were explicitly skipped because the full required locale set is unavailable. See upstream-status.txt, check.log and timeout-serial.log.

Standalone extension lookup uses the user AWKLIBPATH pointing at the installation's lib/gawk directory via /cygdrive. Both the previous user PATH and AWKLIBPATH are saved in user-environment-before-install.json. Existing applications must restart to inherit these registry environment settings. Historical benchmark measurements were not relabeled or rerun as native-awk timings.

Reproduction: run scripts/build-awk.sh in the configured local MSYS2 environment with PROJECT_ROOT set to the project directory, then scripts/InstallTunedTools.ps1 in PowerShell. The build script blocks on upstream differences; qualified PTY limitations require review and retained exact-binary evidence before installation. The current installed hashes are in installed-hashes.json.
