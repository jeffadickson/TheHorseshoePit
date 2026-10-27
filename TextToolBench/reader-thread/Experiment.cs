using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Checks the GNU grep adapter and measures same-session, interleaved process runs.</summary>
internal static class Experiment
{
    private const int Seed = 20261026;
    private static string Root = "", Bench = "";
    private sealed record Engine(string Name, string File, string? Enabled, int Chunk = 262144, int Slots = 3);
    internal sealed record TaskSpec(string Name, string[] Flags, string Pattern);
    internal sealed record Result(string Task, string Engine, int Round, double Milliseconds,
        double CpuMilliseconds, int ExitCode, long OutputBytes, string Sha256, string Error);
    internal sealed record ExecutableInfo(string Name, string? Enabled, int Chunk, int Slots, string Sha256);
    internal sealed record EnvironmentInfo(int Seed, int Repeats, int Warmups, DateTime Utc,
        string Input, long InputBytes, string InputSha256, ExecutableInfo[] Executables,
        string Runtime, string Os, string Notes);
    internal sealed record Summary(string Task, string Engine, int N, double MedianMs,
        double P25Ms, double P75Ms, double MedianCpuMs);
    internal sealed record Validation(int OracleChecks, int DifferentialChecks, int NegativeChecks,
        int FixtureCases, string Notes);
    private static readonly TaskSpec[] Tasks = [
        new("literal", ["-F"], "Prince"), new("regex", ["-E"], "war|peace"),
        new("count", ["-Fc"], "Prince"), new("nonblank", ["-E"], "[^ \t]"),
        new("absent", ["-Fc"], "TEXTTOOL_NO_SUCH_TOKEN_20261026")];

