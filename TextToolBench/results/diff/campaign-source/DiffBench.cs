using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

/// <summary>Fresh-process unified-diff benchmark. Patch application is outside the measured interval.</summary>
internal static class DiffBench
{
    // Keep the native process handle alive: Process.PeakWorkingSet64 rejects already-exited processes.
    [StructLayout(LayoutKind.Sequential)]
    struct MemoryCounters { public uint Size,PageFaults;public nuint PeakWorkingSet,WorkingSet,PeakPagedPool,PagedPool,PeakNonPagedPool,NonPagedPool,PageFile,PeakPageFile; }
    [DllImport("psapi.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool GetProcessMemoryInfo(Microsoft.Win32.SafeHandles.SafeProcessHandle handle,out MemoryCounters counters,uint size);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    static (long Idle,long Total) CpuSnapshot()
    {
        if(!GetSystemTimes(out long idle,out long kernel,out long user))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return (idle,kernel+user);
    }
    const int Seed=20261027;
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    static readonly string Out="results/diff";
    static readonly string PatchExe=Path.GetFullPath("tools/msys64/usr/bin/patch.exe");
    static readonly string Git=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git/cmd/git.exe");
    static readonly string Gnu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git/usr/bin/diff.exe");
    sealed record Engine(string Name,string Exe,string[] Prefix,string[] Flags);
    sealed record Fixture(string Name,int Scale,byte[] Before,byte[] After);
    sealed record Run(string Fixture,int Scale,long BeforeBytes,long AfterBytes,string Engine,int Round,double Milliseconds,double CpuMilliseconds,long PeakWorkingSetBytes,int ExitCode,long PatchBytes,string PatchSha256,int Added,int Deleted,int Hunks);
    sealed record Execution(byte[] Output,int ExitCode,string Error,double Milliseconds,double CpuMilliseconds,long PeakWorkingSetBytes);
    static Engine[] Engines()=>[
        new("gnu-installed",Gnu,[],[]),new("gnu-minimal",Gnu,[],["--minimal"]),new("gnu-speed",Gnu,[],["--speed-large-files"]),
        new("gnu-generic",Path.GetFullPath("tools/diff-suite/gnu-generic/bin/diff.exe"),[],[]),
        new("gnu-native",Path.GetFullPath("tools/diff-suite/gnu-native/src/diff.exe"),[],[]),
        new("uutils-official",Path.GetFullPath("tools/diff-suite/uutils-official/diffutils.exe"),["diff"],[]),
        new("uutils-generic",Path.GetFullPath("tools/diff-suite/rust-generic/release/diffutils.exe"),["diff"],[]),
        new("uutils-native",Path.GetFullPath("tools/diff-suite/rust-native/release/diffutils.exe"),["diff"],[]),
        new("busybox",Path.GetFullPath("tools/sed-suite/busybox.exe"),["diff"],["-a"]),
        ..new[]{"myers","minimal","patience","histogram"}.Select(a=>new Engine("git-"+a,Git,["--no-pager","-c","core.autocrlf=false","-c","core.safecrlf=false","diff","--no-index","--no-ext-diff","--no-textconv","--no-color","--no-renames","--text","--src-prefix=a/","--dst-prefix=b/","--diff-algorithm="+a],[]))
    ];

    /// <summary>Generate reproducible cases, retain every rejection, and never mix instrumented and timed sessions.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            if(!Directory.Exists("results")||!Directory.Exists("data"))throw new DirectoryNotFoundException("Run from the checkout root.");
            Directory.CreateDirectory(Out);Directory.CreateDirectory(Out+"/scratch");
            if(args.Contains("--report")){Report();return 0;}
            Engine[] engines=Engines();
            if(args.Contains("--available")&&args.Contains("--test"))engines=engines.Where(e=>File.Exists(e.Exe)).ToArray();
            foreach(var e in engines)if(!File.Exists(e.Exe))throw new FileNotFoundException(e.Exe);
            if(!File.Exists(PatchExe))throw new FileNotFoundException(PatchExe);
            await Validate(engines);
            if(args.Contains("--test"))return 0;
            string source=File.ReadAllText("data/1MiB.txt");
            string code=File.ReadAllText("DiffBench.cs");
            var fixtures=new List<Fixture>();
            foreach(int size in new[]{1024,65536,1048576})foreach(string task in new[]{"identical","scattered","insert-delete","moved","repeated","different","long-line"}) {
                var p=DiffModel.Pair(source,size,task);fixtures.Add(new(task,size,p.Before,p.After));
            }
            foreach(int size in new[]{8388608,33554432})foreach(string task in new[]{"identical","scattered","long-line"}) {
                var p=DiffModel.Pair(source,size,task);fixtures.Add(new(task,size,p.Before,p.After));
            }
            // A different text structure, retained verbatim as the source snapshot for reproducibility.
            File.WriteAllText(Out+"/source-code-fixture.cs",code,new UTF8Encoding(false));
            foreach(string task in new[]{"scattered","moved","insert-delete"}) {
                var p=DiffModel.Pair(code,65536,task);fixtures.Add(new("code-"+task,65536,p.Before,p.After));
            }
            var rejects=new List<object>();var cells=new List<(Fixture F,Engine E)>();
            foreach(var f in fixtures)foreach(var e in engines) {
                try{await Measure(f,e,-2);cells.Add((f,e));}
                catch(Exception ex){rejects.Add(new{Fixture=f.Name,f.Scale,Engine=e.Name,Reason=ex.Message});Console.WriteLine($"Excluded {e.Name}/{f.Name}/{f.Scale}: {ex.Message}");}
            }
            var versions=new List<object>();foreach(var e in engines) {
                string[] command=e.Name.StartsWith("git-")?["--version"]:e.Name.StartsWith("uutils-")?[]:e.Name=="busybox"?[]:["--version"];
                var version=await Execute(e.Exe,command,10);
                versions.Add(new{e.Name,e.Exe,e.Prefix,e.Flags,Sha256=Hash(File.ReadAllBytes(e.Exe)),Version=Encoding.UTF8.GetString(version.Output).Split('\n')[0],VersionError=version.Error});
            }
            File.WriteAllText(Out+"/environment.json",JsonSerializer.Serialize(new{Seed,Repeats=7,Warmups=1,Utc=DateTime.UtcNow,OS=Environment.OSVersion.ToString(),LogicalProcessors=Environment.ProcessorCount,Runtime=Environment.Version.ToString(),Tools=versions,PatchTool=new{Path=PatchExe,Sha256=Hash(File.ReadAllBytes(PatchExe))},Fixtures=fixtures.Select(f=>new{f.Name,f.Scale,BeforeBytes=f.Before.Length,AfterBytes=f.After.Length,BeforeSha256=Hash(f.Before),AfterSha256=Hash(f.After)}),Cells=cells.Count,Rejected=rejects,SourceSha256=Hash(File.ReadAllBytes("data/1MiB.txt")),Notes="One sequential session; startup and stdout capture included; patch application excluded and checked for every output. Unified context=3, LC_ALL=C, git indentation heuristic disabled. PeakWorkingSetBytes is the OS process peak, not allocation or whole-process-tree memory. Seven observations are not confidence intervals. Same-source compiler pairs are distinct from installed binaries."},Json));
            using var writer=new StreamWriter(Out+"/runs.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};var rng=new Random(Seed);
            using var utilization=new StreamWriter(Out+"/system-utilization.jsonl",false,new UTF8Encoding(false)){AutoFlush=true};
            for(int round=-1;round<7;round++) {
                var cpu=CpuSnapshot();var started=DateTime.UtcNow;
                foreach(var cell in cells.OrderBy(_=>rng.Next()))await writer.WriteLineAsync(JsonSerializer.Serialize(await Measure(cell.F,cell.E,round)));
                var end=CpuSnapshot();
                await utilization.WriteLineAsync(JsonSerializer.Serialize(new{Round=round,StartedUtc=started,EndedUtc=DateTime.UtcNow,SystemBusyPercent=100d*(1d-(end.Idle-cpu.Idle)/(double)(end.Total-cpu.Total)),Note="Whole-system CPU utilization across all logical processors, including harness, patch verifier and unrelated activity; not an isolation guarantee."}));
                Console.WriteLine($"diff round {round+1}/7: {cells.Count} outputs reconstructed exactly");
            }
            Report();return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }

    /// <summary>Drain both pipes concurrently, enforce a bounded wall timeout, and preserve raw output bytes.</summary>
    static async Task<Execution> Execute(string exe,IEnumerable<string> args,int seconds=20)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(string a in args)info.ArgumentList.Add(a);
        info.Environment["LC_ALL"]="C";info.Environment["GIT_CONFIG_NOSYSTEM"]="1";info.Environment["GIT_CONFIG_GLOBAL"]=Path.GetFullPath(Out+"/scratch/empty-config");
        // Keep locally compiled GNU programs on their matching MSYS runtime, rather than Git's DLLs.
        if(exe==PatchExe||exe.StartsWith(Path.GetFullPath("tools/diff-suite/gnu-"),StringComparison.OrdinalIgnoreCase))
            info.Environment["PATH"]=Path.GetFullPath("tools/msys64/usr/bin")+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH");
        using var p=new Process{StartInfo=info};using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));using var output=new MemoryStream();
        var watch=Stopwatch.StartNew();p.Start();var handle=p.SafeHandle;var err=p.StandardError.ReadToEndAsync(deadline.Token);
        try {
            await p.StandardOutput.BaseStream.CopyToAsync(output,deadline.Token);await p.WaitForExitAsync(deadline.Token);watch.Stop();
            if(!GetProcessMemoryInfo(handle,out var memory,(uint)Marshal.SizeOf<MemoryCounters>()))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Cannot read process peak working set");
            return new(output.ToArray(),p.ExitCode,await err,watch.Elapsed.TotalMilliseconds,p.TotalProcessorTime.TotalMilliseconds,checked((long)memory.PeakWorkingSet));
        }catch(OperationCanceledException){if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();throw new TimeoutException($"Command exceeded {seconds}s");}
    }

    /// <summary>Apply the patch with GNU patch independently, then compare exact bytes (including final newline).</summary>
    static async Task Check(Fixture f,byte[] patch,int exit)
    {
        bool same=f.Before.AsSpan().SequenceEqual(f.After);
        if(exit!=(same?0:1))throw new InvalidDataException($"Wrong diff exit {exit}, expected {(same?0:1)}");
        if(same){if(patch.Length!=0)throw new InvalidDataException("Identical input emitted a patch");return;}
        if(patch.Length==0)throw new InvalidDataException("Changed input emitted no patch");
        string output=Path.GetFullPath(Out+"/scratch/reconstructed.txt");if(File.Exists(output))File.Delete(output);
        File.WriteAllBytes(Out+"/scratch/change.patch",patch);
        var applied=await Execute(PatchExe,["--binary","--batch","--fuzz=0","--no-backup-if-mismatch","-o",output,Path.GetFullPath(Out+"/scratch/before.txt"),Path.GetFullPath(Out+"/scratch/change.patch")]);
        if(applied.ExitCode!=0||!File.Exists(output)||!File.ReadAllBytes(output).AsSpan().SequenceEqual(f.After))throw new InvalidDataException("Patch reconstruction failed: "+applied.Error+Encoding.UTF8.GetString(applied.Output));
    }

    static async Task<Run> Measure(Fixture f,Engine e,int round)
    {
        // The pinned Rust dependency allocates a full u32 LCS matrix. Bound its allocation before launch.
        if(e.Name.StartsWith("uutils-")) {
            long table=DiffModel.QuadraticTableBytes(f.Before,f.After);
            if(table>268435456)throw new InvalidDataException($"Resource exclusion: predicted LCS table {table} bytes exceeds 256 MiB; not a measured timeout or correctness failure");
        }
        File.WriteAllBytes(Out+"/scratch/before.txt",f.Before);File.WriteAllBytes(Out+"/scratch/after.txt",f.After);
        var args=new List<string>(e.Prefix);
        if(e.Name.StartsWith("git-"))args.AddRange(["--no-indent-heuristic","--unified=3"]);
        else args.AddRange(e.Name.StartsWith("gnu-")?["--binary","-a","-U3"]:["-U3"]);
        args.AddRange(e.Flags);args.Add(Out+"/scratch/before.txt");args.Add(Out+"/scratch/after.txt");
        var result=await Execute(e.Exe,args);await Check(f,result.Output,result.ExitCode);
        var stats=DiffModel.Statistics(result.Output);
        return new(f.Name,f.Scale,f.Before.Length,f.After.Length,e.Name,round,result.Milliseconds,result.CpuMilliseconds,result.PeakWorkingSetBytes,result.ExitCode,result.Output.LongLength,Hash(result.Output),stats.Added,stats.Deleted,stats.Hunks);
    }

    /// <summary>Retain edge incompatibilities and seeded randomized counterexamples separately from benchmark eligibility.</summary>
    static async Task Validate(Engine[] engines)
    {
        var fixtures=new List<Fixture>{new("empty",0,[],[]),new("insert-empty",0,[],"x\n"u8.ToArray()),new("delete-empty",0,"x\n"u8.ToArray(),[]),new("no-newline",0,"a\nb"u8.ToArray(),"a\nc"u8.ToArray()),new("crlf",0,"a\r\nb\r\n"u8.ToArray(),"a\r\nc\r\n"u8.ToArray()),new("utf8",0,Encoding.UTF8.GetBytes("東京 naïve 😀\n"),Encoding.UTF8.GetBytes("東京 café 😀\n")),new("binary",0,[0,255,10,65,10],[0,254,10,66,10])};
        var rng=new Random(Seed);for(int i=0;i<20;i++) {
            string[] a=Enumerable.Range(0,rng.Next(1,30)).Select(_=>rng.Next(7).ToString()).ToArray();var b=a.ToList();b.Insert(rng.Next(b.Count),"new");b.RemoveAt(rng.Next(b.Count));
            fixtures.Add(new("seeded-"+i,0,Encoding.ASCII.GetBytes(string.Join('\n',a)+(i%2==0?"\n":"")),Encoding.ASCII.GetBytes(string.Join('\n',b)+(i%3==0?"\n":""))));
        }
        var failures=new List<object>();int passed=0,negatives=0,mutants=0,minimalChecks=0;
        foreach(var f in fixtures)foreach(var e in engines) {
            try {
                var r=await Measure(f,e,-3);
                if(f.Name.StartsWith("seeded-")&&e.Name is "gnu-minimal" or "git-minimal") {
                    string[] Lines(byte[] bytes)=>Encoding.ASCII.GetString(bytes).TrimEnd('\n').Split('\n');
                    // Missing-final-newline state is an additional edit dimension; only LF-terminated pairs qualify.
                    if(f.Before[^1]==10&&f.After[^1]==10){if(r.Added+r.Deleted!=DiffModel.MinimumEdits(Lines(f.Before),Lines(f.After)))throw new InvalidDataException("Nonminimal result for bounded reference");minimalChecks++;}
                }
                passed++;
            }catch(Exception ex){failures.Add(new{Fixture=f.Name,Engine=e.Name,Reason=ex.Message,Before=Convert.ToBase64String(f.Before),After=Convert.ToBase64String(f.After)});}
        }
        foreach(var e in engines) {
            var bad=await Execute(e.Exe,e.Prefix.Concat(["--texttoolbench-invalid-option"]));if(bad.ExitCode==0)throw new InvalidDataException("Invalid option accepted: "+e.Name);negatives++;
            var missing=await Execute(e.Exe,e.Prefix.Concat([Out+"/scratch/missing-left",Out+"/scratch/missing-right"]));
            // Git --no-index reports missing paths with exit 1; require an error diagnostic and no patch too.
            if(missing.ExitCode==0||missing.Output.Length!=0||string.IsNullOrWhiteSpace(missing.Error))throw new InvalidDataException("Missing-file error not diagnosed: "+e.Name);negatives++;
            var control=new Fixture("mutation-control",0,"a\n"u8.ToArray(),"b\n"u8.ToArray());await Measure(control,e,-3);
            bool killed=false;try{await Check(control,"--- a\n+++ b\n@@ -1 +1 @@\n-a\n+c\n"u8.ToArray(),1);}catch(InvalidDataException){killed=true;}
            if(!killed)throw new InvalidDataException("Undetected patch corruption");mutants++;
        }
        File.WriteAllText(Out+"/validation.json",JsonSerializer.Serialize(new{Seed,Engines=engines.Select(e=>e.Name),Fixtures=fixtures.Count,Passed=passed,Failures=failures,NegativeChecks=negatives,PatchCorruptionMutantsKilled=mutants,MinimalReferenceChecks=minimalChecks,Notes="Randomized CLI campaign is a bounded seeded generator, not coverage-guided fuzzing. Counterexamples retain exact base64 bytes. Stryker source mutation is separate."},Json));
        Console.WriteLine($"diff validation: {passed} reconstructed, {failures.Count} incompatibilities, {negatives} negatives, {mutants} patch mutants");
    }

    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Generate a standalone, script-free HTML report from this session only.</summary>
    static void Report()
    {
        var rows=File.ReadLines(Out+"/runs.jsonl").Select(x=>JsonSerializer.Deserialize<Run>(x)!).ToArray();
        using var environment=JsonDocument.Parse(File.ReadAllText(Out+"/environment.json"));
        int expected=environment.RootElement.GetProperty("Cells").GetInt32();
        if(expected<1||rows.Length!=expected*8||rows.Any(r=>r.Round is < -1 or > 6)||rows.Count(r=>r.Round==-1)!=expected)
            throw new InvalidDataException("Incomplete or invalid campaign; keep every qualified cell and warmup.");
        var summary=rows.Where(r=>r.Round>=0).GroupBy(r=>(r.Fixture,r.Scale,r.Engine)).Select(g=>{
            var values=g.OrderBy(r=>r.Milliseconds).ToArray();if(values.Length!=7||values.Select(r=>r.Round).Distinct().Count()!=7)throw new InvalidDataException("Incomplete seven-repeat cell");
            return new{g.Key.Fixture,g.Key.Scale,g.Key.Engine,N=values.Length,Median=values[3].Milliseconds,Min=values[0].Milliseconds,Max=values[^1].Milliseconds,Q1=values[1].Milliseconds,Q3=values[5].Milliseconds,PeakWorkingSetBytes=values.OrderBy(r=>r.PeakWorkingSetBytes).ElementAt(3).PeakWorkingSetBytes,PatchBytes=values[3].PatchBytes,values[3].Added,values[3].Deleted,values[3].Hunks,values[3].BeforeBytes,values[3].AfterBytes};
        }).OrderBy(x=>x.Scale).ThenBy(x=>x.Fixture).ThenBy(x=>x.Median).ToArray();
        if(summary.Length!=expected)throw new InvalidDataException("Missing qualified summary cells.");
        File.WriteAllText(Out+"/summary.json",JsonSerializer.Serialize(summary,Json));
        var html=new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>TextToolBench · Diff laboratory</title><style>body{margin:0;background:#f5f3ec;color:#172e33;font:16px/1.6 system-ui}main{max-width:1200px;margin:auto;padding:40px 24px}h1,h2{font-family:Georgia,serif;font-weight:400}h1{font-size:64px;line-height:1.05;margin:20px 0}h2{font-size:30px}a{color:#087e73}.intro{max-width:850px}small{color:#596d70}table{border-collapse:collapse;width:100%;background:#fffefa;font-size:13px;font-variant-numeric:tabular-nums}th,td{padding:8px 12px;border-bottom:1px solid #d8ded5;text-align:right}th:first-child,td:first-child{text-align:left}tr:first-child td{background:#e5eee4}details{margin:20px 0}summary{cursor:pointer;font-weight:650;padding:12px;background:#e4e8dd}.scroll{overflow:auto}.cards{display:flex;gap:20px;flex-wrap:wrap}.card{padding:20px;background:#fffefa;border:1px solid #d8ded5}.card strong{font:36px Georgia;display:block}@media print{details{break-inside:avoid}body{background:white}}</style><main><a href=\"../overview.html\">← TextToolBench overview</a><p><small>ONE HOST · ONE INTERLEAVED SESSION · VERIFIED PATCHES</small></p><h1>The diff laboratory</h1><div class=\"intro\"><p>Compare implementations, algorithms and processor tuning. Every retained output reconstructed its target exactly using GNU patch; valid patches do not need identical text.</p>");
        html.Append($"<div class=\"cards\"><div class=\"card\"><strong>{summary.Length}</strong>qualified cells</div><div class=\"card\"><strong>{rows.Count(r=>r.Round>=0):N0}</strong>measured processes</div><div class=\"card\"><strong>{rows.Count(r=>r.Round<0):N0}</strong>warmups</div></div>");
        html.Append("<h2>How to read these results</h2><p>Lowest median comes first within each fixture and size. Elapsed time includes fresh process startup and raw stdout capture; input preparation, patch hashing and GNU patch application occur outside the clock. Warm filesystem cache, one warmup and seven sequential shuffled rounds; seed 20261027. Min–max is an observed range, not a confidence interval. Memory is the median OS peak working set of the diff process, not allocations or total process-tree memory.</p><p>All commands emit unified patches with three context lines. Git indentation heuristics, external diff, text conversion, color, renames and user/system configuration are disabled. GNU uses binary I/O and text mode; other implementations may reject binary fixtures. Different headers contribute to patch byte counts. Added+deleted lines measure edit size, not human readability. Minimality is independently checked only on bounded LF-terminated random fixtures. Source code is synthetic repetition of the retained harness snapshot; the book inputs derive from the existing public-domain corpus.</p><p>The compiler experiments compare the same source and toolchain within each language: GNU generic -O2 against -O3/-march=native/-mtune=native/-ftree-vectorize; Rust release generic against target-cpu=native (both inherit thin LTO and one codegen unit). Installed/release binaries are separate baselines, not controlled compiler comparisons. No global fastest-tool claim follows from this host.</p><p><a href=\"environment.json\">Environment & rejected cells</a> · <a href=\"validation.json\">Compatibility & negative tests</a> · <a href=\"runs.jsonl\">Raw runs</a> · <a href=\"summary.json\">Summary data</a> · <a href=\"README.md\">Reproduction & verification</a></p></div>");
        foreach(var group in summary.GroupBy(s=>(s.Fixture,s.Scale))) {
            html.Append($"<details open><summary>{WebUtility.HtmlEncode(group.Key.Fixture)} · nominal {group.Key.Scale:N0} bytes</summary><small>Actual before / after: {group.First().BeforeBytes:N0} / {group.First().AfterBytes:N0} bytes</small><div class=\"scroll\"><table><thead><tr><th>Engine / algorithm</th><th>Median ms</th><th>Min–max ms</th><th>Q1–Q3 ms</th><th>Peak MiB</th><th>Patch bytes</th><th>+ / − lines</th><th>Hunks</th></tr></thead><tbody>");
            foreach(var s in group)html.Append(FormattableString.Invariant($"<tr><td>{WebUtility.HtmlEncode(s.Engine)}</td><td>{s.Median:F2}</td><td>{s.Min:F2}–{s.Max:F2}</td><td>{s.Q1:F2}–{s.Q3:F2}</td><td>{s.PeakWorkingSetBytes/1048576d:F2}</td><td>{s.PatchBytes:N0}</td><td>{s.Added:N0} / {s.Deleted:N0}</td><td>{s.Hunks:N0}</td></tr>"));
            html.Append("</tbody></table></div></details>");
        }
        html.Append("<footer><p>Retained incompatibilities are not passes. Timeouts are excluded with reasons. Patch verification is an independent application check, not a proof for all inputs. The page has no external assets or scripts. Rendered layout has not been visually verified.</p></footer></main></html>");
        File.WriteAllText(Out+"/report.html",html.ToString(),new UTF8Encoding(false));Console.WriteLine($"Diff report: {summary.Length} cells");
    }
}
