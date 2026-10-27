# Keep setup, instrumented verification and timing as separate invocations.
param([ValidateSet('Build','Benchmark','Test','Coverage','Mutation','Report')][string]$Mode='Benchmark')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($path in @($root,(Join-Path $root 'scripts'),(Join-Path $root 'results'))){if(!(Test-Path -LiteralPath $path -PathType Container)){throw "Missing $path"}}
function Invoke-Checked([string]$Program,[string[]]$Arguments){& $Program @Arguments;if($LASTEXITCODE -ne 0){throw "$Program exited $LASTEXITCODE"}}
function Find-DotnetTool([string]$Folder,[string]$Executable,[string]$Package,[string]$Version){
    $tools=Join-Path $root 'tools'
    if(!(Test-Path -LiteralPath $tools -PathType Container)){New-Item -ItemType Directory -Path $tools|Out-Null}
    $destination=Join-Path $tools $Folder
    $program=Join-Path $destination $Executable
    if(!(Test-Path -LiteralPath $destination -PathType Container)){New-Item -ItemType Directory -Path $destination|Out-Null}
    if(!(Test-Path -LiteralPath $program -PathType Leaf)){Invoke-Checked dotnet @('tool','install',$Package,'--tool-path',$destination,'--version',$Version)|Out-Null}
    return $program
}
Push-Location -LiteralPath $root
try{
    if($Mode -eq 'Build'){& (Join-Path $PSScriptRoot 'Get-DiffTools.ps1') -Build;return}
    Invoke-Checked dotnet @('build','DiffBench.csproj','-c','Release')
    switch($Mode){
        'Benchmark'{Invoke-Checked dotnet @('bin/Release/net10.0/DiffBench.dll')}
        'Report'{Invoke-Checked dotnet @('bin/Release/net10.0/DiffBench.dll','--report')}
        'Test'{Invoke-Checked dotnet @('bin/Release/net10.0/DiffBench.dll','--test');Invoke-Checked dotnet @('test','DiffBench.Tests/DiffBench.Tests.csproj','--collect:XPlat Code Coverage','--results-directory','results/diff/unit-tests')}
        'Coverage'{
            $tool=Find-DotnetTool 'coverage' 'coverlet.exe' 'coverlet.console' '10.1.0'
            Invoke-Checked $tool @('bin/Release/net10.0/DiffBench.dll','--target','dotnet','--targetargs','bin/Release/net10.0/DiffBench.dll --test','--include','[DiffBench]*','--include-test-assembly','--format','cobertura','--output','results/diff/harness.cobertura.xml')
        }
        'Mutation'{
            $tests=Join-Path $root 'DiffBench.Tests';if(!(Test-Path -LiteralPath $tests -PathType Container)){throw 'Missing tests'}
            $tool=Find-DotnetTool 'stryker' 'dotnet-stryker.exe' 'dotnet-stryker' '5.0.0'
            Push-Location -LiteralPath $tests
            try{Invoke-Checked $tool @('--config-file','stryker-config.json')}finally{Pop-Location}
        }
    }
}finally{Pop-Location}
