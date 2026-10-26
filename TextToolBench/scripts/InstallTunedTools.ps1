# Install an isolated CPU-specific release; preserve the previous user PATH for rollback.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Project directory is missing.' }
foreach ($relative in @('tools/awk-build/native-o3','tools/awk-build/native-o3/extension/.libs','tools/grep-build/native-o3/src','tools/msys64/usr/bin','results/awk-tuned','scripts')) {
    if (!(Test-Path -LiteralPath (Join-Path $root $relative) -PathType Container)) { throw "Missing required directory: $relative" }
}
$testDigestPath = Join-Path $root 'results/awk-tuned/upstream-executable.sha256'
if (!(Test-Path -LiteralPath $testDigestPath -PathType Leaf)) { throw 'Missing successful upstream test evidence.' }
$testedDigest = ([IO.File]::ReadAllText($testDigestPath) -split '\s+')[0]
$currentDigest = (Get-FileHash -LiteralPath (Join-Path $root 'tools/awk-build/native-o3/gawk.exe')).Hash
if ($testedDigest -ne $currentDigest) { throw 'The awk executable differs from the upstream-tested binary.' }
$destination = Join-Path $env:LOCALAPPDATA 'Programs/TextToolBench/native-o3/bin'
if (!(Test-Path -LiteralPath $env:LOCALAPPDATA -PathType Container)) { throw 'Local application data directory is missing.' }
if (!(Test-Path -LiteralPath $destination -PathType Container)) {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $root 'tools/awk-build/native-o3/gawk.exe') -Destination (Join-Path $destination 'gawk.exe')
Copy-Item -LiteralPath (Join-Path $destination 'gawk.exe') -Destination (Join-Path $destination 'awk.exe')
Copy-Item -LiteralPath (Join-Path $root 'tools/grep-build/native-o3/src/grep.exe') -Destination (Join-Path $destination 'grep.exe')
# Keep runtime libraries beside these executables, without exposing the MSYS tool directory on PATH.
Get-ChildItem -LiteralPath (Join-Path $root 'tools/msys64/usr/bin') -Filter '*.dll' | Copy-Item -Destination $destination
# Support both conventional MSYS /usr and standalone runtime directory layouts.
$installRoot = Split-Path $destination -Parent
foreach ($relative in @('lib/gawk','usr/lib/gawk')) {
    $libraryDir = Join-Path $installRoot $relative
    if (!(Test-Path -LiteralPath $installRoot -PathType Container)) { throw 'Missing installation root.' }
    if (!(Test-Path -LiteralPath $libraryDir -PathType Container)) { New-Item -ItemType Directory -Path $libraryDir -Force | Out-Null }
    Get-ChildItem -LiteralPath (Join-Path $root 'tools/awk-build/native-o3/extension/.libs') -Filter '*.dll' | Copy-Item -Destination $libraryDir
}
Push-Location -LiteralPath $root
try {
    # MSBuild is incremental; this also prevents using a verifier older than its source.
    & dotnet build (Join-Path $PSScriptRoot 'VerifyTunedTools.csproj') -o (Join-Path $root 'tools/install-verification-project')
    if ($LASTEXITCODE -ne 0) { throw 'Verifier build failed; user PATH was not changed.' }
    # Use the previously compiled verifier rather than rebuilding on every installation.
    & dotnet (Join-Path $root 'tools/install-verification-project/VerifyTunedTools.dll') $destination
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed; user PATH was not changed.' }
    # GNU awk's compiled /usr search path belongs to MSYS; a standalone user
    # installation supplies its own extension path without changing the binary.
    $libraryPath = (Join-Path $installRoot 'lib/gawk').Replace('\','/')
    # A standalone runtime has no MSYS2 /etc/fstab: its drive mount is /cygdrive,
    # whereas cygpath in the development environment would incorrectly return /c.
    if ($libraryPath -match '^([A-Za-z]):(.*)$') { $libraryPath = '/cygdrive/' + $Matches[1].ToLowerInvariant() + $Matches[2] }
    $oldLibraryPath = [Environment]::GetEnvironmentVariable('AWKLIBPATH','User')
    $env:AWKLIBPATH = $libraryPath + $(if ($oldLibraryPath) { ':' + $oldLibraryPath } else { '' })
    $extensionResult = & (Join-Path $destination 'gawk.exe') -l intdiv 'BEGIN {intdiv(10,3,r); print r["quotient"],r["remainder"]}'
    if ($LASTEXITCODE -ne 0 -or $extensionResult -ne '3 1') { throw 'Extension verification failed; user PATH was not changed.' }
} finally { Pop-Location }
$oldPath = [Environment]::GetEnvironmentVariable('Path','User')
$backup = Join-Path $root 'results/awk-tuned/user-path-before-install.txt'
if (!(Test-Path -LiteralPath $backup)) { [IO.File]::WriteAllText($backup,$oldPath) }
$environmentBackup = Join-Path $root 'results/awk-tuned/user-environment-before-install.json'
if (!(Test-Path -LiteralPath $environmentBackup)) {
    @{ Path=$oldPath; AWKLIBPATH=$oldLibraryPath } | ConvertTo-Json | Set-Content -LiteralPath $environmentBackup
}
$entries = @($oldPath -split ';' | Where-Object { $_ -and $_ -ne $destination })
[Environment]::SetEnvironmentVariable('Path',(($destination + ';') + ($entries -join ';')),'User')
[Environment]::SetEnvironmentVariable('AWKLIBPATH',$env:AWKLIBPATH,'User')
$env:PATH = $destination + ';' + $env:PATH
Get-Command awk,gawk,grep | Select-Object Name,Source
Get-FileHash -LiteralPath (Join-Path $destination 'awk.exe'),(Join-Path $destination 'grep.exe') | ConvertTo-Json | Set-Content (Join-Path $root 'results/awk-tuned/installed-hashes.json')
