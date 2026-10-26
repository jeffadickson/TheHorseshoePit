using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Search-only extension with interleaved ripgrep and GNU grep measurements.</summary>
internal static class RipgrepBench
{
    const int Seed=20261028;
    static readonly string[] Tasks=["literal","regex","count","nonblank"];
    static readonly string[] Engines=["ripgrep","grep-current"];
    static readonly string Rg=ResolveRipgrep();
    static readonly string Grep=Path.Combine(Environment.GetEnvironmentVariable("BENCH_GNU_BIN")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","usr","bin"),"grep.exe");

    /// <summary>Validate first, then run one warmup and seven randomized sequential rounds per cell.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            Directory.CreateDirectory("results"); Directory.CreateDirectory("data");
            await Validate();
            if(args.Contains("--small")) { await Small(args.Contains("--test")); return 0; }
            if(args.Contains("--test")) return 0;
            var reference=File.ReadLines("results/runs.jsonl").Select(s=>JsonSerializer.Deserialize<Row>(s)!).Where(r=>r.Engine=="gnu"&&r.Round==0&&Tasks.Contains(r.Task)).ToDictionary(r=>(r.InputBytes,r.Task));
            var cells=Directory.GetFiles("data","*MiB.txt").SelectMany(p=>Tasks.SelectMany(t=>Engines.Select(e=>(Path.GetFullPath(p),t,e)))).ToArray();
            Require(cells.Length==24,"Expected three input sizes");
            File.WriteAllText("results/ripgrep-environment.json",JsonSerializer.Serialize(new{Seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,RipgrepPath=Rg,RipgrepVersion=await Version(Rg),RipgrepSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Rg))),GrepPath=Grep,GrepVersion=await Version(Grep),Timing="Fresh process through stdout hashing and exit; warm cache; interleaved native tools; original PowerShell measurements retained"},new JsonSerializerOptions{WriteIndented=true}));
            var random=new Random(Seed);
            using var log=new StreamWriter("results/ripgrep-runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                foreach(var (path,task,engine) in cells.OrderBy(_=>random.Next())) {
                    var r=await Run(path,task,engine,round);var expected=reference[(r.InputBytes,task)];
                    Require(r.OutputBytes==expected.OutputBytes&&r.Sha256==expected.Sha256,$"Corpus mismatch: {task}/{engine}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(r));
                }
                Console.WriteLine($"Native search round {round+1}/7: 24 verified runs");
            }
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }

    /// <summary>Use explicit case-sensitive text semantics and emit zero for count-with-no-matches.</summary>
    static string[] Arguments(string task,string engine,string path,string? mutation)
    {
        string pattern=task switch {"literal" or "count"=>"Prince","regex"=>"war|peace","nonblank"=>"[^ \t]",_=>throw new ArgumentException("Unknown task",nameof(task))};
        if(mutation=="literal") pattern="prince";
        var a=new List<string>();
        if(engine=="ripgrep") a.AddRange(["--no-config","--text","--color","never","--no-heading","--no-line-number","--case-sensitive"]);
        else if(engine!="grep-current") throw new ArgumentException("Unknown engine",nameof(engine));
        a.Add(task is "literal" or "count"?"-F":"-E");
        // ripgrep regex mode is its default; -E means encoding there, unlike grep.
        if(engine=="ripgrep"&&a[^1]=="-E") a.RemoveAt(a.Count-1);
        if(task=="count") {a.Add(mutation=="count"&&engine=="ripgrep"?"--count-matches":"-c");if(engine=="ripgrep")a.Add("--include-zero");}
        if(mutation=="regex")a.Add("-i");
        a.AddRange(["--",pattern,path]);return a.ToArray();
    }

    /// <summary>Same wall-time boundary and streaming SHA-256 sink as the existing benchmark.</summary>
    static async Task<Row> Run(string path,string task,string engine,int round,string? mutation=null)
    {
        bool powershell=engine is "pipeline" or "reader" or "selectstring";
        string pwsh=Environment.GetEnvironmentVariable("BENCH_PWSH") ?? "pwsh.exe";
        var info=new ProcessStartInfo(powershell?pwsh:engine=="ripgrep"?Rg:Grep){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        if(powershell) {
            foreach(string a in new[]{"-NoLogo","-NoProfile","-NonInteractive","-File",Path.GetFullPath(engine=="selectstring"?"select-string.ps1":"worker.ps1"),"-Path",path,"-Task",task})info.ArgumentList.Add(a);
            if(engine!="selectstring"){info.ArgumentList.Add("-Style");info.ArgumentList.Add(engine);}
        } else foreach(string a in Arguments(task,engine,path,mutation))info.ArgumentList.Add(a);
        info.Environment["LC_ALL"]="C";
        using var p=new Process{StartInfo=info};var watch=Stopwatch.StartNew();p.Start();
        var errors=p.StandardError.ReadToEndAsync();using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer=new byte[65536];long bytes=0;int n;
        while((n=await p.StandardOutput.BaseStream.ReadAsync(buffer))>0){hash.AppendData(buffer,0,n);bytes+=n;}
        await p.WaitForExitAsync();watch.Stop();string error=await errors;
        Require(p.ExitCode==0||(!powershell&&p.ExitCode==1),$"Exit {p.ExitCode}: {error}");
        return new(task,engine,new FileInfo(path).Length,round,watch.Elapsed.TotalMilliseconds,bytes,Convert.ToHexString(hash.GetHashAndReset()),p.TotalProcessorTime.TotalMilliseconds);
    }

    /// <summary>Independent ordinal-string reference for the ASCII benchmark's four search operations.</summary>
    static (long Bytes,string Hash) Oracle(string path,string task)
    {
        Require(Tasks.Contains(task),"Unknown reference task");
        var lines=File.ReadAllLines(path);
        string output=task=="count"?lines.Count(l=>l.Contains("Prince",StringComparison.Ordinal))+"\n":string.Concat(lines.Where(l=>task switch {
            "literal"=>l.Contains("Prince",StringComparison.Ordinal),
            "regex"=>l.Contains("war",StringComparison.Ordinal)||l.Contains("peace",StringComparison.Ordinal),
            _=>l.Any(c=>c!=' '&&c!='\t')
        }).Select(l=>l+"\n"));
        byte[] bytes=Encoding.UTF8.GetBytes(output);return(bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Exercise edge cases, seeded bounded fuzz, failures and actual argument mutations.</summary>
    static async Task Validate()
    {
        var fixtures=new List<string>{"","\n \t\n","Prince Prince\nwar peace\nWAR PEACE\nprince\n","no final newline",new string('x',131072)+"Prince\n"};
        var random=new Random(Seed);
        for(int i=0;i<3;i++)fixtures.Add(string.Join('\n',Enumerable.Range(0,100).Select(_=>string.Join(" \t",Enumerable.Range(0,random.Next(0,9)).Select(_=>new[]{"Prince","prince","war","peace","WAR","0","!","abc"}[random.Next(8)]))))+"\n");
        int checks=0,killed=0,negative=0;string path=Path.GetFullPath("data/ripgrep-test.txt");
        foreach(string fixture in fixtures) {
            File.WriteAllText(path,fixture,new UTF8Encoding(false));
            foreach(string task in Tasks)foreach(string engine in Engines) {
                var r=await Run(path,task,engine,-2);var expected=Oracle(path,task);
                Require(r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"Fixture mismatch {checks}/{task}/{engine}");checks++;
            }
        }
        foreach(string engine in Engines) {
            try{await Run(Path.GetFullPath("data/does-not-exist.txt"),"literal",engine,-2);throw new Exception("Missing file accepted");}catch(InvalidOperationException){negative++;}
        }
        try{Arguments("invalid","ripgrep",path,null);throw new Exception("Unknown task accepted");}catch(ArgumentException){negative++;}
        try{Arguments("literal","invalid",path,null);throw new Exception("Unknown engine accepted");}catch(ArgumentException){negative++;}
        File.WriteAllText(path,"Prince Prince\nwar peace\nWAR\n",new UTF8Encoding(false));
        foreach(string task in new[]{"literal","regex","count"}) {
            var r=await Run(path,task,"ripgrep",-2,task);var expected=Oracle(path,task);
            if(r.OutputBytes!=expected.Bytes||r.Sha256!=expected.Hash)killed++;
        }
        Require(killed==3,"Surviving argument mutant");
        File.WriteAllText("results/ripgrep-validation.json",JsonSerializer.Serialize(new{Seed,EquivalenceChecks=checks,FixedFixtures=5,RandomFixtures=3,NegativeChecks=negative,MutantsKilled=killed,MutantsTotal=3,Scope="ASCII/LF fixtures; empty, no matches, case, repeated matches, missing final newline, long line; bounded randomized fuzz; targeted literal/case/count argument mutations"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} native equivalence checks, {negative} negatives, {killed}/3 mutants killed");
    }

    /// <summary>Compare five implementations on contiguous 1/64 KiB corpus slices, including startup.</summary>
    static async Task Small(bool testOnly)
    {
        const int seed=20261029;
        string[] tasks=["literal","regex","count"];
        string[] engines=["ripgrep","grep-current","pipeline","reader","selectstring"];
        var random=new Random(seed);
        string[] fixtures=["", "Prince Prince\nwar peace\nWAR\n", " \t\nno matching text\n", string.Join('\n',Enumerable.Range(0,100).Select(_=>string.Join(" \t",Enumerable.Range(0,random.Next(0,8)).Select(_=>new[]{"Prince","prince","war","peace","WAR","x"}[random.Next(6)]))))+"\n"];
        int checks=0;
        string fixturePath=Path.GetFullPath("data/small-fixture.txt");
        foreach(string fixture in fixtures) {
            File.WriteAllText(fixturePath,fixture,new UTF8Encoding(false));
            foreach(string task in tasks)foreach(string engine in engines) {
                var r=await Run(fixturePath,task,engine,-2);var expected=Oracle(fixturePath,task);
                Require(r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"Small fixture mismatch {task}/{engine}");checks++;
            }
        }
        File.WriteAllText("results/small-validation.json",JsonSerializer.Serialize(new{Seed=seed,EquivalenceChecks=checks,FixedFixtures=3,RandomFixtures=1,NativeNegativeAndMutationEvidence="ripgrep-validation.json"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} small-suite fixture checks");
        if(testOnly)return;
        string source=File.ReadAllText("data/1MiB.txt");
        // Start near a real literal match, retain contiguous corpus text, and end at a complete line.
        int first=source.IndexOf("Prince",StringComparison.Ordinal);Require(first>=0,"Source must contain Prince");
        int offset=source.LastIndexOf('\n',Math.Max(0,first-256))+1;
        var paths=new List<string>();
        foreach(int kib in new[]{1,64}) {
            int end=source.IndexOf('\n',offset+kib*1024);Require(end>=0,"Source too short");
            string path=Path.GetFullPath($"data/small-{kib}KiB.txt");File.WriteAllText(path,source[offset..(end+1)],new UTF8Encoding(false));paths.Add(path);
        }
        File.WriteAllText("results/small-environment.json",JsonSerializer.Serialize(new{Seed=seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,Source="data/1MiB.txt",SourceOffset=offset,Inputs=paths.Select(p=>new{Path=p,Bytes=new FileInfo(p).Length,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))),MatchingLines=File.ReadLines(p).Count(l=>l.Contains("Prince",StringComparison.Ordinal)),RegexMatchingLines=File.ReadLines(p).Count(l=>l.Contains("war",StringComparison.Ordinal)||l.Contains("peace",StringComparison.Ordinal))}),Timing="Sequential, shuffled; fresh processes including startup and stdout hashing; warm cache; all five engines interleaved"},new JsonSerializerOptions{WriteIndented=true}));
        var cells=paths.SelectMany(p=>tasks.SelectMany(t=>engines.Select(e=>(p,t,e)))) .ToArray();
        var expectedOutputs=paths.SelectMany(p=>tasks.Select(t=>(p,t))).ToDictionary(x=>x,x=>Oracle(x.p,x.t));
        using var log=new StreamWriter("results/small-runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
        for(int round=-1;round<7;round++) {
            foreach(var (path,task,engine) in cells.OrderBy(_=>random.Next())) {
                var r=await Run(path,task,engine,round);var expected=expectedOutputs[(path,task)];
                Require(r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"Small corpus mismatch {task}/{engine}");
                await log.WriteLineAsync(JsonSerializer.Serialize(r));
            }
            Console.WriteLine($"Small-file round {round+1}/7: 30 verified runs");
        }
    }

    static async Task<string> Version(string exe)
    {
        using var p=Process.Start(new ProcessStartInfo(exe,"--version"){RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true})!;
        string output=await p.StandardOutput.ReadToEndAsync();await p.WaitForExitAsync();Require(p.ExitCode==0,"Version command failed");return output.Trim();
    }
    // Winget may add the portable package directory to PATH when it cannot create a link.
    // Restrict discovery to the requested package so Codex's bundled rg is never benchmarked accidentally.
    static string ResolveRipgrep()
    {
        string? configured=Environment.GetEnvironmentVariable("BENCH_RG");
        if(!string.IsNullOrWhiteSpace(configured))return configured;
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WinGet");
        string link=Path.Combine(root,"Links","rg.exe");if(File.Exists(link))return link;
        string package=Path.Combine(root,"Packages","BurntSushi.ripgrep.MSVC_Microsoft.Winget.Source_8wekyb3d8bbwe");
        return Directory.GetFiles(package,"rg.exe",SearchOption.AllDirectories).Single();
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

    /// <summary>Raw observation using the original benchmark's schema; time is in milliseconds.</summary>
    public record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds);
}
