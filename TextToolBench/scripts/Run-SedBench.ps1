# Windows entry point for acquisition, incremental builds, tests and uninstrumented measurements.
param([ValidateSet('Benchmark','Test','Coverage','Mutation','Report')][string]$Mode='Benchmark')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($folder in @($root,(Join-Path $root 'scripts'),(Join-Path $root 'results'))) {
    if (!(Test-Path -LiteralPath $folder -PathType Container)) { throw "Missing directory: $folder" }
}
function Invoke-Checked([string]$Program,[string[]]$Arguments) {
    & $Program @Arguments
    if($LASTEXITCODE -ne 0){throw "$Program exited $LASTEXITCODE"}
}
Push-Location -LiteralPath $root
try {
    if($Mode -eq 'Report') {
        Invoke-Checked dotnet @('build','Analyze.csproj','-c','Release')
        Invoke-Checked dotnet @('bin/Release/net10.0/Analyze.dll')
        return
    }
    & (Join-Path $PSScriptRoot 'Get-SedTools.ps1')
    Invoke-Checked dotnet @('build','SedBench.csproj','-c','Release')
    switch($Mode) {
        'Benchmark' { Invoke-Checked dotnet @('bin/Release/net10.0/SedBench.dll') }
        'Test' {
            Invoke-Checked dotnet @('bin/Release/net10.0/SedBench.dll','--test')
            Invoke-Checked dotnet @('test','SedBench.Tests/SedBench.Tests.csproj','--logger','trx;LogFileName=oracle.trx','--results-directory','results/sed/unit-tests')
        }
        'Coverage' {
            $tool=Join-Path $root 'tools/coverage/coverlet.exe'
            if(!(Test-Path -LiteralPath $tool -PathType Leaf)) {
                Invoke-Checked dotnet @('tool','install','coverlet.console','--tool-path','tools/coverage','--version','10.1.0')
            }
            Invoke-Checked $tool @('bin/Release/net10.0/SedBench.dll','--target','dotnet','--targetargs','bin/Release/net10.0/SedBench.dll --test','--include','[SedBench]*','--include-test-assembly','--format','cobertura','--output','results/coverage-sed.cobertura.xml')
        }
        'Mutation' {
            $tool=Join-Path $root 'tools/stryker/dotnet-stryker.exe'
            if(!(Test-Path -LiteralPath $tool -PathType Leaf)) {
                Invoke-Checked dotnet @('tool','install','dotnet-stryker','--tool-path','tools/stryker','--version','5.0.0')
            }
            $tests=Join-Path $root 'SedBench.Tests'
            if(!(Test-Path -LiteralPath $tests -PathType Container)){throw 'Missing oracle tests'}
            Push-Location -LiteralPath $tests
            try{Invoke-Checked $tool @('--config-file','stryker-config.json')}finally{Pop-Location}
        }
    }
}finally{Pop-Location}
