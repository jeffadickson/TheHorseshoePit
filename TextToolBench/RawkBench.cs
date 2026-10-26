using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Equivalent AWK scripts across two pinned Rust implementations and contemporaneous GNU awk.</summary>
internal static class RawkBench
{
    const int Seed=20261030;
    static readonly string[] Tasks=["field","words","lines"];
    static readonly string[] Engines=["rawk-stefan","awk-rs-quinn","awk-current"];
    internal static string Executable(string engine)=>Path.GetFullPath(engine switch {
        "rawk-stefan"=>"tools/stefanalfbo-rawk/target/release/rawk.exe",
        "awk-rs-quinn"=>"tools/quinnjr-rawk/target/release/awk-rs.exe",
        "awk-current"=>Path.Combine(Environment.GetEnvironmentVariable("BENCH_GNU_BIN")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","usr","bin"),"awk.exe"),
        "mawk"=>"tools/mawk/bin/mawk.exe",
        "frawk-cranelift"=>"tools/frawk/target/release/frawk.exe",
        "frawk-interp"=>"tools/frawk/target/release/frawk.exe",
        "rawk-stefan-buffered"=>"tools/variants/stefan-buffered/target/release/rawk.exe",
        "awk-rs-quinn-buffered"=>"tools/variants/quinn-buffered/target/release/awk-rs.exe",
        _=>throw new ArgumentException("Unknown engine",nameof(engine))
    });
    internal static string Script(string task)=>task switch {
        "field"=>"NF {print $1}","words"=>"{n+=NF} END {print n+0}","lines"=>"END {print NR+0}",
        _=>throw new ArgumentException("Unknown task",nameof(task))
    };

    /// <summary>Validate before timing; seven rounds and one warmup cover five existing corpus sizes.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            await Validate();if(args.Contains("--test"))return 0;
            string[] paths=Directory.GetFiles("data","*MiB.txt").Concat(Directory.GetFiles("data","small-*KiB.txt")).Select(Path.GetFullPath).ToArray();
            Require(paths.Length==5,"Expected five corpus inputs");
            var expected=paths.SelectMany(p=>Tasks.Select(t=>(p,t))).ToDictionary(x=>x,x=>Oracle(x.p,x.t));
            var metadata=new List<object>();
            foreach(string engine in Engines) {
                string repo=engine=="rawk-stefan"?"tools/stefanalfbo-rawk":"tools/quinnjr-rawk";
                string version=engine=="rawk-stefan"?"rawk-cli "+ManifestVersion(Path.Combine(repo,"crates/rawk-cli/Cargo.toml"))+" (manifest; CLI --version unsupported)":await Command(Executable(engine),["--version"]);
                metadata.Add(new{Engine=engine,Path=Executable(engine),Version=version,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Executable(engine)))),Commit=engine=="awk-current"?null:await Command(@"C:\Program Files\Git\cmd\git.exe",["-C",repo,"rev-parse","HEAD"]),SourceStatus=engine=="awk-current"?null:await Command(@"C:\Program Files\Git\cmd\git.exe",["-C",repo,"status","--porcelain"]),CargoLockSha256=engine=="awk-current"?null:Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo,"Cargo.lock"))))});
            }
            string rustc=Directory.GetFiles("tools/rustup/toolchains","rustc.exe",SearchOption.AllDirectories).Single(p=>p.EndsWith(Path.Combine("bin","rustc.exe"),StringComparison.OrdinalIgnoreCase));
            File.WriteAllText("results/rawk-environment.json",JsonSerializer.Serialize(new{Seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,RustCompiler=await Command(rustc,["-Vv"]),Build="cargo build --release --locked, repository release profiles, x86_64-pc-windows-msvc; no source edits",Tools=metadata,Inputs=paths.Select(p=>new{Path=p,Bytes=new FileInfo(p).Length,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}),Scripts=Tasks.ToDictionary(t=>t,Script),Timing="Sequential interleaved fresh processes, warm cache, stdout hashing included; 120-second per-process timeout"},new JsonSerializerOptions{WriteIndented=true}));
            var cells=paths.SelectMany(p=>Tasks.SelectMany(t=>Engines.Select(e=>(p,t,e)))).ToArray();
            var random=new Random(Seed);
            using var log=new StreamWriter("results/rawk-runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                foreach(var (path,task,engine) in cells.OrderBy(_=>random.Next())) {
                    var r=await Run(path,task,engine,round);var oracle=expected[(path,task)];
                    Require(r.ExitCode==0&&r.OutputBytes==oracle.Bytes&&r.Sha256==oracle.Hash,$"Corpus mismatch {task}/{engine}: {r.Error}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(r));
                }
                Console.WriteLine($"AWK round {round+1}/7: 45 verified runs");
            }
            return 0;
        } catch(Exception ex) {File.WriteAllText("results/rawk-failure.txt",ex.ToString());Console.Error.WriteLine(ex);return 1;}
    }

    /// <summary>Drain both pipes concurrently; cancel and kill a hung interpreter instead of waiting indefinitely.</summary>
    internal static async Task<Row> Run(string path,string task,string engine,int round,string? script=null)
    {
        var info=new ProcessStartInfo(Executable(engine)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        // Native Windows mawk needs binary stdout to match the benchmark's explicit LF convention.
        if(engine=="mawk"){info.ArgumentList.Add("-v");info.ArgumentList.Add("BINMODE=3");}
        if(engine=="frawk-cranelift"){info.ArgumentList.Add("-B");info.ArgumentList.Add("cranelift");}
        if(engine=="frawk-interp"){info.ArgumentList.Add("-B");info.ArgumentList.Add("interp");}
        info.ArgumentList.Add(script??Script(task));info.ArgumentList.Add(path);info.Environment["LC_ALL"]="C";
        using var p=new Process{StartInfo=info};using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var watch=Stopwatch.StartNew();p.Start();var errors=p.StandardError.ReadToEndAsync(timeout.Token);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long bytes=0;int n;
        try {
            while((n=await p.StandardOutput.BaseStream.ReadAsync(buffer,timeout.Token))>0){hash.AppendData(buffer,0,n);bytes+=n;}
            await p.WaitForExitAsync(timeout.Token);watch.Stop();string error=await errors;
            return new(task,engine,new FileInfo(path).Exists?new FileInfo(path).Length:0,round,watch.Elapsed.TotalMilliseconds,bytes,Convert.ToHexString(hash.GetHashAndReset()),p.TotalProcessorTime.TotalMilliseconds,p.ExitCode,error);
        } catch(OperationCanceledException){if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();throw new TimeoutException($"AWK exceeded 120 seconds: {engine}/{task}");}
    }

    /// <summary>Reference uses independent ASCII space/tab field splitting, ordinal strings, and LF output.</summary>
    internal static (long Bytes,string Hash) Oracle(string path,string task)
    {
        _=Script(task);var output=new StringBuilder();long n=0;
        foreach(string line in File.ReadLines(path)) {
            string[] fields=line.Split([' ','\t'],StringSplitOptions.RemoveEmptyEntries);
            if(task=="field"){if(fields.Length>0)output.Append(fields[0]).Append('\n');}
            else n+=task=="words"?fields.Length:1;
        }
        if(task!="field")output.Append(n).Append('\n');
        byte[] bytes=Encoding.UTF8.GetBytes(output.ToString());return(bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Cross-check edge/fuzz inputs, malformed programs/missing files and deliberately wrong AWK programs.</summary>
    static async Task Validate()
    {
        var fixtures=new List<string>{"","\n \t\n","Prince Prince\nwar peace\n \talpha\tbeta\n","no final newline",new string('x',131072)+" last\n"};
        var random=new Random(Seed);
        for(int i=0;i<3;i++)fixtures.Add(string.Join('\n',Enumerable.Range(0,100).Select(_=>string.Join(" \t",Enumerable.Range(0,random.Next(0,9)).Select(_=>new[]{"Prince","war","peace","0","!","abc"}[random.Next(6)]))))+"\n");
        string path=Path.GetFullPath("data/rawk-fixture.txt");int checks=0,negative=0,killed=0;
        foreach(string fixture in fixtures) {
            File.WriteAllText(path,fixture,new UTF8Encoding(false));
            foreach(string task in Tasks)foreach(string engine in Engines) {
                var r=await Run(path,task,engine,-2);var expected=Oracle(path,task);
                Require(r.ExitCode==0&&r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"Fixture {checks}/{engine}/{task}, expected {expected.Bytes} bytes / {expected.Hash}, got {r.OutputBytes} / {r.Sha256}, exit {r.ExitCode}: {r.Error}");checks++;
            }
        }
        foreach(string engine in Engines) {
            var missing=await Run(Path.GetFullPath("data/absent-rawk.txt"),"lines",engine,-2);Require(missing.ExitCode!=0,$"Missing file accepted by {engine}");negative++;
            var malformed=await Run(path,"lines",engine,-2,"BEGIN {");Require(malformed.ExitCode!=0,$"Malformed program accepted by {engine}");negative++;
        }
        bool invalid=false;try{Script("invalid");}catch(ArgumentException){invalid=true;}Require(invalid,"Invalid task accepted");negative++;
        File.WriteAllText(path,"alpha beta gamma\none two\n\n",new UTF8Encoding(false));
        foreach(string engine in Engines)foreach(var (task,script) in new[]{("field","NF {print $2}"),("words","{n+=1} END {print n+0}"),("lines","END {print NR+1}")}) {
            var r=await Run(path,task,engine,-2,script);var expected=Oracle(path,task);
            if(r.ExitCode==0&&(r.OutputBytes!=expected.Bytes||r.Sha256!=expected.Hash))killed++;
        }
        Require(killed==9,"Surviving or nonexecuting mutant");
        File.WriteAllText("results/rawk-validation.json",JsonSerializer.Serialize(new{Seed,EquivalenceChecks=checks,FixedFixtures=5,RandomFixtures=3,NegativeChecks=negative,MutantsKilled=killed,MutantsTotal=9,Scope="ASCII/LF; empty, whitespace, missing final newline, long line, seeded bounded fuzz; wrong field, wrong word aggregation, off-by-one line count per implementation"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} equivalence checks, {negative} negatives, {killed}/9 mutants killed");
    }

    static async Task<string> Command(string exe,string[] arguments)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string a in arguments)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var error=p.StandardError.ReadToEndAsync();string output=await p.StandardOutput.ReadToEndAsync();await p.WaitForExitAsync();Require(p.ExitCode==0,await error);return output.Trim();
    }
    // This rawk release omits a CLI version flag, so retain its checked-out manifest version instead.
    static string ManifestVersion(string path)=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path),"(?m)^version\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

    /// <summary>Raw observation extends the common schema with diagnostic process status.</summary>
    public record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds,int ExitCode,string Error);
}
