#!/usr/bin/env dotnet
#:package System.CommandLine@2.0.0
// Copyright (c) 2026 Jeff Dickson <jeff.dickson@gmail.com>
// SPDX-License-Identifier: MIT
// HockeyBalance - goal-share and goal-balance reports for a league.
// Goals only: assists are ignored because they are tracked unreliably.
//
// Usage (.NET 10 SDK or later, no project file needed):
//   dotnet run hockeybalance.cs                              live data -> hockeybalance.html
//   dotnet run hockeybalance.cs -- --text                   live data -> console text
//   dotnet run hockeybalance.cs -- --schedule "12U A Regular Season"
//   dotnet run hockeybalance.cs -- teams.txt players.txt     offline, from pasted tables
//
// Options:
//   --schedule NAME   schedule to fetch, as shown in the site's "Select a Schedule" list
//                     (default "10U B Regular Season"; the site's current season is used)
//   --team NAME       restrict reports to one team; age/division prefixes are allowed
//   --top N           limit report 1 (player shares) to the first N players by goals
//   --text            print the console report instead of writing HTML
//   --base-url URL    site root (default https://www.scaha.net/scaha/)
//
// Live mode reads two pages of www.scaha.net: scoreboard.xhtml (standings) and
// statscentral.xhtml (player stats). Each is a JSF page, so the script GETs the
// page, then POSTs the same partial-ajax "schedule changed" request the browser
// sends, and reads the table out of the response.
//
// Offline mode reads the site's tables pasted as-is (tab separated). Header rows,
// "Search:" and "Showing 1 to ..." lines are ignored.
//
//   teams:    rank  Team  GP  W  L  T  Pts  GF  GA  GD
//   players:  #     Name  Team  GP  G  A  Pts  PIMS

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.CommandLine;

var filesArgument = new Argument<string[]>("files")
{
    Arity = ArgumentArity.ZeroOrMore,
    Description = "Optional teams and players TSV files for offline mode.",
};
var scheduleOption = new Option<string>("--schedule")
{
    DefaultValueFactory = _ => "10U B Regular Season",
    Description = "Schedule shown in the site's schedule selector.",
};
var teamOption = new Option<string?>("--team")
{
    Description = "Restrict reports to one team; age/division prefixes are allowed.",
};
var topOption = new Option<int?>("--top")
{
    Description = "Limit the player-share table to the first N players by goals.",
};
var baseUrlOption = new Option<string>("--base-url")
{
    DefaultValueFactory = _ => "https://www.scaha.net/scaha/",
    Description = "Site root for live mode.",
};
var textOption = new Option<bool>("--text")
{
    Description = "Print the text reports instead of writing HTML.",
};

