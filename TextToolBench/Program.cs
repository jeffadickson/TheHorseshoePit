using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Reproducible, sequential end-to-end text-tool benchmark; see requirements.sdoc.</summary>
internal static class Program
{
    static readonly string[] Tasks = ["literal", "regex", "nonblank", "replace", "field", "words", "lines", "count"];
    static readonly string[] Engines = ["gnu", "pipeline", "reader"];
    static readonly string Bin = Environment.GetEnvironmentVariable("BENCH_GNU_BIN") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","usr","bin");
    static readonly string Pwsh = Environment.GetEnvironmentVariable("BENCH_PWSH") ?? "pwsh.exe";
    const int Seed = 20261026;
    record Result(string Task, string Engine, long InputBytes, int Round, double Milliseconds, long OutputBytes, string Sha256, double CpuMilliseconds);
    record Digest(long Bytes, string Hash);

    /// <summary>Runs validation or a seven-repeat matrix. Timed runs never overlap.</summary>
    static async Task<int> Main(string[] args)
    {
        try {
            Directory.CreateDirectory("results"); Directory.CreateDirectory("data");
            if (args.Contains("--test")) { await Test(); return 0; }
            var source = File.ReadAllText("source.txt").Replace("\r\n", "\n").Replace("\r", "\n");
            // ASCII substitution removes locale/Unicode regex differences; preserve tabs and LF.
            source = new string(source.Where(c => c == '\n' || c == '\t' || c >= 32).Select(c => c <= 126 ? c : '?').ToArray()).TrimStart('\uFEFF');
            if (!source.EndsWith('\n')) source += "\n";
            var inputs = new List<string>();
            foreach (int mib in new[] { 1, 8, 32 }) {
                string path = Path.GetFullPath($"data/{mib}MiB.txt");
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false))) {
                    long written = 0;
                    while (written < mib * 1048576L) foreach (string line in source.Split('\n').SkipLast(1)) {
                        writer.Write(line); writer.Write('\n'); written += line.Length + 1;
                        if (written >= mib * 1048576L) break;
                    }
                }
                inputs.Add(path);
            }
            // Dependency/bootstrap workflows can materialize deterministic inputs without timing tools.
            if(args.Contains("--prepare")){Console.WriteLine("Prepared three corpus sizes");return 0;}
            var expected = inputs.SelectMany(p => Tasks.Select(t => (p,t))).ToDictionary(x => x, x => Oracle(x.p,x.t));
            File.WriteAllText("results/environment.json", JsonSerializer.Serialize(new { Seed, Repeats=7, Warmups=1, Runtime=Environment.Version.ToString(), OS=Environment.OSVersion.ToString(), LogicalProcessors=Environment.ProcessorCount, SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("source.txt"))), Utc=DateTime.UtcNow, Inputs=inputs.Select(p => new { Path=p, Bytes=new FileInfo(p).Length, Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }) },new JsonSerializerOptions {WriteIndented=true}));
            var cells = inputs.SelectMany(p => Tasks.SelectMany(t => Engines.Select(e => (p,t,e)))).ToArray();
            var rng = new Random(Seed);
            using var log = new StreamWriter("results/runs.jsonl",false,new UTF8Encoding(false)) {AutoFlush=true};
            for (int round = -1; round < 7; round++) {
                foreach (var (p,t,e) in cells.OrderBy(_ => rng.Next())) {
                    var result = await Run(p,t,e,round);
                    Require(new Digest(result.OutputBytes,result.Sha256) == expected[(p,t)], $"Output mismatch {p}/{t}/{e}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(result));
                }
                Console.WriteLine($"Completed {(round == -1 ? "warmup" : $"round {round+1}/7")}: {cells.Length} verified runs");
            }
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>Independent UTF-8/LF reference; fields are sequences separated by ASCII spaces or tabs.</summary>
    static Digest Oracle(string path, string task)
    {
        var output = new StringBuilder(); long total = 0;
        foreach (string line in File.ReadLines(path)) {
            var fields = line.Split([' ', '\t'],StringSplitOptions.RemoveEmptyEntries);
            switch(task) {
                case "literal": if (line.Contains("Prince",StringComparison.Ordinal)) output.Append(line).Append('\n'); break;
                case "regex": if (line.Contains("war",StringComparison.Ordinal) || line.Contains("peace",StringComparison.Ordinal)) output.Append(line).Append('\n'); break;
                case "nonblank": if (fields.Length > 0) output.Append(line).Append('\n'); break;
                case "replace": output.Append(line.Replace("Prince","Count",StringComparison.Ordinal)).Append('\n'); break;
                case "field": if (fields.Length > 0) output.Append(fields[0]).Append('\n'); break;
                case "words": total += fields.Length; break;
                case "lines": total++; break;
                case "count": if (line.Contains("Prince",StringComparison.Ordinal)) total++; break;
                default: throw new ArgumentException("Unknown task",nameof(task));
            }
        }
        if (task is "words" or "lines" or "count") output.Append(total).Append('\n');
        var bytes=Encoding.UTF8.GetBytes(output.ToString());
        return new(bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Includes startup, processing, pipe transfer and hashing; stderr is drained concurrently.</summary>
    static async Task<Result> Run(string path,string task,string engine,int round,string worker="worker.ps1")
    {
        var info=new ProcessStartInfo { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
        if (engine == "gnu") {
            (string exe,string[] arguments) spec=task switch {
                "literal" => ("grep.exe", new string[] {"-F","Prince",path}),
                "regex" => ("grep.exe", new string[] {"-E","war|peace",path}),
                "nonblank" => ("grep.exe", new string[] {"-E","[^ \t]",path}),
                "replace" => ("sed.exe", new string[] {"s/Prince/Count/g",path}),
                "field" => ("awk.exe", new string[] {"NF {print $1}",path}),
                "words" => ("awk.exe", new string[] {"{n+=NF} END {print n+0}",path}),
                "lines" => ("awk.exe", new string[] {"END {print NR+0}",path}),
                "count" => ("grep.exe", new string[] {"-F","-c","Prince",path}),
                _ => throw new ArgumentException("Unknown task",nameof(task))
            };
            info.FileName=Path.Combine(Bin,spec.exe); foreach(var a in spec.arguments) info.ArgumentList.Add(a);
        } else {
            info.FileName=Pwsh;
            foreach(var a in new[]{"-NoLogo","-NoProfile","-NonInteractive","-File",Path.GetFullPath(worker),"-Path",path,"-Task",task,"-Style",engine}) info.ArgumentList.Add(a);
        }
        info.Environment["LC_ALL"]="C";
        using var process=new Process {StartInfo=info};
        var sw=Stopwatch.StartNew(); process.Start();
        var errors=process.StandardError.ReadToEndAsync();
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer=new byte[65536]; long count=0; int read;
        while((read=await process.StandardOutput.BaseStream.ReadAsync(buffer))>0) { hash.AppendData(buffer,0,read); count+=read; }
        await process.WaitForExitAsync(); sw.Stop();
        var error=await errors;
        bool noMatch=engine=="gnu" && task is "literal" or "regex" or "nonblank" or "count" && process.ExitCode==1;
        Require(process.ExitCode==0 || noMatch,$"Exit {process.ExitCode}: {error}");
        return new(task,engine,new FileInfo(path).Length,round,sw.Elapsed.TotalMilliseconds,count,Convert.ToHexString(hash.GetHashAndReset()),process.TotalProcessorTime.TotalMilliseconds);
    }

    /// <summary>Fixed edges and reproducible ASCII fuzz inputs exercise every tool against the reference.</summary>
    static async Task Test()
    {
        string[] fixedCases=["", "\n \t\n", "Prince Prince\nwar peace\nWAR PEACE\n  alpha\tbeta\n", "no final newline", new string('x',131072)+"Prince\n"];
        int checks=0;
        var rng=new Random(Seed);
        var cases=fixedCases.ToList();
        for(int i=0;i<3;i++) cases.Add(string.Join('\n',Enumerable.Range(0,100).Select(_ => string.Join(" \t",Enumerable.Range(0,rng.Next(0,9)).Select(_ => new[]{"Prince","prince","war","peace","WAR","0","!","abc"}[rng.Next(8)]))))+"\n");
        foreach(var content in cases) {
            string path=Path.GetFullPath("data/test.txt"); File.WriteAllText(path,content,new UTF8Encoding(false));
            foreach(var task in Tasks) foreach(var engine in Engines) {
                var r=await Run(path,task,engine,0); if (content == "no final newline" && task == "replace" && engine == "gnu") { Require(new Digest(r.OutputBytes,r.Sha256)!=Oracle(path,task),"Expected final-newline difference missing"); continue; } Require(new Digest(r.OutputBytes,r.Sha256)==Oracle(path,task),$"Fixture {checks}/{task}/{engine}"); checks++;
            }
        }
        bool rejected=false; try { Oracle("data/test.txt","invalid"); } catch(ArgumentException) { rejected=true; } Require(rejected,"Invalid task accepted");
        rejected=false; try { await Run(Path.GetFullPath("data/missing.txt"),"lines","gnu",0); } catch(InvalidOperationException) { rejected=true; } Require(rejected,"Missing input accepted");
        // Real worker mutants must be caught by the independently computed digest.
        string fixture=Path.GetFullPath("data/test.txt"); File.WriteAllText(fixture,"Prince Prince\nwar peace\nWAR\n",new UTF8Encoding(false));
        string original=File.ReadAllText("worker.ps1"); int killed=0;
        foreach(var (from,to,task) in new[]{(".Contains('Prince')",".Contains('prince')","literal"),("-cmatch 'war|peace'","-match 'war|peace'","regex"),(".Replace('Prince','Count')",".Replace('Prince','Duke')","replace")}) {
            File.WriteAllText("data/mutant.ps1",original.Replace(from,to));
            var r=await Run(fixture,task,"pipeline",0,"data/mutant.ps1");
            if(new Digest(r.OutputBytes,r.Sha256)!=Oracle(fixture,task)) killed++;
        }
        Require(killed==3,"Surviving worker mutant");
        File.WriteAllText("results/validation.json",JsonSerializer.Serialize(new { Seed, EquivalenceChecks=checks, FixedCases=fixedCases.Length, RandomCases=3, NegativeChecks=2, MutantsKilled=killed, MutantsTotal=3, Scope="ASCII, LF, spaces/tabs; includes empty input, no trailing newline, long line. UTF-8/non-ASCII and CRLF performance are outside scope." },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} equivalence checks, 2 negative checks, {killed}/3 mutants killed.");
    }

    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
