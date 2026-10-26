# Retrieve official Windows releases locally; never replace default commands.
# Mutable upstream URLs are guarded by the captured digest and fail closed if updated.
param(
    [string]$WorkspaceRoot=(Split-Path $PSScriptRoot -Parent),
    [string]$ManifestPath=(Join-Path $PSScriptRoot 'sed-dependencies.json')
)
$ErrorActionPreference='Stop'
$root=$WorkspaceRoot
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing workspace.' }
$tools=Join-Path $root 'tools'
if (!(Test-Path -LiteralPath $tools -PathType Container)) { New-Item -ItemType Directory -Path $tools | Out-Null }
$suite=Join-Path $tools 'sed-suite'
if (!(Test-Path -LiteralPath $suite -PathType Container)) { New-Item -ItemType Directory -Path $suite | Out-Null }
$manifest=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
foreach($asset in $manifest.Assets) {
    $folder=if($asset.Archive){Join-Path $suite $asset.Name}else{$suite}
    if (!(Test-Path -LiteralPath $folder -PathType Container)) { New-Item -ItemType Directory -Path $folder | Out-Null }
    $file=Join-Path $folder $(if($asset.Archive){'release.zip'}else{'busybox.exe'})
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
        $temporary=$file+'.download'
        try {
            Invoke-WebRequest -Uri $asset.URL -OutFile $temporary
            if((Get-FileHash -LiteralPath $temporary).Hash -ne $asset.Sha256){throw "Digest mismatch: $($asset.Name)"}
            Move-Item -LiteralPath $temporary -Destination $file
        } finally {if(Test-Path -LiteralPath $temporary -PathType Leaf){Remove-Item -LiteralPath $temporary}}
    }
    if((Get-FileHash -LiteralPath $file).Hash -ne $asset.Sha256){throw "Cached digest mismatch: $($asset.Name)"}
    if($asset.Archive){Expand-Archive -LiteralPath $file -DestinationPath $folder -Force}
}
Write-Output 'All four official sed-suite downloads match the pinned SHA-256 digests.'
