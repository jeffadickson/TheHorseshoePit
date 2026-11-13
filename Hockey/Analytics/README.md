# HockeyBalance Analytics

HockeyBalance fetches SCAHA standings and player totals, filters an optional
team, and produces goal-share and goal-balance reports. The default output is a
self-contained HTML report with filterable and sortable tables; the original
console report is available with `--text`.

## Requirements

- .NET 10 SDK or later
- Network access for live mode
- `System.CommandLine` 2.0.0 (restored automatically by the file-based app)

## Run it

HTML is the default:

```powershell
dotnet run hockeybalance.cs -- --team "10U B Flyers(2)"
```

This writes `hockeybalance.html` in the current directory. Use text output when
you want the original terminal report:

```powershell
dotnet run hockeybalance.cs -- --text --team "10U B Flyers(2)"
```

## Detailed examples

### 1. Generate a league-wide HTML report

Omit `--team` to include every team in the selected schedule:

```powershell
dotnet run hockeybalance.cs
```

The report is written to `hockeybalance.html`. To keep separate snapshots, run
from a separate output directory:

```powershell
New-Item -ItemType Directory -Force reports\10u-b | Out-Null
Push-Location reports\10u-b
dotnet run ..\..\hockeybalance.cs
Pop-Location
```

### 2. Report one team

The preferred team string can include the age/division prefix. Matching ignores
spaces and punctuation, so `10U B Flyers(2)` matches the site's `Flyers (2)`:

```powershell
dotnet run hockeybalance.cs -- --team "10U B Flyers(2)"
```

The equivalent short form is:

```powershell
dotnet run hockeybalance.cs -- --team "Flyers (2)"
```

### 3. Limit the player-share table

`--top` affects only the player-share table. Team balance, source tables, and
reconciliation still use the complete selected dataset:

```powershell
dotnet run hockeybalance.cs -- --top 5
dotnet run hockeybalance.cs -- --team "Flyers (2)" --top 3
```

### 4. Use a different schedule

The schedule name must match an entry in the SCAHA schedule selector:

```powershell
dotnet run hockeybalance.cs -- --schedule "12U A Regular Season"
dotnet run hockeybalance.cs -- --schedule "12U A Regular Season" --team "12U A Hawks"
```

### 5. Keep the output in the terminal

Use `--text` for shell pipelines, quick checks, or logs:

```powershell
dotnet run hockeybalance.cs -- --text | Tee-Object league-report.txt
dotnet run hockeybalance.cs -- --text --team "Flyers (2)" --top 10
```

### 6. Use a custom site root

This is useful for a mirror, test server, or captured-compatible endpoint:

```powershell
dotnet run hockeybalance.cs -- --base-url "https://www.scaha.net/scaha/"
```

The HTML report contains five filterable, sortable tables:

1. Player goal share
2. Team goal balance
3. Source team standings
4. Source player totals
5. Player-goal versus team-GF reconciliation

It also includes a responsive three-column bar chart sorted by average goals per
scoring player. That metric is calculated as player goals divided by the number
of players with at least one goal. The source player table preserves the SCAHA
`#` field as the source ranking/order; goal-rating tables sort by goals
descending, then last name, and assign competition ranks so equal goal totals
share a rank (`1, 1, 3`). The source rank is not treated as a jersey number.

The source tables preserve every standings and player field used by the report:
GP, W, L, T, points, GF, GA, GD, goals, assists, points, and PIMS.

## Options

```text
--schedule NAME    Schedule shown in the SCAHA schedule selector
--team NAME        Restrict reports to one team; age/division prefixes allowed
--top N            Limit the player-share table to the first N goal scorers
--text             Print the console report instead of writing HTML
--base-url URL     Override the site root
```

These team filters are equivalent when the site reports the team as `Flyers (2)`:

```text
Flyers (2)
10U B Flyers(2)
```

## Offline mode

Pass two tab-separated files: standings first, player totals second.

```powershell
dotnet run hockeybalance.cs -- teams.txt players.txt --text
```

Expected columns:

```text
teams:   rank  Team  GP  W  L  T  Pts  GF  GA  GD
players: #     Name  Team  GP  G  A  Pts  PIMS
```

For example, `teams.txt` might contain:

```text
rank\tTeam\tGP\tW\tL\tT\tPts\tGF\tGA\tGD
1\tFlyers (2)\t10\t8\t1\t1\t17\t27\t12\t15
```

And `players.txt` might contain:

```text
#\tName\tTeam\tGP\tG\tA\tPts\tPIMS
1\tDanila Grebinchenko\tFlyers (2)\t10\t7\t3\t10\t2
2\tDeclan Griffis\tFlyers (2)\t10\t7\t1\t8\t0
```

Run the offline files and filter using the same normalized team syntax:

```powershell
dotnet run hockeybalance.cs -- teams.txt players.txt --team "10U B Flyers(2)"
```

Offline mode ignores the header row and any non-data rows whose first field is
not an integer. It still validates that every player team exists in standings.

### 7. Inspect the generated HTML

The report is ordinary self-contained HTML, so it can be opened directly:

```powershell
Start-Process .\hockeybalance.html
```

It contains no external JavaScript or CSS dependencies and can be archived or
shared as a single file.

## Optional Obscura installation

The current SCAHA fetcher uses direct HTTP and does not require Obscura. Obscura
is an open-source, lightweight headless browser engine for agent and scraping
workloads. It provides a Chrome DevTools Protocol (CDP) endpoint that can be
used by Playwright or other browser clients. In this project it is an optional
dependency for future browser-backed fetching, JavaScript-heavy pages, or smoke
tests; it is not needed for the default HTTP fetcher. Keep the `obscura`
executable and its `obscura-worker` companion in the same directory.

### Linux x86_64

```bash
curl -LO https://github.com/h4ckf0r0day/obscura/releases/latest/download/obscura-x86_64-linux.tar.gz
tar xzf obscura-x86_64-linux.tar.gz
./obscura --version
```

### Linux ARM64

```bash
curl -LO https://github.com/h4ckf0r0day/obscura/releases/latest/download/obscura-aarch64-linux.tar.gz
tar xzf obscura-aarch64-linux.tar.gz
./obscura --version
```

Linux releases target Ubuntu 22.04 and require glibc 2.35 or later.

### macOS Apple Silicon

```bash
curl -LO https://github.com/h4ckf0r0day/obscura/releases/latest/download/obscura-aarch64-macos.tar.gz
tar xzf obscura-aarch64-macos.tar.gz
xattr -d com.apple.quarantine ./obscura 2>/dev/null || true
./obscura --version
```

### macOS Intel

```bash
curl -LO https://github.com/h4ckf0r0day/obscura/releases/latest/download/obscura-x86_64-macos.tar.gz
tar xzf obscura-x86_64-macos.tar.gz
xattr -d com.apple.quarantine ./obscura 2>/dev/null || true
./obscura --version
```

### Windows

Download the Windows ZIP from the [Obscura releases page](https://github.com/h4ckf0r0day/obscura/releases), extract it, and run:

```powershell
.\obscura.exe --version
```

### Docker

```bash
export OBSCURA_CDP_TOKEN="$(openssl rand -hex 32)"
docker run -d --name obscura -p 127.0.0.1:9222:9222 \
  -e OBSCURA_CDP_TOKEN h4ckf0r0day/obscura
```

See the [Obscura installation guide](https://github.com/h4ckf0r0day/obscura/wiki/Installation)
for current release names, architectures, and troubleshooting.