var root = new RootCommand("HockeyBalance goal-share and goal-balance reports.");
root.Arguments.Add(filesArgument);
root.Options.Add(scheduleOption);
root.Options.Add(teamOption);
root.Options.Add(topOption);
root.Options.Add(baseUrlOption);
root.Options.Add(textOption);
root.SetAction(async parseResult =>
{
    string[] files = parseResult.GetValue(filesArgument) ?? [];
    string schedule = parseResult.GetValue(scheduleOption)!;
    string? team = parseResult.GetValue(teamOption);
    int? top = parseResult.GetValue(topOption);
    string baseUrl = parseResult.GetValue(baseUrlOption)!;
    bool text = parseResult.GetValue(textOption);

    if (files.Length is not (0 or 2))
    {
        Console.Error.WriteLine("Give both a teams file and a players file, or neither to fetch live data.");
        return 1;
    }
    if (top is <= 0)
    {
        Console.Error.WriteLine("--top needs a positive integer.");
        return 1;
    }

    List<string[]> teamRows, playerRows;
    try
    {
        if (files.Length == 2)
        {
            teamRows = Parser.FileRows(files[0]);
            playerRows = Parser.FileRows(files[1]);
        }
        else
        {
            if (text) Console.WriteLine($"Fetching \"{schedule}\" from {baseUrl} ...");
            using var site = new Scaha(baseUrl);
            teamRows = await site.FetchTable("scoreboard.xhtml", schedule, "parts");
            playerRows = await site.FetchTable("statscentral.xhtml", schedule, "playertotals");
        }
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or TaskCanceledException)
    {
        Console.Error.WriteLine("Error: " + ex.Message);
        return 1;
    }

    Dictionary<string, Team> teams;
    List<Player> players;
    try
    {
        teams = Parser.Teams(teamRows);
        players = Parser.Players(playerRows);
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine("Error: " + ex.Message);
        return 1;
    }

    var unknown = players.Select(p => p.Team).Distinct().Where(t => !teams.ContainsKey(t)).ToList();
    if (unknown.Count > 0)
    {
        Console.Error.WriteLine("Players reference teams missing from the teams file: " + string.Join(", ", unknown));
        return 1;
    }

    if (team is not null)
    {
        var matches = teams.Keys.Where(name => TeamMatches(name, team)).ToList();
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"Team \"{team}\" was not found. Available teams: " +
                                     string.Join(", ", teams.Keys.Order(StringComparer.Ordinal)));
            return 1;
        }
        if (matches.Count > 1)
        {
            Console.Error.WriteLine($"Team \"{team}\" is ambiguous: " + string.Join(", ", matches));
            return 1;
        }

        string selectedTeam = matches[0];
        teams = teams.Where(pair => pair.Key == selectedTeam)
                     .ToDictionary(pair => pair.Key, pair => pair.Value);
        players = players.Where(p => p.Team == selectedTeam).ToList();
    }

    players = players.OrderByDescending(p => p.Goals).ThenBy(p => p.Name, StringComparer.Ordinal).ToList();
    List<TeamStats> stats = teams.Values
        .Select(t => new TeamStats(t, players.Where(p => p.Team == t.Name).ToList()))
        .ToList();

    if (text)
    {
        Reports.PlayerShares(players, teams, top);
        Reports.GoalBalance(stats);
        Reports.Reconciliation(stats);
    }
    else
    {
        string output = Path.Combine(Environment.CurrentDirectory, "hockeybalance.html");
        await HtmlReports.Write(output, schedule, team, players, teams, stats, top);
        Console.WriteLine($"Wrote {output}");
    }
    return 0;
});

return await root.Parse(args).InvokeAsync();

static bool TeamMatches(string actual, string requested)
{
    static string Key(string value) =>
        Regex.Replace(value, "[^A-Za-z0-9]", "").ToUpperInvariant();

    string actualKey = Key(actual);
    string requestedKey = Key(requested);
    return requestedKey == actualKey || requestedKey.EndsWith(actualKey, StringComparison.Ordinal);
}

record Team(string Name, int GamesPlayed, int Wins, int Losses, int Ties,
            int Points, int GoalsFor, int GoalsAgainst, int GoalDifference);

record Player(string Name, string Team, int GamesPlayed, int Goals, int Assists,
              int Points, int Pims);

/// <summary>Per-team goal distribution. Roster is ordered by goals, descending.</summary>
record TeamStats(Team Team, List<Player> Roster)
{
    public int PlayerGoals => Roster.Sum(p => p.Goals);
    public int Scorers => Roster.Count(p => p.Goals > 0);
    public int TopGoals => Roster.FirstOrDefault()?.Goals ?? 0;
    public int Top3Goals => Roster.Take(3).Sum(p => p.Goals);

    /// <summary>Inverse Herfindahl index on goals: the number of equal-share
    /// scorers that would give the same concentration. Higher = more balanced.</summary>
    public double EffectiveScorers
    {
        get
        {
            double sumSq = Roster.Sum(p => (double)p.Goals * p.Goals);
            return sumSq == 0 ? 0 : (double)PlayerGoals * PlayerGoals / sumSq;
        }
    }
}

static class Parser
{
    /// <summary>Tab-separated rows of a pasted table whose first field is an integer (rank / #).</summary>
    public static List<string[]> FileRows(string path) =>
        File.ReadLines(path)
            .Select(line => line.Split('\t').Select(f => f.Trim()).ToArray())
            .Where(f => f.Length > 1 && int.TryParse(f[0], out _))
            .ToList();

