# Collect existing evidence only; never rerun timings, change raw data or infer missing metrics.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($folder in @($root,(Join-Path $root 'results/diff'),(Join-Path $root 'results/diff/unit-tests'))){
    if(!(Test-Path -LiteralPath $folder -PathType Container)){throw "Missing evidence directory: $folder"}
}
Push-Location -LiteralPath $root
try{
    $unitPath=Get-ChildItem -LiteralPath results/diff/unit-tests -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if(!$unitPath){throw 'Missing unit coverage'}
    [xml]$unit=Get-Content -LiteralPath $unitPath.FullName -Raw
    $model=$unit.coverage.packages.package.classes.class|Where-Object name -eq DiffModel
    [xml]$cli=Get-Content -LiteralPath results/diff/harness.cobertura.xml -Raw
    [xml]$trx=Get-Content -LiteralPath results/diff/unit-tests/model.trx -Raw
    $counts=$trx.TestRun.ResultSummary.Counters
    $validation=Get-Content -LiteralPath results/diff/validation.json -Raw|ConvertFrom-Json
    $rows=@(Get-Content -LiteralPath results/diff/runs.jsonl|ForEach-Object{$_|ConvertFrom-Json})
    $summaries=@(Get-Content -LiteralPath results/diff/summary.json -Raw|ConvertFrom-Json)
    if($rows.Count -ne 2976 -or @($rows|Where-Object Round -ge 0).Count -ne 2604 -or $summaries.Count -ne 372 -or @($summaries|Where-Object N -ne 7).Count){throw 'Retained campaign matrix differs from this session'}
    $scope=[ordered]@{
        Requirement='BENCH-012'
        Measurements=[ordered]@{
            Measured=2604;Warmups=372;Cells=372;Candidates=390;ResourceExclusions=18
            OriginalCampaignExitCode=1;Reason='Postprocessing failed on an open writer after all eight complete rounds; raw data preserved'
            RecoveredReportExitCode=0;MissingChildMetrics='Git CPU/memory raw counters are launcher-only; summary memory is null'
        }
        Tests=[ordered]@{
            NUnitPassed=[int]$counts.passed;NUnitFailed=[int]$counts.failed;FsCheckCases=1000;FsCheckReplay=@(20261027,17)
            CliSeed=20261027;CliPassed=$validation.Passed;CliFailures=$validation.Failures.Count;NegativeChecks=$validation.NegativeChecks
            PatchCorruptionMutantsKilled=$validation.PatchCorruptionMutantsKilled;MinimalReferenceChecks=$validation.MinimalReferenceChecks
            DomChecks=3;OverviewDomChecks=4;OverviewViews=134;UpstreamRustPassed=251;UpstreamRustSkipped=32
        }
        Coverage=[ordered]@{
            ModelLinePercent=100*[double]$model.'line-rate';ModelBranchPercent=100*[double]$model.'branch-rate'
            UnitEvidence=$unitPath.FullName.Substring($root.Length+1).Replace('\','/')
            CliAndReportModuleLinePercent=100*[double]$cli.coverage.'line-rate';CliAndReportModuleBranchPercent=100*[double]$cli.coverage.'branch-rate'
            CliEvidence='results/diff/harness.cobertura.xml'
            Scope='CLI conformance plus report mode, not instrumented timings. Model uses final NUnit/FsCheck. Earlier coverage files are historical intermediate versions, not comparable improvements.'
            Unmeasured=@('native binaries','Git child CPU/memory','PowerShell/shell wrappers','rendered layout','Linux','coverage-guided fuzzing')
        }
        Mutation=[ordered]@{
            Tool='Stryker.NET 5.0.0';Scope='DiffModel only; 20-test baseline before report tests were added'
            ScorePercent=95.69;Killed=103;Timeout=8;Survived=5;CompileError=47;Ignored=404;IncludesTimeouts=$true
            Report='results/diff/mutation-report.json'
        }
        Libraries=[ordered]@{
            FsCheck='3.3.2';NUnit='4.3.2';CoverletCollector='6.0.4';CoverletConsole='10.1.0';Jsdom='30.1.3'
            Production='System.Diagnostics.Process, System.Text.Json, SHA256, filesystem, minimal Windows counter interop; GNU patch'
        }
        SourceIdentity=[ordered]@{
            AsMeasuredAssembly=(Get-Content -LiteralPath results/diff/campaign-assembly.json -Raw|ConvertFrom-Json).Hash
            FinalAssembly=(Get-FileHash -LiteralPath bin/Release/net10.0/DiffBench.dll).Hash
            CodeFixture=(Get-FileHash -LiteralPath results/diff/source-code-fixture.cs).Hash
            ArchivedMain=(Get-FileHash -LiteralPath results/diff/campaign-source/DiffBench.cs).Hash
            PostMeasurementChanges='File-lease regression fix, saved code-fixture reuse, responsive HTML and unavailable Git child metrics; no recorded measurements replaced'
        }
    }
    $scope|ConvertTo-Json -Depth 12|Set-Content -LiteralPath results/diff/verification.json
    $sources=@('DiffBench.cs','DiffModel.cs','DiffBench.csproj','Directory.Build.props','DiffBench.Tests/ModelTests.cs','DiffBench.Tests/ReportTests.cs','DiffBench.Tests/DiffBench.Tests.csproj','DiffBench.Tests/packages.lock.json','scripts/Get-DiffTools.ps1','scripts/Run-DiffBench.ps1','scripts/build-diff.sh','scripts/diff-stddef-wint.patch','scripts/Test-DiffReport.cjs','scripts/Collect-DiffEvidence.ps1','requirements.sdoc')
    $sources|ForEach-Object{[ordered]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_).Hash}}|ConvertTo-Json|Set-Content -LiteralPath results/diff/source-hashes.json
    Copy-Item -LiteralPath bin/Release/net10.0/DiffBench.xml -Destination results/diff/api/DiffBench.xml
    $scope.Tests|ConvertTo-Json
    $scope.Coverage|ConvertTo-Json -Depth 4
}finally{Pop-Location}
