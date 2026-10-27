param([Parameter(Mandatory=$true)][string]$BenchmarkRoot)
# Capture actual files used by this finite experiment; a dirty parent checkout
# is permitted and explicitly recorded rather than treated as a clean revision.
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $PSScriptRoot -PathType Container)){throw 'Missing extension'}
if (!(Test-Path -LiteralPath $BenchmarkRoot -PathType Container)){throw 'Missing benchmark workspace'}
$out=Join-Path $PSScriptRoot 'results/copy'
if (!(Test-Path -LiteralPath $out -PathType Container)){throw 'Build and measure first'}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!(Test-Path -LiteralPath $repo -PathType Container)){throw 'Missing repository'}
$files=@('memory-copy.cpp','memory-copy.h','read-ahead.cpp','read-ahead.h','queue-profile.h',
    'vector-search.cpp','vector-search.h','copy-check.cpp','copy-kernel.cpp','native-check.cpp','vector-check.cpp',
    'Experiment.cs','ProfileReport.cs','ExperimentTests.cs','Experiment.csproj','Experiment.Tests.csproj','Directory.Build.props',
    'build-copy.sh','verify-copy.sh','Run-Experiment.ps1','Verify-Harness.ps1','Collect-CopyEvidence.ps1',
    'requirements.sdoc','stryker-config.json','copy-memcpy.exe','copy-avx2.exe','copy-rep.exe',
    'copy-memcpy-profile.exe','copy-avx2-profile.exe','copy-rep-profile.exe','copy-kernel.exe',
    'build/grep-vector.o','build/kwset-vector.o','vector.o')
$hashes=foreach($name in $files){
    $file=Join-Path $PSScriptRoot $name
    if (!(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing evidence input: $name"}
    [pscustomobject]@{File=$name;Bytes=(Get-Item -LiteralPath $file).Length;Sha256=(Get-FileHash -LiteralPath $file).Hash}
}
$dependencies=foreach($name in @('data/32MiB.txt','tools/msys64/usr/bin/msys-2.0.dll',
    'tools/msys64/usr/bin/g++.exe','tools/msys64/usr/bin/gcc.exe','tools/grep-build/native-o3/lib/libgreputils.a',
    'tools/grep-build/native-o3/src/dfasearch.o','tools/grep-build/native-o3/src/kwsearch.o',
    'tools/grep-build/native-o3/src/pcresearch.o','tools/grep-build/native-o3/src/searchutils.o')){
    $file=Join-Path $BenchmarkRoot $name
    if (!(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing dependency: $name"}
    [pscustomobject]@{File="benchmark/$name";Bytes=(Get-Item -LiteralPath $file).Length;Sha256=(Get-FileHash -LiteralPath $file).Hash}
}
@($hashes)+@($dependencies)|Export-Csv -LiteralPath (Join-Path $out 'hashes.csv') -NoTypeInformation
git -C $repo rev-parse HEAD > (Join-Path $out 'base-revision.txt')
if($LASTEXITCODE -ne 0){throw 'Git revision failed'}
git -C $repo status --short > (Join-Path $out 'checkout-status.txt')
if($LASTEXITCODE -ne 0){throw 'Git status failed'}
dotnet --info > (Join-Path $out 'dotnet-info.txt')
if($LASTEXITCODE -ne 0){throw 'SDK query failed'}
Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors |
    ConvertTo-Json > (Join-Path $out 'cpu.json')
$tests=Join-Path $out 'harness-tests'
if (Test-Path -LiteralPath $tests -PathType Container){
    Copy-Item -LiteralPath (Join-Path $tests 'harness.trx') -Destination (Join-Path $out 'harness.trx')
    $coverage=@(Get-ChildItem -LiteralPath $tests -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTime -Descending)
    if ($coverage.Count -eq 0){throw 'Missing unit coverage'}
    Copy-Item -LiteralPath $coverage[0].FullName -Destination (Join-Path $out 'harness-unit.cobertura.xml')
}
$mutation=Join-Path $out 'stryker/reports/mutation-report.json'
if (Test-Path -LiteralPath $mutation -PathType Leaf){
    Copy-Item -LiteralPath $mutation -Destination (Join-Path $out 'mutation-report.json')
}
Write-Output 'Captured source, binary and tool provenance; historical cohorts preserved.'
