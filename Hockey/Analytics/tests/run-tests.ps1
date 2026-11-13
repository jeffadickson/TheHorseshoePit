$ErrorActionPreference = 'Stop'

$script = (Resolve-Path (Join-Path $PSScriptRoot '..\hockeybalance.cs')).Path
$teams = (Resolve-Path (Join-Path $PSScriptRoot 'teams.tsv')).Path
$players = (Resolve-Path (Join-Path $PSScriptRoot 'players.tsv')).Path
$runRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('hockeybalance-tests-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $runRoot | Out-Null

function Assert-Contains([string]$text, [string]$expected, [string]$message) {
    if (-not $text.Contains($expected)) { throw "${message}: missing '$expected'" }
}

try {
    $textOutput = (& dotnet run $script -- $teams $players --text 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Text-mode command failed: $textOutput" }

    # Equal goals share rank 1, and the tied names are ordered by last name.
    Assert-Contains $textOutput '  1  Ben Alpha' 'goal rank / last-name order'
    Assert-Contains $textOutput '  1  Alex Zebra' 'goal tie rank'

    $htmlOutput = (& dotnet run $script -- $teams $players 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "HTML-mode command failed: $htmlOutput" }
    $htmlPath = Join-Path $runRoot 'hockeybalance-10u-b-regular-season.html'
    # The file-based app writes into its working directory; copy the generated
    # report into the isolated assertion directory before checking its contents.
    $generated = Join-Path (Get-Location) 'hockeybalance-10u-b-regular-season.html'
    if (-not (Test-Path $generated)) { throw "HTML output was not generated" }
    Copy-Item $generated $htmlPath
    $html = Get-Content -Raw $htmlPath
    Assert-Contains $html 'Player point ranking' 'point ranking table'
    Assert-Contains $html 'Goals by player' 'goals-by-player chart'
    Assert-Contains $html 'Include assists' 'assists toggle'
    Assert-Contains $html 'Top 3 scorers (G:A:Pts)' 'team balance detail'
    Assert-Contains $html '<span>Player</span>' 'source table without SCAHA rank'
    Assert-Contains $html 'class="sort-button"' 'visible sortable table controls'
    Assert-Contains $html 'Type to filter...' 'filterable table control'
    Assert-Contains $html 'aria-sort="none"' 'unsorted indicator'
    Assert-Contains $html 'title="Ben Alpha: 7 goals"' 'player bar hover detail'
    if ($html.IndexOf('Player goals and team-goal share') -gt $html.IndexOf('Player point ranking') -or
        $html.IndexOf('Player point ranking') -gt $html.IndexOf('Goals by player')) {
        throw 'Goal distribution chart should be the third report section'
    }
    if ($html.Contains('team-logos.svg')) { throw 'Report should not reference team logos' }

    Write-Output 'HockeyBalance tests passed.'
}
finally {
    if (Test-Path $runRoot) { Remove-Item -LiteralPath $runRoot -Recurse -Force }
}
