using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Fresh mawk/frawk comparison or paired unmodified/buffered Rust output experiment.</summary>
internal static class AwkExtra
{
    static readonly string[] Tasks=["field","words","lines"];
    /// <summary>Validate independently before shuffled sequential timing; --test excludes the timing matrix.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            bool buffering=args.Contains("--buffering");string prefix=buffering?"buffering":"awk-extra";int seed=buffering?20261102:20261101;
            string[] engines=buffering?["rawk-stefan","rawk-stefan-buffered","awk-rs-quinn","awk-rs-quinn-buffered"]:["mawk","frawk-interp","awk-current"];
            var random=new Random(seed);int checks=0,negative=0,killed=0;var compatibility=new List<object>();
            if(!buffering) {
                var failures=new List<RawkBench.Row>();
                foreach(string task in Tasks)failures.Add(await RawkBench.Run(Path.GetFullPath("data/small-1KiB.txt"),task,"frawk-cranelift",-3));
                File.WriteAllText("results/frawk-cranelift-compatibility.json",JsonSerializer.Serialize(failures,new JsonSerializerOptions{WriteIndented=true}));
            }
            var fixtures=new List<string>{"","\n \t\n","alpha beta gamma\none two\n", "no final newline",new string('x',131072)+" y\n"};
            for(int i=0;i<3;i++)fixtures.Add(string.Join('\n',Enumerable.Range(0,100).Select(_=>string.Join(" \t",Enumerable.Range(0,random.Next(0,9)).Select(_=>new[]{"alpha","beta","0","!","abc"}[random.Next(5)]))))+"\n");
            string fixturePath=Path.GetFullPath($"data/{prefix}-fixture.txt");
            foreach(string fixture in fixtures) {
                File.WriteAllText(fixturePath,fixture,new UTF8Encoding(false));
                foreach(string task in Tasks)foreach(string engine in engines) {
                    var r=await RawkBench.Run(fixturePath,task,engine,-2);var expected=RawkBench.Oracle(fixturePath,task);
                    if(engine=="frawk-interp"&&(r.ExitCode!=0||r.OutputBytes!=expected.Bytes||r.Sha256!=expected.Hash)) {
                        compatibility.Add(new{Engine=engine,Task=task,InputBytes=r.InputBytes,ExpectedBytes=expected.Bytes,ExpectedHash=expected.Hash,Actual=r});continue;
                    }
                    Require(r.ExitCode==0&&r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"{prefix} fixture {checks}/{engine}/{task}: exit {r.ExitCode}, {r.OutputBytes} vs {expected.Bytes} bytes; {r.Error}");checks++;
                }
            }
            foreach(string engine in engines)foreach(string program in new[]{"BEGIN {"}) {
                var r=await RawkBench.Run(fixturePath,"lines",engine,-2,program);Require(r.ExitCode!=0,$"Malformed program accepted: {engine}");negative++;
            }
            File.WriteAllText(fixturePath,"alpha beta gamma\none two\n\n",new UTF8Encoding(false));
            foreach(string engine in engines)foreach(var (task,program) in new[]{("field","NF {print $2}"),("words","{n+=1} END {print n+0}"),("lines","END {print NR+1}")}) {
                var r=await RawkBench.Run(fixturePath,task,engine,-2,program);var expected=RawkBench.Oracle(fixturePath,task);
                if(r.ExitCode==0&&(r.OutputBytes!=expected.Bytes||r.Sha256!=expected.Hash))killed++;
            }
            Require(killed==engines.Length*3,"Surviving or nonexecuting mutant");
            File.WriteAllText($"results/{prefix}-validation.json",JsonSerializer.Serialize(new{Seed=seed,EquivalenceChecks=checks,CompatibilityFailures=compatibility,NegativeChecks=negative,MutantsKilled=killed,MutantsTotal=engines.Length*3,FixedCases=5,RandomCases=3,Note="Known frawk fixture failures are retained, not counted as passes. Every timed corpus output must still match exactly."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS {prefix}: {checks} equivalence checks, {negative} negatives, {killed} mutants killed");
            if(args.Contains("--test"))return 0;
            string[] paths=buffering?[Path.GetFullPath("data/32MiB.txt")]:Directory.GetFiles("data","*MiB.txt").Concat(Directory.GetFiles("data","small-*KiB.txt")).Select(Path.GetFullPath).ToArray();
            var expectedOutputs=paths.SelectMany(p=>Tasks.Select(t=>(p,t))).ToDictionary(x=>x,x=>RawkBench.Oracle(x.p,x.t));
            File.WriteAllText($"results/{prefix}-environment.json",JsonSerializer.Serialize(new{Seed=seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,Tools=engines.Select(e=>new{Engine=e,Path=RawkBench.Executable(e),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(RawkBench.Executable(e))))}),Purpose=buffering?"Only CLI stdout buffering changed; 64 KiB BufWriter; same compiler and release profiles; original source trees untouched":"Official native MinGW64 mawk 1.3.4 20260302; frawk 0.4.8 release --no-default-features, explicit BYTECODE INTERPRETER backend, Windows MSVC; Cranelift JIT fails and is excluded from timings; GNU awk from Git/MSYS",FrawkCommit=buffering?null:"d548b159a5a76c12de508f8bf415fdc09e609f0a",FrawkLockSha256=buffering?null:Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/frawk/Cargo.lock"))),Notes="Fresh process, warm cache, sequential interleaved order, stdout hashing; no successful frawk JIT measurements"},new JsonSerializerOptions{WriteIndented=true}));
            var cells=paths.SelectMany(p=>Tasks.SelectMany(t=>engines.Select(e=>(p,t,e)))).ToArray();
            using var log=new StreamWriter($"results/{prefix}-runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                foreach(var (path,task,engine) in cells.OrderBy(_=>random.Next())) {
                    var r=await RawkBench.Run(path,task,engine,round);var expected=expectedOutputs[(path,task)];
                    Require(r.ExitCode==0&&r.OutputBytes==expected.Bytes&&r.Sha256==expected.Hash,$"{prefix} corpus mismatch {engine}/{task}: {r.Error}");
                    await log.WriteLineAsync(JsonSerializer.Serialize(r));
                }
                Console.WriteLine($"{prefix} round {round+1}/7: {cells.Length} verified runs");
            }
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
