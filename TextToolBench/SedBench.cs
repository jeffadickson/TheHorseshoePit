using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Comparable Windows sed commands, plus sd on its two supported replacement workloads.</summary>
internal static class SedBench
{
    const int Seed=20261107;
    static readonly string[] Tasks=["sed-literal","sed-regex","sed-nonblank","sed-select","sed-head","sed-transliterate"];
    static readonly string[] Engines=["sed-gnu","sed-busybox","sed-uutils","sed-red","sd"];
    static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
    static string Exe(string engine)=>Path.GetFullPath(engine switch {
        "sed-gnu"=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git/usr/bin/sed.exe"),
        "sed-busybox"=>"tools/sed-suite/busybox.exe",
        "sed-uutils"=>"tools/sed-suite/uutils-sed/sed.exe",
        "sed-red"=>"tools/sed-suite/red/red.exe",
        "sd"=>"tools/sed-suite/sd/sd-v1.1.0-x86_64-pc-windows-msvc/sd.exe",
        _=>throw new ArgumentException("Unknown engine",nameof(engine))
    });
    static bool Supports(string engine,string task)=>engine!="sd"||task is "sed-literal" or "sed-regex";
    static string Script(string task,bool mutant=false)=>task switch {
        "sed-literal"=>mutant?"s/prince/PRINCE/g":"s/Prince/PRINCE/g",
        "sed-regex"=>mutant?"s/[0-9][0-9]*/!/g":"s/[0-9][0-9]*/#/g",
        "sed-nonblank"=>mutant?"/^ *$/d":"/^[ \t]*$/d",
        "sed-select"=>mutant?"/prince/p":"/Prince/p",
        "sed-head"=>mutant?"1,99p;99q":"1,100p;100q",
        "sed-transliterate"=>mutant?"y/aeiou/aeiou/":"y/aeiou/AEIOU/",
        _=>throw new ArgumentException("Unknown task",nameof(task))
    };

    /// <summary>Record conformance separately, qualify each corpus cell, then execute a shuffled sequential matrix.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            foreach(string folder in new[]{"data","results","tools/sed-suite"})Require(Directory.Exists(folder),$"Missing directory: {folder}");
            Directory.CreateDirectory("results/sed");await Validate();if(args.Contains("--test"))return 0;
            string[] paths=["data/small-1KiB.txt","data/small-64KiB.txt","data/1MiB.txt","data/8MiB.txt","data/32MiB.txt"];
            var cells=new List<(string Path,string Task,string Engine)>();var rejected=new List<object>();
            var expected=new Dictionary<(string,string),(long Bytes,string Hash)>();
            foreach(string path in paths)foreach(string task in Tasks) {
                expected[(path,task)]=Digest(SedSemantics.Transform(File.ReadAllBytes(path),task));
                foreach(string engine in Engines.Where(e=>Supports(e,task))) {
                    var row=await Run(path,task,engine,-3);
                    if(Matches(row,expected[(path,task)]))cells.Add((path,task,engine));
                    else rejected.Add(new{Path=path,Task=task,Engine=engine,Expected=expected[(path,task)],Actual=row});
                }
            }
            var metadata=new List<object>();
            foreach(string engine in Engines) {
                var version=await Capture(Exe(engine),engine=="sed-busybox"?[]:["--version"]);
                metadata.Add(new{Engine=engine,Path=Exe(engine),Version=version,ExecutableSha256=HashFile(Exe(engine)),Distribution="Official Windows binary; publisher build flags are not inferred from language"});
            }
            File.WriteAllText("results/sed/environment.json",JsonSerializer.Serialize(new{Seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,Tools=metadata,Cells=cells.Select(c=>new{c.Path,c.Task,c.Engine}),RejectedCells=rejected,CellCount=cells.Count,ExpectedMeasured=cells.Count*7,ExpectedWarmups=cells.Count,Tasks=Tasks.ToDictionary(t=>t,ScriptText),Inputs=paths.Select(p=>new{Path=p,Bytes=new FileInfo(p).Length,Sha256=HashFile(p)}),Notes="GNU sed in Git and local MSYS2 is byte-identical and is counted once. sd is replacement-only, uses --preview and never edits the source. LC_ALL=C; explicit binary mode where supported; fresh processes, warm cache, startup/stdout hashing included. Missing final newline and non-ASCII/binary compatibility failures are retained separately, not normalized into passes."},Pretty));
            Require(cells.Count>0,"No qualified cells");
            var random=new Random(Seed);
            using var log=new StreamWriter("results/sed/runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                foreach(var cell in cells.OrderBy(_=>random.Next())) {
                    var row=await Run(cell.Path,cell.Task,cell.Engine,round);
                    Require(Matches(row,expected[(cell.Path,cell.Task)]),$"Timed mismatch: {cell.Engine}/{cell.Task}/{cell.Path}: {row.Error}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(row));
                }
                Console.WriteLine($"sed round {round+1}/7: {cells.Count} verified commands");
            }
            foreach(string path in paths)Require(HashFile(path)==JsonDocument.Parse(File.ReadAllText("results/sed/environment.json")).RootElement.GetProperty("Inputs").EnumerateArray().Single(x=>x.GetProperty("Path").GetString()==path).GetProperty("Sha256").GetString(),"Corpus changed during benchmark");
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static string ScriptText(string task)=>Script(task);