    /// <summary>Standings rows: rank, Team, GP, W, L, T, Pts, GF, GA, GD.</summary>
    public static Dictionary<string, Team> Teams(List<string[]> rows)
    {
        var teams = new Dictionary<string, Team>();
        foreach (string[] f in rows)
            if (f.Length >= 10 &&
                int.TryParse(f[2], out int gp) &&
                int.TryParse(f[3], out int w) &&
                int.TryParse(f[4], out int l) &&
                int.TryParse(f[5], out int t) &&
                int.TryParse(f[6], out int pts) &&
                int.TryParse(f[7], out int gf) &&
                int.TryParse(f[8], out int ga) &&
                int.TryParse(f[9], out int gd))
                teams[f[1]] = new Team(f[1], gp, w, l, t, pts, gf, ga, gd);
        if (teams.Count == 0) throw new InvalidDataException("No team rows found.");
        return teams;
    }

    /// <summary>Player rows: #, Name, Team, GP, G, A, Pts, PIMS.</summary>
    public static List<Player> Players(List<string[]> rows)
    {
        var players = new List<Player>();
        foreach (string[] f in rows)
            if (f.Length >= 8 &&
                int.TryParse(f[3], out int gp) &&
                int.TryParse(f[4], out int g) &&
                int.TryParse(f[5], out int a) &&
                int.TryParse(f[6], out int pts) &&
                int.TryParse(f[7], out int pims))
                players.Add(new Player(f[1], f[2], gp, g, a, pts, pims));
        if (players.Count == 0) throw new InvalidDataException("No player rows found.");
        return players;
    }
}

/// <summary>Minimal client for the SCAHA site's JSF (MyFaces + BootsFaces) pages.</summary>
sealed class Scaha : IDisposable
{
    const RegexOptions Opt = RegexOptions.Singleline | RegexOptions.IgnoreCase;
    readonly HttpClient _http;