    /// <summary>Arguments: extension directory, prior benchmark directory, optional --test.</summary>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Root = Path.GetFullPath(args[0]); Bench = Path.GetFullPath(args[1]);
            Require(Directory.Exists(Root) && Directory.Exists(Bench), "Missing experiment or benchmark directory");
            string results = Path.Combine(Root, "results");
            Require(Directory.Exists(results), "Build before verification");
            Engine[] engines = [new("original", "baseline.exe", null),
                new("adapter-disabled", "threaded.exe", null),
                new("queue-64KiB", "threaded.exe", "1", 65536),
                new("queue-256KiB", "threaded.exe", "1"),
                new("queue-1MiB", "threaded.exe", "1", 1048576)];
            await Validate(engines, results);
            if (args.Contains("--test")) return 0;
            string input = Path.Combine(Bench, "data", "32MiB.txt");
            Require(File.Exists(input), "Missing original large corpus");
            var expected = Tasks.ToDictionary(t => t.Name, t => Oracle(File.ReadAllBytes(input), t));
            var environment = new EnvironmentInfo(Seed, 15, 2, DateTime.UtcNow,
                input, new FileInfo(input).Length, Hash(File.ReadAllBytes(input)),
                engines.Select(e => new ExecutableInfo(e.Name, e.Enabled, e.Chunk, e.Slots,
                    Hash(File.ReadAllBytes(Path.Combine(Root, e.File))))).ToArray(),
                Environment.Version.ToString(), Environment.OSVersion.ToString(),
                "Warm-cache process wall time including startup and stdout SHA256; no cache eviction. " +
                    "Host has other active work; shuffled sequential runs reduce but cannot eliminate interference. " +
                    "Queue uses pread and an extra buffer copy; no CPU-cache residency claim.");
            await File.WriteAllTextAsync(Path.Combine(results, "environment.json"),
                JsonSerializer.Serialize(environment, ExperimentJson.Default.EnvironmentInfo));
            var random = new Random(Seed);
            var cells = engines.SelectMany(e => Tasks.Select(t => (e, t))).ToArray();
            var measured = new List<Result>();
            using var log = new StreamWriter(Path.Combine(results, "runs.jsonl"), false, new UTF8Encoding(false));
            for (int round = -2; round < 15; round++)
            {
                foreach (var (engine, task) in cells.OrderBy(_ => random.Next()))
                {
                    Result r = await Run(engine, task, [input], round);
                    var oracle = expected[task.Name];
                    Require(r.OutputBytes == oracle.Bytes && r.Sha256 == oracle.Hash && r.ExitCode == oracle.Exit,
                        $"Timed output mismatch: {engine.Name}/{task.Name}: {r.Error}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(r, ExperimentJson.Default.Result)); await log.FlushAsync();
                    if (round >= 0) measured.Add(r);
                }
                Console.WriteLine($"Round {round}: {cells.Length} verified runs");
            }
            var summary = measured.GroupBy(r => new { r.Task, r.Engine }).Select(g => new Summary(
                g.Key.Task, g.Key.Engine, g.Count(), Quantile(g.Select(r => r.Milliseconds), 0.5),
                Quantile(g.Select(r => r.Milliseconds), 0.25), Quantile(g.Select(r => r.Milliseconds), 0.75),
                Quantile(g.Select(r => r.CpuMilliseconds), 0.5)
            )).OrderBy(r => r.Task).ThenBy(r => r.Engine).ToArray();
            await File.WriteAllTextAsync(Path.Combine(results, "summary.json"), JsonSerializer.Serialize(summary,
                ExperimentJson.Default.SummaryArray));
            var report = new StringBuilder("Task,Engine,N,MedianMs,P25Ms,P75Ms,MedianCpuMs\n");
            foreach (var row in summary) report.AppendLine(FormattableString.Invariant(
                $"{row.Task},{row.Engine},{row.N},{row.MedianMs:F3},{row.P25Ms:F3},{row.P75Ms:F3},{row.MedianCpuMs:F3}"));
            await File.WriteAllTextAsync(Path.Combine(results, "summary.csv"), report.ToString());
            Console.WriteLine(report);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static async Task<Result> Run(Engine engine, TaskSpec task, string[] files, int round,
        byte[]? stdin = null, string? invalidChunk = null)
    {
        var start = new ProcessStartInfo(Path.Combine(Root, engine.File)) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = stdin != null, CreateNoWindow = true };
        start.Environment["PATH"] = Path.Combine(Bench, "tools", "msys64", "usr", "bin")
            + Path.PathSeparator + start.Environment["PATH"];
        start.Environment["LC_ALL"] = "C";
        start.Environment.Remove("GREP_OPTIONS");
        start.Environment.Remove("GREP_READ_AHEAD");
        if (engine.Enabled != null) start.Environment["GREP_READ_AHEAD"] = engine.Enabled;
        start.Environment["GREP_READ_CHUNK"] = invalidChunk ?? engine.Chunk.ToString(CultureInfo.InvariantCulture);
        start.Environment["GREP_READ_SLOTS"] = engine.Slots.ToString(CultureInfo.InvariantCulture);
        foreach (string flag in task.Flags) start.ArgumentList.Add(flag);
        start.ArgumentList.Add("--"); start.ArgumentList.Add(task.Pattern);
        foreach (string file in files) start.ArgumentList.Add(file);
        using var process = new Process { StartInfo = start };
        var watch = Stopwatch.StartNew(); process.Start();
        var error = process.StandardError.ReadToEndAsync();
        // A timeout is essential: early-exit tests must detect a producer stuck on a full queue.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var hashTask = HashOutput(process.StandardOutput.BaseStream, timeout.Token);
        if (stdin != null) { await process.StandardInput.BaseStream.WriteAsync(stdin, timeout.Token); process.StandardInput.Close(); }
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        var output = await hashTask;
        watch.Stop();
        return new(task.Name, engine.Name, round, watch.Elapsed.TotalMilliseconds,
            process.TotalProcessorTime.TotalMilliseconds, process.ExitCode, output.Bytes, output.Hash, await error);
    }