    /// <summary>Read raw bytes while draining stderr; timeout kills the owned process tree.</summary>
    static async Task<Row> Run(string path,string task,string engine,int round,bool mutant=false,string? overrideScript=null)
    {
        var info=new ProcessStartInfo(Exe(engine)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        if(engine=="sed-busybox")info.ArgumentList.Add("sed");
        if(engine=="sd") {
            info.ArgumentList.Add("--preview");if(task=="sed-literal")info.ArgumentList.Add("-F");
            info.ArgumentList.Add(overrideScript??(task=="sed-literal"?(mutant?"prince":"Prince"):"[0-9]+"));
            info.ArgumentList.Add(task=="sed-literal"?"PRINCE":mutant?"!":"#");
        } else {
            if(engine!="sed-uutils")info.ArgumentList.Add("-b");
            if(task is "sed-select" or "sed-head")info.ArgumentList.Add("-n");
            info.ArgumentList.Add("-e");info.ArgumentList.Add(overrideScript??Script(task,mutant));
        }
        info.ArgumentList.Add(Path.GetFullPath(path));info.Environment["LC_ALL"]="C";
        using var process=new Process{StartInfo=info};using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var watch=Stopwatch.StartNew();process.Start();var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long bytes=0;
        try {
            int count;while((count=await process.StandardOutput.BaseStream.ReadAsync(buffer,timeout.Token))!=0){hash.AppendData(buffer,0,count);bytes+=count;}
            await process.WaitForExitAsync(timeout.Token);watch.Stop();string error=await stderr;
            return new(task,engine,File.Exists(path)?new FileInfo(path).Length:0,round,watch.Elapsed.TotalMilliseconds,bytes,Convert.ToHexString(hash.GetHashAndReset()),process.TotalProcessorTime.TotalMilliseconds,process.ExitCode,error);
        }catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw new TimeoutException($"sed command timed out: {engine}/{task}");}
    }

