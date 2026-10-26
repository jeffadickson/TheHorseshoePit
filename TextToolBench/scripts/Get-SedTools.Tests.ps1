# Offline installer tests: fake cached bytes are never executed as a program.
Describe 'Pinned sed downloads' {
    $script:retriever=Join-Path $PSScriptRoot 'Get-SedTools.ps1'
    $script:testRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'results/sed/acquisition-tests'
    $parent=Split-Path $script:testRoot -Parent
    if (!(Test-Path -LiteralPath $parent -PathType Container)) { throw 'Missing results directory' }
    if (!(Test-Path -LiteralPath $script:testRoot -PathType Container)) { New-Item -ItemType Directory -Path $script:testRoot | Out-Null }
    $cache=Join-Path $script:testRoot 'tools/sed-suite'
    if (!(Test-Path -LiteralPath $cache -PathType Container)) { New-Item -ItemType Directory -Path $cache -Force | Out-Null }
    $script:fixture=Join-Path $cache 'busybox.exe'
    $script:manifest=Join-Path $script:testRoot 'manifest.json'
    It 'accepts a cached file only when its digest matches' {
        [IO.File]::WriteAllBytes($script:fixture,[byte[]](0,1,2,255))
        @{Assets=@(@{Name='busybox';Archive=$false;URL='https://invalid.invalid/unreachable';Sha256=(Get-FileHash -LiteralPath $script:fixture).Hash})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $script:manifest
        {& $script:retriever -WorkspaceRoot $script:testRoot -ManifestPath $script:manifest} | Should Not Throw
    }
    It 'rejects a deliberate cached-byte mutation without downloading' {
        [IO.File]::WriteAllBytes($script:fixture,[byte[]](0,1,3,255))
        {& $script:retriever -WorkspaceRoot $script:testRoot -ManifestPath $script:manifest} | Should Throw
    }
    It 'rejects a nonexistent workspace' {
        {& $script:retriever -WorkspaceRoot (Join-Path $script:testRoot 'missing') -ManifestPath $script:manifest} | Should Throw
    }
}
