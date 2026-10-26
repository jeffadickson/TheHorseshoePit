using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Focused Select-String experiment. Run after Bench.dll so experiments never compete for resources.
const int seed=20261027;
string pwsh=Environment.GetEnvironmentVariable("BENCH_PWSH") ?? "pwsh.exe";
string[] tasks=["literal","regex","count"];
var rng=new Random(seed);
int checks=0;
// Independent reference for empty input, case, repeated matches, missing newline and bounded ASCII fuzz.
var fixtures=new List<string>{"", "Prince Prince\nWAR war\npeace\nprince\n", "Prince"};
for(int i=0;i<3;i++) fixtures.Add(string.Join('\n',Enumerable.Range(0,100).Select(_ => string.Join(" \t",Enumerable.Range(0,rng.Next(0,8)).Select(_=>new[]{"Prince","prince","war","peace","WAR","x"}[rng.Next(6)]))))+"\n");
foreach(string fixture in fixtures) {
    string path=Path.GetFullPath("data/select-test.txt"); File.WriteAllText(path,fixture,new UTF8Encoding(false));
    foreach(string task in tasks) {
        var r=await Run(path,task,-2);
        var lines=File.ReadAllLines(path);
        string expected=task=="count" ? lines.Count(l=>l.Contains("Prince",StringComparison.Ordinal))+"\n" : string.Concat(lines.Where(l=>task=="literal" ? l.Contains("Prince",StringComparison.Ordinal) : l.Contains("war",StringComparison.Ordinal)||l.Contains("peace",StringComparison.Ordinal)).Select(l=>l+"\n"));
        byte[] bytes=Encoding.UTF8.GetBytes(expected);
        if(r.OutputBytes!=bytes.Length || r.Sha256!=Convert.ToHexString(SHA256.HashData(bytes))) throw new Exception("Select-String fixture mismatch");
        checks++;
    }
}
File.WriteAllText("results/select-string-validation.json",JsonSerializer.Serialize(new{Seed=seed,EquivalenceChecks=checks,FixedFixtures=3,RandomFixtures=3}));
// Coverage runs validate fixtures without contaminating the timing experiment.
if(args.Contains("--test")) { Console.WriteLine($"PASS: {checks} Select-String fixture checks"); return; }
var reference=File.ReadLines("results/runs.jsonl").Select(s=>JsonSerializer.Deserialize<Row>(s)!).Where(r=>r.Engine=="gnu"&&r.Round==0&&tasks.Contains(r.Task)).ToDictionary(r=>(r.InputBytes,r.Task));
var cells=Directory.GetFiles("data","*MiB.txt").SelectMany(p=>tasks.Select(t=>(Path.GetFullPath(p),t))).ToArray();
using var log=new StreamWriter("results/select-string-runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
for(int round=-1;round<7;round++) {
    foreach(var (path,task) in cells.OrderBy(_=>rng.Next())) {
        var r=await Run(path,task,round); var expected=reference[(r.InputBytes,task)];
        if(r.OutputBytes!=expected.OutputBytes || r.Sha256!=expected.Sha256) throw new Exception("Select-String corpus mismatch");
        log.WriteLine(JsonSerializer.Serialize(r));
    }
    Console.WriteLine($"Select-String round {round+1}/7 complete");
}
Console.WriteLine($"Select-String: {checks} fixture checks, 9 warmups, 63 measured runs passed.");

// Same timing boundary as the main harness: process launch through stdout hashing and process exit.
async Task<Row> Run(string path,string task,int round) {
    var info=new ProcessStartInfo(pwsh){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
    foreach(string a in new[]{"-NoLogo","-NoProfile","-NonInteractive","-File",Path.GetFullPath("select-string.ps1"),"-Path",path,"-Task",task}) info.ArgumentList.Add(a);
    using var p=new Process{StartInfo=info}; var watch=Stopwatch.StartNew(); p.Start();
    var errors=p.StandardError.ReadToEndAsync(); using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    byte[] buffer=new byte[65536]; long bytes=0; int n;
    while((n=await p.StandardOutput.BaseStream.ReadAsync(buffer))>0){hash.AppendData(buffer,0,n);bytes+=n;}
    await p.WaitForExitAsync(); watch.Stop();
    if(p.ExitCode!=0) throw new Exception(await errors);
    await errors;
    return new(task,"selectstring",new FileInfo(path).Length,round,watch.Elapsed.TotalMilliseconds,bytes,Convert.ToHexString(hash.GetHashAndReset()),p.TotalProcessorTime.TotalMilliseconds);
}

/// <summary>Serializable raw observation, compatible with the main harness output.</summary>
record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds);