    private static async Task<(long Bytes, string Hash)> HashOutput(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536]; long bytes = 0; int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0) { hash.AppendData(buffer, 0, count); bytes += count; }
        return (bytes, Convert.ToHexString(hash.GetHashAndReset()));
    }

    // Independent ASCII oracle: counts matching lines, including an unterminated
    // final line, and emits grep's LF-terminated text output.
    internal static (long Bytes, string Hash, int Exit) Oracle(byte[] bytes, TaskSpec task)
    {
        string text = Encoding.ASCII.GetString(bytes);
        string[] lines = text.Length == 0 ? [] : text.TrimEndOneNewline().Split('\n');
        var selected = lines.Where(line => task.Name switch {
            "literal" or "count" => line.Contains("Prince", StringComparison.Ordinal),
            "regex" => line.Contains("war", StringComparison.Ordinal) || line.Contains("peace", StringComparison.Ordinal),
            "nonblank" => line.Any(c => c != ' ' && c != '\t'),
            "absent" => line.Contains(task.Pattern, StringComparison.Ordinal),
            _ => throw new ArgumentException("Unknown oracle task") }).ToArray();
        string output = task.Flags.Contains("-Fc") ? selected.Length + "\n" : string.Concat(selected.Select(l => l + "\n"));
        var encoded = Encoding.ASCII.GetBytes(output);
        return (encoded.Length, Hash(encoded), selected.Length == 0 ? 1 : 0);
    }

    private static string TrimEndOneNewline(this string text) => text.EndsWith('\n') ? text[..^1] : text;

    private static async Task Validate(Engine[] engines, string results)
    {
        string fixture = Path.Combine(results, "fixture.txt");
        byte[][] fixtures = [[], "\n \t\n"u8.ToArray(), "Prince Prince\nwar peace\nWAR\n"u8.ToArray(),
            "Prince without final newline"u8.ToArray(), Encoding.ASCII.GetBytes(new string('x', 1048576) + "Prince\npeace\n"),
            Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("Prince\nwar\nnone\n", 10000)))];
        int oracleChecks = 0, differentialChecks = 0, negatives = 0;
        foreach (byte[] content in fixtures)
        {
            await File.WriteAllBytesAsync(fixture, content);
            foreach (TaskSpec task in Tasks)
            {
                var expected = Oracle(content, task);
                foreach (Engine engine in engines)
                {
                    var actual = await Run(engine, task, [fixture], -3);
                    Require(actual.OutputBytes == expected.Bytes && actual.Sha256 == expected.Hash && actual.ExitCode == expected.Exit,
                        $"Oracle failed: {engine.Name}/{task.Name}/{content.Length}"); oracleChecks++;
                }
            }
            TaskSpec[] options = [new("quiet", ["-Fq"], "Prince"), new("max-count", ["-Fm", "1"], "Prince"),
                new("context", ["-Fn", "-C", "1"], "Prince"), new("offset", ["-Fb"], "Prince"),
                new("invert", ["-Fvc"], "Prince"), new("list", ["-Fl"], "Prince"),
                new("nul-record", ["-azFc"], "Prince")];
            foreach (TaskSpec task in options)
            {
                var expected = await Run(engines[0], task, [fixture], -3);
                foreach (Engine engine in engines.Skip(1))
                {
                    Same(await Run(engine, task, [fixture], -3), expected, "Option differential"); differentialChecks++;
                }
            }
        }
        await File.WriteAllBytesAsync(fixture, [0, 0, 80, 114, 105, 110, 99, 101, 10, 0, 255]);
        foreach (TaskSpec task in Tasks)
        {
            var expected = await Run(engines[0], task, [fixture, fixture], -3);
            foreach (Engine engine in engines.Skip(1)) { Same(await Run(engine, task, [fixture, fixture], -3), expected, "Binary/multiple-file differential"); differentialChecks++; }
        }
        foreach (Engine engine in engines)
        {
            var missing = await Run(engine, Tasks[0], [Path.Combine(results, "missing-file")], -3);
            Require(missing.ExitCode == 2, "Missing input must fail"); negatives++;
            var malformed = await Run(engine, new("malformed", ["-E"], "["), [fixture], -3);
            Require(malformed.ExitCode == 2, "Malformed regex must fail"); negatives++;
            var stdin = await Run(engine, Tasks[0], [], -3, "Prince\nnone\n"u8.ToArray());
            var stdinExpected = Oracle("Prince\nnone\n"u8.ToArray(), Tasks[0]);
            Require(stdin.Sha256 == stdinExpected.Hash && stdin.ExitCode == 0, "stdin fallback"); differentialChecks++;
            foreach (string badChunk in new[] { "0", "-1", "junk", "999999999999999999999999" })
            {
                Same(await Run(engine, Tasks[0], [fixture], -3, invalidChunk: badChunk),
                    await Run(engines[0], Tasks[0], [fixture], -3), "Invalid tuning fallback"); negatives++;
            }
        }
        await File.WriteAllTextAsync(Path.Combine(results, "validation.json"), JsonSerializer.Serialize(new Validation(
            oracleChecks, differentialChecks, negatives, fixtures.Length,
            "Native adapter fuzz/property and mutation evidence recorded separately."
        ), ExperimentJson.Default.Validation));
        Console.WriteLine($"PASS {oracleChecks} independent oracle checks; {differentialChecks} differential checks; {negatives} negatives");
    }
    private static void Same(Result actual, Result expected, string message) => Require(actual.ExitCode == expected.ExitCode
        && actual.Sha256 == expected.Sha256 && actual.OutputBytes == expected.OutputBytes, message);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static double Quantile(IEnumerable<double> values, double p) { var a = values.Order().ToArray(); double index = p * (a.Length - 1); int lo = (int)index; return a[lo] + (a[(int)Math.Ceiling(index)] - a[lo]) * (index - lo); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

// Generated metadata keeps the orchestration executable compatible with Native AOT.
[JsonSerializable(typeof(Experiment.Result))]
[JsonSerializable(typeof(Experiment.EnvironmentInfo))]
[JsonSerializable(typeof(Experiment.Summary[]))]
[JsonSerializable(typeof(Experiment.Validation))]
internal partial class ExperimentJson : JsonSerializerContext { }
