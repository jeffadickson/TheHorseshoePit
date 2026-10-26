using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Same-source GNU grep O2/O3 experiment with the installed binary as a separate reference.</summary>
internal static class GrepBuildBench
{
    const int Seed=20261104;
    static readonly string[] Tasks=["literal","regex","count","nonblank"];
    static readonly string[] Engines=["grep-built-o2","grep-native-o3","grep-installed-fresh"];
    static string Exe(string engine)=>Path.GetFullPath(engine switch {
        "grep-built-o2"=>"tools/grep-build/generic-o2/src/grep.exe",
        "grep-native-o3"=>"tools/grep-build/native-o3/src/grep.exe",
        "grep-installed-fresh"=>Path.Combine(Environment.GetEnvironmentVariable("BENCH_GNU_BIN")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","usr","bin"),"grep.exe"),
        _=>throw new ArgumentException("Unknown engine")
    });

    /// <summary>Fixed/seeded checks precede 60 cells, one warmup and seven measured shuffled rounds.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            Directory.CreateDirectory("results/grep-tuned");await Validate();if(args.Contains("--test"))return 0;
            var paths=Directory.GetFiles("data","*MiB.txt").Concat(Directory.GetFiles("data","small-*KiB.txt")).Select(Path.GetFullPath).ToArray();Require(paths.Length==5,"Expected five input files");
            var expected=paths.SelectMany(p=>Tasks.Select(t=>(p,t))).ToDictionary(x=>x,x=>Oracle(x.p,x.t));
            File.WriteAllText("results/grep-tuned/environment.json",JsonSerializer.Serialize(new{Seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,Tools=Engines.Select(e=>new{Engine=e,Path=Exe(e),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Exe(e))))}),O2Flags=File.ReadAllText("results/grep-tuned/generic-o2.flags.txt"),O3Flags=File.ReadAllText("results/grep-tuned/native-o3.flags.txt"),Compiler=File.ReadAllText("results/grep-tuned/compiler.txt"),Notes="O2/O3 share source, compiler, configure options and local MSYS2 runtime. Installed grep has older GCC/runtime. Native build is specific to this processor. Fresh-process wall time includes runtime startup and stdout hashing."},new JsonSerializerOptions{WriteIndented=true}));
            var cells=paths.SelectMany(p=>Tasks.SelectMany(t=>Engines.Select(e=>(p,t,e)))).ToArray();var random=new Random(Seed);
            using var log=new StreamWriter("results/grep-tuned/runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                foreach(var (path,task,engine) in cells.OrderBy(_=>random.Next())) {
                    var r=await Run(path,task,engine,round);var oracle=expected[(path,task)];Require(r.OutputBytes==oracle.Bytes&&r.Sha256==oracle.Hash,$"Timed output mismatch: {engine}/{task}");await log.WriteLineAsync(JsonSerializer.Serialize(r));
                }
                Console.WriteLine($"Grep flags round {round+1}/7: 60 verified runs");
            }
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static async Task<Row> Run(string path,string task,string engine,int round,string? mutation=null)
    {
        string pattern=task switch{"literal" or "count"=>mutation=="literal"?"prince":"Prince","regex"=>"war|peace","nonblank"=>"[^ \t]",_=>throw new ArgumentException("Unknown task")};
        if(mutation=="invalid-regex")pattern="[";
        var info=new ProcessStartInfo(Exe(engine)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(task is "literal" or "count"?"-F":"-E");
        if(task=="count")info.ArgumentList.Add(mutation=="count"?"-n":"-c");if(mutation=="regex")info.ArgumentList.Add("-i");
        info.ArgumentList.Add("--");info.ArgumentList.Add(pattern);info.ArgumentList.Add(path);info.Environment["LC_ALL"]="C";
        // Each rebuilt executable loads the same local MSYS2 runtime; do not borrow Git's older DLLs.
        if(engine!="grep-installed-fresh")info.Environment["PATH"]=Path.GetFullPath("tools/msys64/usr/bin")+Path.PathSeparator+info.Environment["PATH"];
        using var p=new Process{StartInfo=info};var watch=Stopwatch.StartNew();p.Start();var error=p.StandardError.ReadToEndAsync();using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long bytes=0;int n;
        while((n=await p.StandardOutput.BaseStream.ReadAsync(buffer))>0){hash.AppendData(buffer,0,n);bytes+=n;}await p.WaitForExitAsync();watch.Stop();string errors=await error;
        if(mutation!="invalid-regex"&&File.Exists(path))Require(p.ExitCode is 0 or 1,$"{engine} exit {p.ExitCode}: {errors}");
        return new(task,engine,File.Exists(path)?new FileInfo(path).Length:0,round,watch.Elapsed.TotalMilliseconds,bytes,Convert.ToHexString(hash.GetHashAndReset()),p.TotalProcessorTime.TotalMilliseconds,p.ExitCode,errors);
    }
    static (long Bytes,string Hash) Oracle(string path,string task)
    {
        var lines=File.ReadAllLines(path);string result=task=="count"?lines.Count(l=>l.Contains("Prince",StringComparison.Ordinal))+"\n":string.Concat(lines.Where(l=>task switch{"literal"=>l.Contains("Prince",StringComparison.Ordinal),"regex"=>l.Contains("war",StringComparison.Ordinal)||l.Contains("peace",StringComparison.Ordinal),"nonblank"=>l.Any(c=>c!=' '&&c!='\t'),_=>throw new ArgumentException("Unknown task")}).Select(l=>l+"\n"));
        byte[] bytes=Encoding.UTF8.GetBytes(result);return(bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    }
    static async Task Validate()
    {
        var random=new Random(Seed);var fixtures=new List<string>{"","\n \t\n","Prince Prince\nwar peace\nWAR\n", "no final newline",new string('x',131072)+"Prince\n"};
        for(int i=0;i<3;i++)fixtures.Add(string.Join('\n',Enumerable.Range(0,100).Select(_=>string.Join(" \t",Enumerable.Range(0,random.Next(0,9)).Select(_=>new[]{"Prince","prince","war","peace","WAR","x"}[random.Next(6)]))))+"\n");
        string path=Path.GetFullPath("data/grep-flags-fixture.txt");int checks=0,negative=0,killed=0;
        foreach(string fixture in fixtures){File.WriteAllText(path,fixture,new UTF8Encoding(false));foreach(string task in Tasks)foreach(string engine in Engines){var r=await Run(path,task,engine,-2);var expected=Oracle(path,task);Require(r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"Fixture mismatch {engine}/{task}");checks++;}}
        foreach(string engine in Engines){var missing=await Run(Path.GetFullPath("data/missing-grep-flags.txt"),"literal",engine,-2);Require(missing.ExitCode==2,"Missing input did not fail");negative++;var bad=await Run(path,"regex",engine,-2,"invalid-regex");Require(bad.ExitCode==2,"Malformed regex did not fail");negative++;}
        File.WriteAllText(path,"Prince Prince\nwar peace\nWAR\n",new UTF8Encoding(false));foreach(string engine in Engines)foreach(string task in new[]{"literal","regex","count"}){var r=await Run(path,task,engine,-2,task);var expected=Oracle(path,task);if(r.OutputBytes!=expected.Bytes||r.Sha256!=expected.Hash)killed++;}Require(killed==9,"Surviving flag mutant");
        File.WriteAllText("results/grep-tuned/validation.json",JsonSerializer.Serialize(new{Seed,EquivalenceChecks=checks,NegativeChecks=negative,MutantsKilled=killed,MutantsTotal=9,RandomCases=3,FixedCases=5},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS grep flags: {checks} equivalence checks, {negative} negatives, {killed} mutants killed");
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds,int ExitCode,string Error);
}
