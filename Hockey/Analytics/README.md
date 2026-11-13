# HockeyBalance Analytics

HockeyBalance fetches SCAHA standings and player totals, filters an optional
team, and produces goal-share and goal-balance reports. The default output is a
self-contained HTML report with pivot-style tables; the original console report
is available with `--text`.

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

The HTML report contains five tables:

1. Player goal share
2. Team goal balance
3. Source team standings
4. Source player totals
5. Player-goal versus team-GF reconciliation

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

## Optional Obscura installation

The current SCAHA fetcher uses direct HTTP and does not require Obscura. Obscura
is an optional headless browser/CDP engine for future browser-backed fetching or
smoke tests. Keep the `obscura` executable and its `obscura-worker` companion in
the same directory.

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
