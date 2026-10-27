param([Parameter(Mandatory=$true)][string]$BenchmarkRoot,[switch]$Mutation,[switch]$Block256)
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $PSScriptRoot -PathType Container)) {throw 'Missing extension directory'}
if (!(Test-Path -LiteralPath $BenchmarkRoot -PathType Container)) {throw 'Missing benchmark directory'}
$results=Join-Path $PSScriptRoot $(if($Block256){'results/block256'}else{'results/optimized'})
if (!(Test-Path -LiteralPath $results -PathType Container)) {throw 'Build the extension first'}
function Check-Exit {if($LASTEXITCODE -ne 0){throw "Verification command failed: $LASTEXITCODE"}}
dotnet test (Join-Path $PSScriptRoot 'Experiment.Tests.csproj') -c Release '--collect:XPlat Code Coverage' --results-directory (Join-Path $results 'harness-tests') --logger 'trx;LogFileName=harness.trx'; Check-Exit
$assembly=Join-Path $PSScriptRoot 'bin/Experiment/Release/net10.0/Experiment.dll'
$targetArgs='"'+$assembly+'" "'+$PSScriptRoot+'" "'+$BenchmarkRoot+'" --test'
if($Block256){$targetArgs+=' --block256'}
$coverlet=Join-Path $BenchmarkRoot 'tools/coverage/coverlet.exe'
if (!(Test-Path -LiteralPath $coverlet -PathType Leaf)) {throw 'Missing existing Coverlet console tool'}
& $coverlet $assembly --target dotnet --targetargs $targetArgs --include '[Experiment]*' --include-test-assembly --format cobertura --output (Join-Path $results 'harness-integration.cobertura.xml'); Check-Exit
if($Mutation){
    # Stryker spans are character offsets. Recalculate when source changes.
    $code=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Experiment.cs'))
    $first=$code.IndexOf('    internal static (long Bytes')
    $end=$code.IndexOf('    private static async Task Validate')
    $quantile=$code.IndexOf('    internal static double Quantile')
    $quantileEnd=$code.IndexOf('    private static void Require')
    if($first -lt 0 -or $end -lt $first -or $quantile -lt 0 -or $quantileEnd -lt $quantile){throw 'Mutation spans not found'}
    $configPath=Join-Path $PSScriptRoot 'stryker-config.json'
    $config=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $reportCode=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ProfileReport.cs'))
    $aggregate=$reportCode.IndexOf('    internal static double? AggregateMetric')
    $aggregateEnd=$reportCode.IndexOf('    internal static void Write')
    if($aggregate -lt 0 -or $aggregateEnd -lt $aggregate){throw 'Profile aggregation span missing'}
    $config.'stryker-config'.mutate=@("Experiment.cs{${first}..${end}}","Experiment.cs{${quantile}..${quantileEnd}}","ProfileReport.cs{${aggregate}..${aggregateEnd}}")
    $config|ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $configPath
    Push-Location -LiteralPath $PSScriptRoot
    try { & (Join-Path $BenchmarkRoot 'tools/stryker/dotnet-stryker.exe') --config-file stryker-config.json --output (Join-Path $results 'stryker'); Check-Exit }
    finally {Pop-Location}
}