    public Scaha(string baseUrl)
    {
        _http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip |
                                      DecompressionMethods.Deflate |
                                      DecompressionMethods.Brotli,
        })
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(60),
        };
        // SCAHA rejects clearly synthetic user agents with HTTP 403. Use a normal
        // browser profile and the headers its pages expect, without executing JS.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _http.DefaultRequestHeaders.Referrer = new Uri(_http.BaseAddress!, ".");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Loads <paramref name="page"/>, selects <paramref name="schedule"/> in its schedule
    /// drop-down the way the browser does, and returns the cell text of every body row of
    /// the table whose id ends with ":<paramref name="tableIdSuffix"/>".
    /// </summary>
    public async Task<List<string[]>> FetchTable(string page, string schedule, string tableIdSuffix)
    {
        string html = await Read(await _http.GetAsync(page));

        // The drop-down that offers the schedule. Component ids are generated by the
        // server, so everything is located by content rather than by a fixed id.
        Match? select = null;
        string? value = null;
        var offered = new List<string>();
        foreach (Match sel in Regex.Matches(html, @"<select\b[^>]*>.*?</select>", Opt))
        {
            foreach (Match o in Regex.Matches(sel.Value, @"<option\b[^>]*>(.*?)</option>", Opt))
            {
                string label = Clean(o.Groups[1].Value);
                if (label.Contains("Regular Season", StringComparison.OrdinalIgnoreCase)) offered.Add(label);
                if (select is null && label.Equals(Clean(schedule), StringComparison.OrdinalIgnoreCase))
                {
                    select = sel;
                    value = Attr(o.Value, "value");
                }
            }
        }
        if (select is null || value is null)
            throw new InvalidDataException($"Schedule \"{schedule}\" is not offered on {page}. Regular-season schedules: " +
                                           string.Join("; ", offered.Distinct()));

        string selectName = Attr(select.Value, "name") ?? throw new InvalidDataException($"Schedule list on {page} has no name.");

        // The form around that drop-down.
        int formStart = html.LastIndexOf("<form", select.Index, StringComparison.OrdinalIgnoreCase);
        int formEnd = html.IndexOf("</form>", select.Index, StringComparison.OrdinalIgnoreCase);
        if (formStart < 0 || formEnd < 0) throw new InvalidDataException($"No form around the schedule list on {page}.");
        string form = html[formStart..formEnd];
        string formTag = form[..(form.IndexOf('>') + 1)];
        string formId = Attr(formTag, "id") ?? throw new InvalidDataException($"Form on {page} has no id.");
        string action = Attr(formTag, "action") ?? page;

        // Every form field with its current value, then the partial-ajax parameters.
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match sel in Regex.Matches(form, @"<select\b[^>]*>.*?</select>", Opt))
        {
            string? name = Attr(sel.Value, "name");
            if (name is null) continue;
            if (name == selectName) { fields.Add(new(name, value)); continue; }
            var options = Regex.Matches(sel.Value, @"<option\b[^>]*>", Opt);
            Match? chosen = options.FirstOrDefault(o => Attr(o.Value, "selected") is not null) ?? options.FirstOrDefault();
            fields.Add(new(name, chosen is null ? "" : Attr(chosen.Value, "value") ?? ""));
        }
        foreach (Match input in Regex.Matches(form, @"<input\b[^>]*>", Opt))
        {
            if (!string.Equals(Attr(input.Value, "type"), "hidden", StringComparison.OrdinalIgnoreCase)) continue;
            string? name = Attr(input.Value, "name");
            if (name is not null) fields.Add(new(name, Attr(input.Value, "value") ?? ""));
        }
        if (!fields.Any(f => f.Key == "javax.faces.ViewState"))
            throw new InvalidDataException($"No javax.faces.ViewState in the form on {page}.");

        fields.Add(new("params", "BsFEvent=change"));
        fields.Add(new(selectName, selectName));
        fields.Add(new("javax.faces.partial.event", "change"));
        fields.Add(new("javax.faces.source", selectName));
        fields.Add(new("javax.faces.partial.ajax", "true"));
        fields.Add(new("javax.faces.partial.execute", "@all"));
        fields.Add(new("javax.faces.partial.render", formId));
        fields.Add(new(formId, formId));

        string body = string.Join("&", fields.Select(f => WebUtility.UrlEncode(f.Key) + "=" + WebUtility.UrlEncode(f.Value)));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_http.BaseAddress!, action))
        {
            Content = new StringContent(body, Encoding.ASCII, "application/x-www-form-urlencoded"),
        };
        request.Headers.Add("Faces-Request", "partial/ajax");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        request.Headers.Referrer = new Uri(_http.BaseAddress!, page);
        string response = await Read(await _http.SendAsync(request));

        // The table, then each body row's cells.
        Match table = Regex.Match(response, $@"<table\b[^>]*\bid=""[^""]*:{Regex.Escape(tableIdSuffix)}""[^>]*>.*?</table>", Opt);
        if (!table.Success) throw new InvalidDataException($"Table \"{tableIdSuffix}\" not found in the response from {page}.");
        Match tbody = Regex.Match(table.Value, @"<tbody\b[^>]*>(.*?)</tbody>", Opt);
        var rows = new List<string[]>();
        foreach (Match tr in Regex.Matches(tbody.Groups[1].Value, @"<tr\b[^>]*>(.*?)</tr>", Opt))
        {
            string[] cells = Regex.Matches(tr.Groups[1].Value, @"<td\b[^>]*>(.*?)</td>", Opt)
                .Select(td => Clean(td.Groups[1].Value)).ToArray();
            if (cells.Length > 1) rows.Add(cells);
        }
        if (rows.Count == 0) throw new InvalidDataException($"Table \"{tableIdSuffix}\" on {page} has no rows for \"{schedule}\".");
        return rows;
    }

    /// <summary>Response body; the site serves ISO-8859-1.</summary>
    static async Task<string> Read(HttpResponseMessage response)
    {
        using (response)
        {
            response.EnsureSuccessStatusCode();
            return Encoding.Latin1.GetString(await response.Content.ReadAsByteArrayAsync());
        }
    }

    /// <summary>Value of an attribute in a single tag, or null when absent.</summary>
    static string? Attr(string tag, string name)
    {
        Match m = Regex.Match(tag, $@"\s{Regex.Escape(name)}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    /// <summary>Text of an HTML fragment: tags removed, entities decoded, whitespace collapsed.</summary>
    static string Clean(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", "")), @"\s+", " ").Trim();
}

static class HtmlReports
{
    public static async Task Write(string path, string schedule, string? requestedTeam,
                                   List<Player> players, Dictionary<string, Team> teams,
                                   List<TeamStats> stats, int? top)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>HockeyBalance report</title><style>");
        html.AppendLine("body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:#f4f7fb;color:#172033;margin:0;padding:2rem}main{max-width:1400px;margin:auto}h1{margin-bottom:.25rem}.meta{color:#526078;margin-top:0}.card{background:white;border:1px solid #d9e1ef;border-radius:12px;padding:1rem;margin:1rem 0;box-shadow:0 2px 8px #17203312}table{border-collapse:collapse;width:100%;font-size:.92rem}th,td{border-bottom:1px solid #e5eaf2;padding:.55rem .65rem;text-align:left;white-space:nowrap}th{background:#eaf0f8;position:sticky;top:0}tr:nth-child(even){background:#fafcff}.ok{color:#087443;font-weight:600}.off{color:#a33b16;font-weight:600}.note{font-size:.9rem;color:#526078}");
        html.AppendLine("</style></head><body><main>");
        html.AppendLine($"<h1>HockeyBalance</h1><p class=\"meta\">Schedule: <strong>{E(schedule)}</strong>" +
                        (requestedTeam is null ? "" : $" · Team filter: <strong>{E(requestedTeam)}</strong>") +
                        $" · Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}</p>");

        html.AppendLine("<section class=\"card\"><h2>Pivot: player share of team goals</h2>");
        html.AppendLine("<p class=\"note\">Players are ranked by goals. The share uses player goals divided by the selected team's goals-for.</p>");
        Table(html, ["#", "Player", "Team", "Team GF", "G", "% of team goals"],
            players.Where(p => p.Goals > 0).Take(top ?? int.MaxValue).Select((p, i) => new[] {
                (i + 1).ToString(CultureInfo.InvariantCulture), E(p.Name), E(p.Team),
                teams[p.Team].GoalsFor.ToString(CultureInfo.InvariantCulture),
                p.Goals.ToString(CultureInfo.InvariantCulture),
                Pct(p.Goals, teams[p.Team].GoalsFor)}));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"card\"><h2>Pivot: team goal balance</h2>");
        Table(html, ["Rank", "Team", "GF", "Scorers", "Top scorer %", "Top 3 %", "Effective scorers", "Top scorer(s)"],
            stats.OrderByDescending(s => s.EffectiveScorers).Select((s, i) => new[] {
                (i + 1).ToString(CultureInfo.InvariantCulture), E(s.Team.Name),
                s.Team.GoalsFor.ToString(CultureInfo.InvariantCulture),
                s.Scorers.ToString(CultureInfo.InvariantCulture), Pct(s.TopGoals, s.Team.GoalsFor),
                Pct(s.Top3Goals, s.Team.GoalsFor), s.EffectiveScorers.ToString("0.0", CultureInfo.InvariantCulture),
                E(s.TopGoals == 0 ? "" : string.Join(", ", s.Roster.Where(p => p.Goals == s.TopGoals).Select(p => p.Name)) + $" ({s.TopGoals})") }));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"card\"><h2>Source pivot: team standings</h2>");
        Table(html, ["Team", "GP", "W", "L", "T", "Pts", "GF", "GA", "GD"],
            teams.Values.OrderBy(t => t.Name).Select(t => new[] {E(t.Name),
                t.GamesPlayed.ToString(CultureInfo.InvariantCulture), t.Wins.ToString(CultureInfo.InvariantCulture),
                t.Losses.ToString(CultureInfo.InvariantCulture), t.Ties.ToString(CultureInfo.InvariantCulture),
                t.Points.ToString(CultureInfo.InvariantCulture), t.GoalsFor.ToString(CultureInfo.InvariantCulture),
                t.GoalsAgainst.ToString(CultureInfo.InvariantCulture), t.GoalDifference.ToString(CultureInfo.InvariantCulture)}));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"card\"><h2>Source pivot: player totals</h2>");
        Table(html, ["Player", "Team", "GP", "G", "A", "Pts", "PIMS"],
            players.Select(p => new[] {E(p.Name), E(p.Team), p.GamesPlayed.ToString(CultureInfo.InvariantCulture),
                p.Goals.ToString(CultureInfo.InvariantCulture), p.Assists.ToString(CultureInfo.InvariantCulture),
                p.Points.ToString(CultureInfo.InvariantCulture), p.Pims.ToString(CultureInfo.InvariantCulture)}));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"card\"><h2>Pivot: reconciliation</h2>");
        Table(html, ["Team", "Player goals", "Team GF", "Difference", "Status"],
            stats.OrderBy(s => s.Team.Name).Select(s => {
                int diff = s.PlayerGoals - s.Team.GoalsFor;
                return new[] {E(s.Team.Name), s.PlayerGoals.ToString(CultureInfo.InvariantCulture),
                    s.Team.GoalsFor.ToString(CultureInfo.InvariantCulture), diff.ToString("+0;-0;0"),
                    diff == 0 ? "<span class=\"ok\">OK</span>" : "<span class=\"off\">OFF</span>"};
            }));
        html.AppendLine("</section></main></body></html>");
        await File.WriteAllTextAsync(path, html.ToString(), Encoding.UTF8);
    }

    static void Table(StringBuilder html, string[] headers, IEnumerable<string[]> rows)
    {
        html.AppendLine("<div style=\"overflow:auto\"><table><thead><tr>");
        foreach (string header in headers) html.Append($"<th>{E(header)}</th>");
        html.AppendLine("</tr></thead><tbody>");
        foreach (string[] row in rows)
        {
            html.Append("<tr>");
            foreach (string cell in row) html.Append($"<td>{cell}</td>");
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody></table></div>");
    }

    static string Pct(double num, double den) =>
        den == 0 ? "n/a" : (100.0 * num / den).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    static string E(string value) => WebUtility.HtmlEncode(value);
}

