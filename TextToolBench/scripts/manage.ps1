param(
    [ValidateSet('help','bootstrap','deps','source','data','native','build','test','coverage','profile','grep-tuned','benchmark','report','requirements')]
    [string]$Action='help',
    [switch]$LibraryOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$script:Root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:Lock=Get-Content -LiteralPath (Join-Path $script:Root 'dependencies.json') -Raw | ConvertFrom-Json

function Invoke-Checked {
    param([Parameter(Mandatory)][string]$Program,[string[]]$Arguments=@())
    # Use argument arrays: paths or AWK programs must never become shell source.
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed with exit code $LASTEXITCODE" }
}
function Assert-Digest {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Sha256)
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw "SHA-256 mismatch: $Path" }
}
function Get-VerifiedFile {
    param([Parameter(Mandatory)]$Item)
    $path=Join-Path $script:Root $Item.path
    if (Test-Path -LiteralPath $path) { Assert-Digest $path $Item.sha256; return }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
    $temporary="$path.$PID.download"
    try {
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $Item.url -OutFile $temporary
        Assert-Digest $temporary $Item.sha256
        Move-Item -LiteralPath $temporary -Destination $path
    } finally { if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary} }
}
function Set-BenchEnvironment {
    # All changes are process-scoped. No user PATH or compiler settings are overwritten.
    $env:RUSTUP_HOME=Join-Path $script:Root 'tools/rustup'
    $env:RUSTUP_TOOLCHAIN=$script:Lock.rustToolchain
    $env:CARGO_HOME=Join-Path $script:Root 'tools/cargo'
    $env:PATH="$env:CARGO_HOME\bin;$env:PATH"
    $pwsh=Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if($pwsh){$env:BENCH_PWSH=$pwsh.Source}
    $git=Get-Command git.exe -ErrorAction SilentlyContinue
    if($git){
        $gitRoot=Split-Path (Split-Path $git.Source)
        $gnu=Join-Path $gitRoot 'usr/bin'
        if(Test-Path (Join-Path $gnu 'awk.exe')){$env:BENCH_GNU_BIN=$gnu}
    }
    $rgRoot=Join-Path $env:LOCALAPPDATA 'Microsoft/WinGet/Packages/BurntSushi.ripgrep.MSVC_Microsoft.Winget.Source_8wekyb3d8bbwe'
    if(Test-Path $rgRoot){$rg=@(Get-ChildItem -LiteralPath $rgRoot -Recurse -Filter rg.exe);if($rg.Count -eq 1){$env:BENCH_RG=$rg[0].FullName}}
}
function Install-WingetPackage {
    param([string]$Id,[string[]]$Extra=@())
    Invoke-Checked winget.exe (@('install','--id',$Id,'--exact','--accept-source-agreements','--accept-package-agreements','--disable-interactivity')+$Extra)
}
function Ensure-Dependencies {
    if(-not(Get-Command dotnet.exe -ErrorAction SilentlyContinue)){Install-WingetPackage Microsoft.DotNet.SDK.10}
    if(-not(Get-Command git.exe -ErrorAction SilentlyContinue)){Install-WingetPackage Git.Git}
    if(-not(Get-Command pwsh.exe -ErrorAction SilentlyContinue)){Install-WingetPackage Microsoft.PowerShell}
    $rgRoot=Join-Path $env:LOCALAPPDATA 'Microsoft/WinGet/Packages/BurntSushi.ripgrep.MSVC_Microsoft.Winget.Source_8wekyb3d8bbwe'
    if(-not(Test-Path $rgRoot)){Install-WingetPackage BurntSushi.ripgrep.MSVC}
    if(-not(Get-Command python.exe -ErrorAction SilentlyContinue)){Install-WingetPackage Python.Python.3.13}
    # New installers update registry PATH, not this process. Include normal installation roots.
    foreach($p in @("$env:ProgramFiles\dotnet","$env:ProgramFiles\Git\cmd","$env:ProgramFiles\PowerShell\7","$env:LOCALAPPDATA\Programs\Python\Python313")){if(Test-Path $p){$env:PATH="$p;$env:PATH"}}
    Set-BenchEnvironment
    $vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs=@();if(Test-Path $vswhere){$vs=@(& $vswhere -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath)}
    if($vs.Count -eq 0){Install-WingetPackage Microsoft.VisualStudio.2022.BuildTools @('--override','--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended')}
    if(-not(Test-Path "$env:CARGO_HOME\bin\rustup.exe")){
        New-Item -ItemType Directory -Path "$script:Root/tools/downloads" -Force | Out-Null
        Invoke-WebRequest -UseBasicParsing 'https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe' -OutFile "$script:Root/tools/downloads/rustup-init.exe"
        Invoke-Checked "$script:Root/tools/downloads/rustup-init.exe" @('-y','--no-modify-path','--profile','minimal','--default-toolchain',$script:Lock.rustToolchain)
    }
    Invoke-Checked "$env:CARGO_HOME/bin/rustup.exe" @('toolchain','install',$script:Lock.rustToolchain,'--profile','minimal')
    if(-not(Test-Path "$script:Root/tools/dotnet-tools/coverlet.exe")){Invoke-Checked dotnet.exe @('tool','install','coverlet.console','--version',$script:Lock.coverlet,'--tool-path',"$script:Root/tools/dotnet-tools")}
    & python.exe -c 'import strictdoc' 2>$null
    if($LASTEXITCODE -ne 0){Invoke-Checked python.exe @('-m','pip','install','--user',"strictdoc==$($script:Lock.strictdoc)")}
}
function Get-NativeSources {
    foreach($repo in $script:Lock.repositories){
        $path=Join-Path $script:Root $repo.path
        if(-not(Test-Path (Join-Path $path '.git'))){Invoke-Checked git.exe @('clone',$repo.url,$path);Invoke-Checked git.exe @('-C',$path,'checkout','--detach',$repo.commit)}
        $head=(& git.exe -C $path rev-parse HEAD).Trim();if($head -ne $repo.commit){throw "Unexpected source commit in $path; refusing to reset it"}
    }
    # Upstream frawk's checked-in lock is stale for its current manifest. Retain the resolved lock.
    Copy-Item -LiteralPath "$script:Root/dependency-locks/frawk.Cargo.lock" -Destination "$script:Root/tools/frawk/Cargo.lock" -Force
}
function Build-Native {
    Get-NativeSources
    $cargo="$env:CARGO_HOME/bin/cargo.exe"
    Invoke-Checked $cargo @('build','--release','--locked','--manifest-path','tools/stefanalfbo-rawk/Cargo.toml','-p','rawk-cli')
    Invoke-Checked $cargo @('build','--release','--locked','--manifest-path','tools/quinnjr-rawk/Cargo.toml')
    Invoke-Checked $cargo @('build','--release','--locked','--no-default-features','--manifest-path','tools/frawk/Cargo.toml')
    if(-not(Test-Path tools/mawk/bin/mawk.exe)){
        if(!(Test-Path -LiteralPath tools/mawk -PathType Container)){New-Item -ItemType Directory -Path tools/mawk -Force | Out-Null}
        # Refuse a silently updated release from the author's mutable download URL.
        Get-VerifiedFile ($script:Lock.archives | Where-Object path -eq 'tools/mawk/mawk.zip')
        Expand-Archive -LiteralPath tools/mawk/mawk.zip -DestinationPath tools/mawk -Force
    }
    Invoke-Checked powershell.exe @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/prepare-variants.ps1')
    Invoke-Checked $cargo @('build','--release','--locked','--manifest-path','tools/variants/stefan-buffered/Cargo.toml','-p','rawk-cli')
    Invoke-Checked $cargo @('build','--release','--locked','--manifest-path','tools/variants/quinn-buffered/Cargo.toml')
}
function Build-Harnesses {
    foreach($project in Get-ChildItem -LiteralPath $script:Root -Filter '*.csproj') {Invoke-Checked dotnet.exe @('build',$project.FullName,'-c','Release')}
}
function Run-Dll {
    param([string]$Name,[string[]]$Arguments=@())
    Invoke-Checked dotnet.exe (@("bin/Release/net10.0/$Name.dll")+$Arguments)
}
function Run-Verification {
    param([switch]$Coverage)
    foreach($case in @(@('Bench','--test'),@('Supplement','--test'),@('RipgrepBench','--small','--test'),@('RawkBench','--test'),@('AwkExtra','--test'),@('AwkExtra','--buffering','--test'))){
        $name=$case[0];$arguments=@($case | Select-Object -Skip 1)
        if($Coverage){
            New-Item -ItemType Directory -Path results/package-coverage -Force | Out-Null
            $assembly="bin/Release/net10.0/$name.dll"
            Invoke-Checked "$script:Root/tools/dotnet-tools/coverlet.exe" @($assembly,'--target','dotnet','--targetargs',("$assembly "+($arguments -join ' ')),'--include-test-assembly','--include',"[$name]*",'--format','cobertura','--output',"results/package-coverage/$name-$($arguments -join '-').xml")
        }else{Run-Dll $name $arguments}
    }
}
function Prepare-Msys {
    if(-not(Test-Path tools/msys64/usr/bin/bash.exe)){
        Get-VerifiedFile $script:Lock.archives[2]
        Invoke-Checked "$env:SystemRoot/System32/tar.exe" @('-xf',$script:Lock.archives[2].path,'-C','tools')
    }
    $env:MSYSTEM='MSYS';$env:CHERE_INVOKING='1';$env:PROJECT_ROOT=$script:Root
    Invoke-Checked tools/msys64/usr/bin/bash.exe @('-lc','pacman --noconfirm -Sy && pacman --noconfirm -S --needed gcc make autoconf automake libtool gettext-devel libiconv-devel pcre-devel diffutils patch pkgconf')
}
if($LibraryOnly){return}
Push-Location $script:Root
try {
    Set-BenchEnvironment
    switch($Action){
        help { Write-Output 'make deps source native build test coverage profile grep-tuned benchmark report requirements all';Write-Output 'make diff-build diff diff-test diff-coverage diff-mutation diff-report';Write-Output 'Windows x64; install GNU make first with: powershell -File scripts/manage.ps1 -Action bootstrap';Write-Output 'make all reruns the full benchmark and can take tens of minutes. make report uses retained measurements.' }
        bootstrap { Install-WingetPackage GnuWin32.Make;Write-Output 'Restart your shell or invoke C:\Program Files (x86)\GnuWin32\bin\make.exe' }
        deps { Ensure-Dependencies }
        source { Get-VerifiedFile $script:Lock.source;foreach($item in $script:Lock.archives | Select-Object -First 2){Get-VerifiedFile $item} }
        build { Build-Harnesses }
        data {
            Run-Dll Bench @('--prepare')
            $text=[IO.File]::ReadAllText("$script:Root/data/1MiB.txt");$first=$text.IndexOf('Prince',[StringComparison]::Ordinal);$offset=$text.LastIndexOf("`n",[Math]::Max(0,$first-256))+1
            foreach($kib in @(1,64)){$end=$text.IndexOf("`n",$offset+$kib*1024);[IO.File]::WriteAllText("$script:Root/data/small-${kib}KiB.txt",$text.Substring($offset,$end+1-$offset),[Text.UTF8Encoding]::new($false))}
        }
        native { Build-Native }
        test { Run-Verification }
        coverage { Run-Verification -Coverage }
        profile { Invoke-Checked powershell.exe @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/profile.ps1');Run-Dll NativeProfile }
        grep-tuned { Prepare-Msys;Invoke-Checked tools/msys64/usr/bin/bash.exe @('-lc','bash "$(cygpath -u "$PROJECT_ROOT")/scripts/build-grep.sh"') }
        benchmark { foreach($name in @('Bench','Supplement','RipgrepBench')){Run-Dll $name};Run-Dll RipgrepBench @('--small');Run-Dll RawkBench;Run-Dll AwkExtra;Run-Dll AwkExtra @('--buffering');Run-Dll GrepBuildBench }
        report { Run-Dll Analyze }
        requirements { Invoke-Checked python.exe @('-c','from strictdoc.cli.main import main; main()','export','requirements.sdoc','--output-dir','results/requirements') }
    }
}finally{Pop-Location}
