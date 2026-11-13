# HockeyBalance

Goal-share and goal-balance reports for SCAHA hockey teams. The live mode fetches
standings and player totals from `www.scaha.net`; offline mode accepts pasted
tab-separated tables.

## Requirements

- .NET 10 SDK or later
- Network access for live mode

## Live data

Run the default schedule:

```powershell
dotnet run hockeybalance.cs
```

Select a different schedule and team:

```powershell
dotnet run hockeybalance.cs -- --schedule "10U B Regular Season" --team "10U B Flyers(2)"
```

The `--team` value is normalized for matching, so these forms select the same
team when the site reports it as `Flyers (2)`:

```text
Flyers (2)
10U B Flyers(2)
```

Other live-mode options:

```text
--schedule NAME    Schedule shown in the site's schedule selector
--team NAME        Restrict reports to one team; age/division prefixes are allowed
--top N            Show only the first N players by goals in report 1
--base-url URL     Override the site root (default: https://www.scaha.net/scaha/)
```

The live fetcher keeps the site's session cookies, posts the JSF schedule-change
request, and uses normal browser request headers because SCAHA rejects synthetic
user agents.

## Offline data

Provide two tab-separated files: standings first, player totals second.

```powershell
dotnet run hockeybalance.cs -- teams.txt players.txt --team "Flyers (2)"
```

Expected columns:

```text
teams:   rank  Team  GP  W  L  T  Pts  GF  GA  GD
players: #     Name  Team  GP  G  A  Pts  PIMS
```

The script prints player goal shares, team goal-balance rankings, and a
reconciliation check between player goals and team goals-for.