static class Reports
{
    static string Pct(double num, double den) =>
        den == 0 ? "n/a" : (100.0 * num / den).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine(text);
        Console.WriteLine(new string('=', text.Length));
    }

    /// <summary>Report 1: each goal scorer's share of the team's goals for.</summary>
    public static void PlayerShares(List<Player> players, Dictionary<string, Team> teams, int? top)
    {
        Title("1. Player share of team goals" + (top is int n ? $" (top {n} by goals)" : ""));
        Console.WriteLine($"{"#",3}  {"Player",-28}{"Team",-22}{"Team GF",8}{"G",4}{"% of team goals",17}");
        int i = 0;
        foreach (Player p in players.Where(p => p.Goals > 0).Take(top ?? int.MaxValue))
        {
            int gf = teams[p.Team].GoalsFor;
            Console.WriteLine($"{++i,3}  {p.Name,-28}{p.Team,-22}{gf,8}{p.Goals,4}{Pct(p.Goals, gf),17}");
        }
    }

    /// <summary>Report 2: team goal balance, ranked by effective scorers.</summary>
    public static void GoalBalance(List<TeamStats> stats)
    {
        Title("2. Team goal balance, ranked by effective scorers (most balanced first)");
        Console.WriteLine($"{"Rk",2}  {"Team",-22}{"GF",4}{"Scorers",9}{"Top scorer",12}{"Top 3",9}{"Eff. scorers",14}  Top scorer(s)");
        int rank = 0;
        foreach (TeamStats s in stats.OrderByDescending(s => s.EffectiveScorers))
        {
            int gf = s.Team.GoalsFor;
            string leaders = s.TopGoals == 0 ? "" :
                string.Join(", ", s.Roster.Where(p => p.Goals == s.TopGoals).Select(p => p.Name)) + $" ({s.TopGoals})";
            string eff = s.EffectiveScorers.ToString("0.0", CultureInfo.InvariantCulture);
            Console.WriteLine($"{++rank,2}  {s.Team.Name,-22}{gf,4}{s.Scorers,9}{Pct(s.TopGoals, gf),12}{Pct(s.Top3Goals, gf),9}{eff,14}  {leaders}");
        }
    }

    /// <summary>Report 3: do the players' goals add up to the team's GF?</summary>
    public static void Reconciliation(List<TeamStats> stats)
    {
        Title("3. Data check: player goals vs team GF");
        var off = stats.Where(s => s.PlayerGoals != s.Team.GoalsFor).ToList();
        if (off.Count == 0) Console.WriteLine("All teams reconcile.");
        foreach (TeamStats s in off)
            Console.WriteLine($"{s.Team.Name,-22} players {s.PlayerGoals}, team GF {s.Team.GoalsFor} (diff {s.PlayerGoals - s.Team.GoalsFor:+0;-0})");
    }
}
