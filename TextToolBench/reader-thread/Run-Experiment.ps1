param(
    [Parameter(Mandatory=$true)][string]$BenchmarkRoot,
    [ValidateSet('Build','Test','Benchmark','NativeVerify','Requirements')][string]$Mode='Benchmark'
)
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $PSScriptRoot -PathType Container)) {throw 'Missing extension directory'}
if (!(Test-Path -LiteralPath $BenchmarkRoot -PathType Container)) {throw 'Missing benchmark directory'}
$BenchmarkRoot=(Resolve-Path -LiteralPath $BenchmarkRoot).Path
$results=Join-Path $PSScriptRoot 'results'
if (!(Test-Path -LiteralPath $results -PathType Container)) {New-Item -ItemType Directory -Path $results | Out-Null}
function Check-Exit {if($LASTEXITCODE -ne 0){throw "Command failed: exit $LASTEXITCODE"}}
switch($Mode){
    Build {
        # Source is the existing prepared release with this reviewed patch applied.
        $source=Join-Path $PSScriptRoot 'source'
        if (!(Test-Path -LiteralPath $source -PathType Container)) {
            $original=Join-Path $BenchmarkRoot 'tools/gnu-source/extracted/grep-3.0'
            if (!(Test-Path -LiteralPath $original -PathType Container)) {throw 'Prepare the pinned GNU source first'}
            Copy-Item -LiteralPath $original -Destination $source -Recurse
            git -C $source apply --check (Join-Path $PSScriptRoot 'grep.patch'); Check-Exit
            git -C $source apply (Join-Path $PSScriptRoot 'grep.patch'); Check-Exit
        }
        # Refuse to silently reuse a source overlay from an older patch.
        git -C $source apply --reverse --check (Join-Path $PSScriptRoot 'grep.patch'); Check-Exit
        $header=Join-Path $PSScriptRoot 'read-ahead.h'
        $sourceHeader=Join-Path $source 'src/read-ahead.h'
        if (!(Test-Path -LiteralPath $sourceHeader -PathType Leaf) -or
            (Get-FileHash -LiteralPath $header).Hash -ne (Get-FileHash -LiteralPath $sourceHeader).Hash) {
            Copy-Item -LiteralPath $header -Destination $sourceHeader
        }
        $env:BENCH_ROOT=$BenchmarkRoot; $env:EXPERIMENT_ROOT=$PSScriptRoot
        & (Join-Path $BenchmarkRoot 'tools/msys64/usr/bin/bash.exe') -lc 'bash "$(cygpath -u "$EXPERIMENT_ROOT")/build.sh"'; Check-Exit
        dotnet publish (Join-Path $PSScriptRoot 'Experiment.csproj') -c Release -r win-x64 -p:PublishAot=true -o (Join-Path $results 'native-harness'); Check-Exit
    }
    {$_ -in 'Test','Benchmark'} {
        $harness=Join-Path $results 'native-harness/Experiment.exe'
        if (!(Test-Path -LiteralPath $harness -PathType Leaf)) {throw 'Run Build first'}
        $arguments=@($PSScriptRoot,$BenchmarkRoot)
        if($Mode -eq 'Test'){$arguments+='--test'}
        & $harness @arguments; Check-Exit
    }
    NativeVerify {
        $linux=(& wsl -d Ubuntu-24.04 -- wslpath -u $PSScriptRoot.Replace('\','/')).Trim(); Check-Exit
        & wsl -d Ubuntu-24.04 -- bash "$linux/native-verify.sh"; Check-Exit
    }
    Requirements {
        python -c 'from strictdoc.cli.main import main; main()' export (Join-Path $PSScriptRoot 'requirements.sdoc') --output-dir (Join-Path $results 'requirements'); Check-Exit
    }
}
