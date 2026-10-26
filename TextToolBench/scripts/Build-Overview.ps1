# Rebuild the offline overview; this never reruns or modifies the measured experiments.
param([ValidateSet('Build','Test','Mutation')][string]$Mode='Build',[switch]$Native)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($folder in @($root,(Join-Path $root 'results'))){if(!(Test-Path -LiteralPath $folder -PathType Container)){throw "Missing directory: $folder"}}
function Invoke-Checked([string]$Program,[string[]]$Arguments){
    & $Program @Arguments
    if($LASTEXITCODE -ne 0){throw "$Program failed with exit code $LASTEXITCODE"}
}
Push-Location -LiteralPath $root
try{
    switch($Mode){
        'Build'{
            if($Native){
                $tools=Join-Path $root 'tools'
                if(!(Test-Path -LiteralPath $tools -PathType Container)){New-Item -ItemType Directory -Path $tools|Out-Null}
                Invoke-Checked dotnet @('publish','Overview.csproj','-c','Release','-r','win-x64','-p:PublishAot=true','-o','tools/overview-native')
                Invoke-Checked (Join-Path $tools 'overview-native/Overview.exe') @($root)
            }else{
                Invoke-Checked dotnet @('build','Overview.csproj','-c','Release')
                Invoke-Checked dotnet @('bin/Release/net10.0/Overview.dll',$root)
            }
        }
        'Test'{Invoke-Checked dotnet @('test','SedBench.Tests/SedBench.Tests.csproj','--collect:XPlat Code Coverage','--results-directory','results/overview-verification/tests')}
        'Mutation'{
            $tests=Join-Path $root 'SedBench.Tests'
            if(!(Test-Path -LiteralPath $tests -PathType Container)){throw 'Missing test directory'}
            $tool=Join-Path $root 'tools/stryker/dotnet-stryker.exe'
            if(!(Test-Path -LiteralPath $tool -PathType Leaf)){Invoke-Checked dotnet @('tool','install','dotnet-stryker','--tool-path','tools/stryker','--version','5.0.0')}
            Push-Location -LiteralPath $tests
            try{Invoke-Checked $tool @('--config-file','overview-stryker-config.json')}finally{Pop-Location}
        }
    }
}finally{Pop-Location}
