# Rebuild identical release sources with PDBs in separate target directories.
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    $env:RUSTUP_HOME=Join-Path $root 'tools/rustup';$env:CARGO_HOME=Join-Path $root 'tools/cargo';$env:PATH="$env:CARGO_HOME\bin;$env:PATH"
    $old=$env:CARGO_PROFILE_RELEASE_DEBUG
    $env:CARGO_PROFILE_RELEASE_DEBUG='2'
    try {
        & "$env:CARGO_HOME/bin/cargo.exe" build --release --locked --manifest-path tools/stefanalfbo-rawk/Cargo.toml -p rawk-cli --target-dir tools/profiling/stefan
        if($LASTEXITCODE -ne 0){throw 'rawk PDB build failed'}
        & "$env:CARGO_HOME/bin/cargo.exe" build --release --locked --manifest-path tools/quinnjr-rawk/Cargo.toml --target-dir tools/profiling/quinn
        if($LASTEXITCODE -ne 0){throw 'awk-rs PDB build failed'}
    }finally{$env:CARGO_PROFILE_RELEASE_DEBUG=$old}
    & "$env:CARGO_HOME/bin/rustc.exe" -C opt-level=3 -C debuginfo=2 scripts/profile-canary.rs -o tools/profiling/canary.exe
    if($LASTEXITCODE -ne 0){throw 'Profiler control build failed'}
}finally{Pop-Location}