    /// <summary>Retain edge/binary incompatibilities; negative and mutation checks must actually execute.</summary>
    static async Task Validate()
    {
        TestOracle();var fixtures=new List<(string Name,byte[] Input)> {
            ("empty",[]),("blank",Encoding.ASCII.GetBytes("\n \t\n")),
            ("ordinary",Encoding.ASCII.GetBytes("Prince 12 peace\nprince war3\n\t\n")),
            ("no-final-newline",Encoding.ASCII.GetBytes("Prince 123 tail")),
            ("long-line",Encoding.ASCII.GetBytes(new string('x',131072)+" Prince 123\n")),
            ("crlf",Encoding.ASCII.GetBytes("Prince 12\r\n \t\r\n")),
            ("utf8",Encoding.UTF8.GetBytes("Prince naïve 東京 😀 12\n")),
            ("binary",[0,255,80,114,105,110,99,101,32,49,50,10])
        };
        var random=new Random(Seed);
        for(int i=0;i<3;i++)fixtures.Add(($"random-{i}",Encoding.ASCII.GetBytes(string.Join('\n',Enumerable.Range(0,130).Select(_=>string.Join(' ',Enumerable.Range(0,random.Next(0,7)).Select(_=>new[]{"Prince","prince","0123","peace","war5","\t"}[random.Next(6)]))))+(i%2==0?"\n":""))));
        string path="results/sed/fixture.bin";int passed=0,negatives=0,killed=0;var failures=new List<object>();
        foreach(var fixture in fixtures) {
            File.WriteAllBytes(path,fixture.Input);
            foreach(string task in Tasks)foreach(string engine in Engines.Where(e=>Supports(e,task))) {
                var expected=Digest(SedSemantics.Transform(fixture.Input,task));var row=await Run(path,task,engine,-2);
                if(Matches(row,expected))passed++;
                else {File.WriteAllBytes($"results/sed/counterexample-{fixture.Name}.bin",fixture.Input);failures.Add(new{Fixture=fixture.Name,Engine=engine,Task=task,InputSha256=HashFile(path),ExpectedBytes=expected.Bytes,ExpectedHash=expected.Hash,Actual=row});}
            }
        }
        // One deliberate command mutation per supported task; enough rows to distinguish head 99/100.
        File.WriteAllText(path,string.Concat(Enumerable.Repeat("Prince 123 apple\n\t\n",110)),new UTF8Encoding(false));
        foreach(string engine in Engines) {
            var malformed=await Run(path,"sed-regex",engine,-2,overrideScript:engine=="sd"?"[":"s/[//");Require(malformed.ExitCode!=0,$"Accepted malformed program: {engine}");negatives++;
            var missing=await Run("results/sed/does-not-exist", "sed-literal",engine,-2);Require(missing.ExitCode!=0,$"Accepted missing file: {engine}");negatives++;
            foreach(string task in Tasks.Where(t=>Supports(engine,t))) {
                var expected=Digest(SedSemantics.Transform(File.ReadAllBytes(path),task));
                Require(Matches(await Run(path,task,engine,-2),expected),$"Mutation control is not conforming: {engine}/{task}");
                var result=await Run(path,task,engine,-2,mutant:true);
                Require(result.ExitCode==0&&!Matches(result,expected),$"Surviving or nonexecuting mutant: {engine}/{task}");killed++;
            }
        }
        File.WriteAllText("results/sed/validation.json",JsonSerializer.Serialize(new{Seed,Fixtures=fixtures.Count,EquivalenceChecks=passed,CompatibilityFailures=failures,NegativeChecks=negatives,CommandMutantsKilled=killed,CommandMutantsTotal=26,Notes="Failed edge cases are not passes. Qualification checks every corpus cell independently, and every timed output must match. Semantic command mutants are distinct from Stryker.NET source mutations."},Pretty));
        Console.WriteLine($"sed validation: {passed} passes, {failures.Count} compatibility failures, {negatives} negatives, {killed} command mutants detected");
    }

    /// <summary>Explicit expected examples independently challenge the reference oracle and its boundary convention.</summary>
    static void TestOracle()
    {
        foreach(var (task,input,expected) in new[]{("sed-literal","Prince prince\n","PRINCE prince\n"),("sed-regex","a12 0b3","a# #b#"),("sed-nonblank","\n \t\nx\n\r\n","x\n\r\n"),("sed-select","prince\nPrince\nPrince","Prince\nPrince"),("sed-head",string.Concat(Enumerable.Repeat("x\n",101)),string.Concat(Enumerable.Repeat("x\n",100))),("sed-transliterate","aEiOu xyz\n","AEIOU xyz\n")})
            Require(SedSemantics.Transform(Encoding.Latin1.GetBytes(input),task).SequenceEqual(Encoding.Latin1.GetBytes(expected)),"Independent oracle example failed");
        Require(SedSemantics.Transform([],"sed-head").Length==0,"Empty-file oracle");
        try{SedSemantics.Transform([],"invalid");throw new InvalidOperationException("Unknown task accepted");}catch(ArgumentException){}
    }
    static bool Matches(Row r,(long Bytes,string Hash) expected)=>r.ExitCode==0&&r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash;
    static (long Bytes,string Hash) Digest(byte[] bytes)=>(bytes.LongLength,Convert.ToHexString(SHA256.HashData(bytes)));
    static string HashFile(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static async Task<string> Capture(string executable,string[] arguments)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string argument in arguments)info.ArgumentList.Add(argument);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();return(await stdout)+await stderr;
    }
    /// <summary>One process invocation; bytes/hash are compared before a timing row is retained.</summary>
    internal sealed record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds,int ExitCode,string Error);
}
