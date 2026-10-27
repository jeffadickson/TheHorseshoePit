# Acquire pinned upstream sources and official binaries; fail closed on digest mismatch.
param([switch]$Build)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!(Test-Path -LiteralPath $root -PathType Container)){throw 'Missing checkout'}
foreach($relative in @('tools','upstream','results','tools/diff-suite','results/diff')){
    $folder=Join-Path $root $relative
    if(!(Test-Path -LiteralPath (Split-Path $folder -Parent) -PathType Container)){throw "Missing parent of $folder"}
    if(!(Test-Path -LiteralPath $folder -PathType Container)){New-Item -ItemType Directory -Path $folder|Out-Null}
}
$items=@(
    @{File='diffutils-3.12.tar.xz';URL='https://ftp.gnu.org/gnu/diffutils/diffutils-3.12.tar.xz';SHA='7C8B7F9FC8609141FDEA9CECE85249D308624391FF61DEDAF528FCB337727DFD'},
    @{File='uutils-source.tar.gz';URL='https://github.com/uutils/diffutils/releases/download/v0.6.0/source.tar.gz';SHA='048F8A48B8C186A2923C6145F3659A50154A7CEFA4BC5071537DA92BF9BA6649'},
    @{File='uutils-diffutils-x86_64-pc-windows-msvc.zip';URL='https://github.com/uutils/diffutils/releases/download/v0.6.0/diffutils-x86_64-pc-windows-msvc.zip';SHA='69063AB317B737314AC509A1DB0EDEE4BE0B75892AC098969104103D9748E413'}
)
foreach($item in $items){
    $file=Join-Path $root ('upstream/'+$item.File)
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){
        $temporary=$file+'.download'
        try{Invoke-WebRequest $item.URL -UseBasicParsing -OutFile $temporary;if((Get-FileHash -LiteralPath $temporary).Hash -ne $item.SHA){throw 'Downloaded digest mismatch'};Move-Item -LiteralPath $temporary -Destination $file}
        finally{if(Test-Path -LiteralPath $temporary -PathType Leaf){Remove-Item -LiteralPath $temporary}}
    }
    if((Get-FileHash -LiteralPath $file).Hash -ne $item.SHA){throw "Cached digest mismatch: $file"}
}
function Invoke-Checked([string]$Program,[string[]]$Arguments){& $Program @Arguments;if($LASTEXITCODE -ne 0){throw "$Program exited $LASTEXITCODE"}}
Push-Location -LiteralPath $root
try{
    $official='tools/diff-suite/uutils-official'
    if(!(Test-Path -LiteralPath $official -PathType Container)){New-Item -ItemType Directory -Path $official|Out-Null}
    if(!(Test-Path -LiteralPath ($official+'/diffutils.exe') -PathType Leaf)){Expand-Archive -LiteralPath upstream/uutils-diffutils-x86_64-pc-windows-msvc.zip -DestinationPath $official}
    if(!(Test-Path -LiteralPath tools/diff-suite/diffutils-0.6.0 -PathType Container)){Invoke-Checked tar @('-xf','upstream/uutils-source.tar.gz','-C','tools/diff-suite')}
    & (Join-Path $PSScriptRoot 'Get-SedTools.ps1') # BusyBox is shared with the sed suite.
    if($Build){
        foreach($required in @('tools/msys64/usr/bin','tools/rustup','tools/cargo')){if(!(Test-Path -LiteralPath $required -PathType Container)){throw "Run make deps/native first: missing $required"}}
        $env:PROJECT_ROOT=$root
        Invoke-Checked (Join-Path $root 'tools/msys64/usr/bin/bash.exe') @('scripts/build-diff.sh')
        $env:CARGO_HOME=Join-Path $root 'tools/cargo';$env:RUSTUP_HOME=Join-Path $root 'tools/rustup'
        $savedFlags=$env:RUSTFLAGS;$savedTarget=$env:CARGO_TARGET_DIR;$savedPath=$env:PATH
        $env:PATH=(Join-Path $env:CARGO_HOME 'bin')+';'+$env:PATH
        $env:CARGO_BUILD_JOBS='4'
        try{
            foreach($variant in @('generic','native')){
                $env:RUSTFLAGS=if($variant -eq 'native'){'-C target-cpu=native'}else{''}
                $env:CARGO_TARGET_DIR=Join-Path $root ('tools/diff-suite/rust-'+$variant)
                Invoke-Checked (Join-Path $root 'tools/cargo/bin/cargo.exe') @('build','--release','--locked','--manifest-path','tools/diff-suite/diffutils-0.6.0/Cargo.toml')
            }
        }finally{$env:RUSTFLAGS=$savedFlags;$env:CARGO_TARGET_DIR=$savedTarget;$env:PATH=$savedPath}
    }
}finally{Pop-Location}
