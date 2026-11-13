#!/usr/bin/env dotnet
// HockeyBalance - goal-share and goal-balance reports for a league.
// Goals only: assists are ignored because they are tracked unreliably.
//
// Usage (.NET 10 SDK or later, no project file needed):
//   dotnet run hockeybalance.cs                              live SCAHA data, 10U B Regular Season
//   dotnet run hockeybalance.cs -- --schedule "12U A Regular Season"
//   dotnet run hockeybalance.cs -- teams.txt players.txt     offline, from pasted tables
//
// Options:
//   --schedule NAME   schedule to fetch, as shown in the site's "Select a Schedule" list
//                     (default "10U B Regular Season"; the site's current season is used)
//   --top N           limit report 1 (player shares) to the first N players by goals
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

int? top = null;
string schedule = "10U B Regular Season";
string? team = null;
string baseUrl = "https://www.scaha.net/scaha/";
var files = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    if (a is "--top" or "--schedule" or "--team" or "--base-url")
    {
        if (i + 1 >= args.Length) return Usage($"{a} needs a value.");
        string v = args[++i];
        if (a == "--schedule") schedule = v;
        else if (a == "--team") team = v;
        else if (a == "--base-url") baseUrl = v.EndsWith('/') ? v : v + "/";
        else if (int.TryParse(v, out int n) && n > 0) top = n;
        else return Usage("--top needs a positive integer.");
    }
    else if (a.StartsWith("--")) return Usage($"Unknown option {a}.");
    else files.Add(a);
}
if (files.Count is not (0 or 2)) return Usage("Give both a teams file and a players file, or neither to fetch live data.");

List<string[]> teamRows, playerRows;
try
{
    if (files.Count == 2)
    {
        teamRows = Parser.FileRows(files[0]);
        playerRows = Parser.FileRows(files[1]);
    }
    else
    {
        Console.WriteLine($"Fetching \"{schedule}\" from {baseUrl} ...");
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

Reports.PlayerShares(players, teams, top);
Reports.GoalBalance(stats);
Reports.Reconciliation(stats);
return 0;

static int Usage(string error)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Usage: dotnet run hockeybalance.cs -- [<teams file> <players file>] [--schedule NAME] [--team NAME] [--top N] [--base-url URL]");
    return 1;
}

static bool TeamMatches(string actual, string requested)
{
    static string Key(string value) =>
        Regex.Replace(value, "[^A-Za-z0-9]", "").ToUpperInvariant();

    string actualKey = Key(actual);
    string requestedKey = Key(requested);
    return requestedKey == actualKey || requestedKey.EndsWith(actualKey, StringComparison.Ordinal);
}

record Team(string Name, int GoalsFor);

record Player(string Name, string Team, int Goals);

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
            if (f.Length >= 10 && int.TryParse(f[7], out int gf))
                teams[f[1]] = new Team(f[1], gf);
        if (teams.Count == 0) throw new InvalidDataException("No team rows found.");
        return teams;
    }

    /// <summary>Player rows: #, Name, Team, GP, G, A, Pts, PIMS.</summary>
    public static List<Player> Players(List<string[]> rows)
    {
        var players = new List<Player>();
        foreach (string[] f in rows)
            if (f.Length >= 8 && int.TryParse(f[4], out int g))
                players.Add(new Player(f[1], f[2], g));
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
