using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

// Standalone report generator: dotnet run --project Analyze.csproj -c Release. Does not rerun experiments.
using var sedEnvironment=JsonDocument.Parse(File.ReadAllText("results/sed/environment.json"));
int sedCells=sedEnvironment.RootElement.GetProperty("CellCount").GetInt32();
int expectedWarmups=297+sedCells;
var allRows = new[]{"results/runs.jsonl","results/select-string-runs.jsonl","results/ripgrep-runs.jsonl","results/small-runs.jsonl","results/rawk-runs.jsonl","results/awk-extra-runs.jsonl","results/buffering-runs.jsonl","results/grep-tuned/runs.jsonl","results/sed/runs.jsonl"}.SelectMany(path=>File.ReadLines(path).Select(s=>{
    var row=JsonSerializer.Deserialize<Row>(s)!;
    // Keep fresh controls separate from historical runs of the same executable.
    if(path.EndsWith("buffering-runs.jsonl")&&(row.Engine is "rawk-stefan" or "awk-rs-quinn"))row=row with{Engine=row.Engine+"-control"};
    if(path.EndsWith("awk-extra-runs.jsonl")&&row.Engine=="awk-current")row=row with{Engine="awk-extra-control"};
    return row;
})).ToArray();
var rows=allRows.Where(r=>r.Round>=0).ToArray();
// Refuse to publish a partial matrix or silently combine incompatible outputs.
if(rows.Length!=2079+sedCells*7 || allRows.Count(r=>r.Round==-1)!=expectedWarmups) throw new InvalidOperationException("Incomplete experiment matrix");
foreach(var g in rows.GroupBy(r=>(r.Task,r.Engine,r.InputBytes)))
    if(!g.Select(r=>r.Round).Order().SequenceEqual(Enumerable.Range(0,7)) || g.Any(r=>!double.IsFinite(r.Milliseconds)||r.Milliseconds<=0)) throw new InvalidOperationException("Invalid repetitions or timings");
foreach(var g in allRows.GroupBy(r=>(r.Task,r.InputBytes)))
    if(g.Select(r=>(r.OutputBytes,r.Sha256)).Distinct().Count()!=1) throw new InvalidOperationException("Cross-run output mismatch");
var summaries = rows.GroupBy(r => (r.Task,r.Engine,r.InputBytes)).Select(g => {
    var v=g.Select(r=>r.Milliseconds).Order().ToArray();
    return new Summary(g.Key.Task,g.Key.Engine,g.Key.InputBytes,v.Length,v[v.Length/2],v[0],v[^1],v[(int)Math.Floor((v.Length-1)*.25)],v[(int)Math.Ceiling((v.Length-1)*.75)]);
}).OrderBy(s=>s.InputBytes).ThenBy(s=>s.Task).ThenBy(s=>s.Engine).ToArray();
File.WriteAllText("results/summary.json",JsonSerializer.Serialize(summaries,new JsonSerializerOptions{WriteIndented=true}));
using(var csv=new StreamWriter("results/summary.csv")) {
    csv.WriteLine("task,engine,input_bytes,n,median_ms,min_ms,max_ms,q1_ms,q3_ms,mib_per_second");
    foreach(var s in summaries) csv.WriteLine(FormattableString.Invariant($"{s.Task},{s.Engine},{s.InputBytes},{s.N},{s.Median:F3},{s.Min:F3},{s.Max:F3},{s.Q1:F3},{s.Q3:F3},{Rate(s,"F3")}"));
}
var md=new StringBuilder("# Text-tool benchmark results\n\n");
md.AppendLine($"{rows.Length} measured runs plus {expectedWarmups} warmups: original suites 1260; mawk/frawk interpreter/fresh GNU awk 315; controlled Rust buffering experiment 84; same-source GNU grep build-flags experiment 420; sed comparison {sedCells*7}. All timed outputs verified by length and SHA-256. Profiles and bug probes are separate diagnostic evidence, not extra benchmark samples.\n");
md.AppendLine("## Sed implementations across five sizes\n\nGNU sed, BusyBox sed, uutils/sed and red run six workloads; sd runs the two replacements only. Git/MSYS2 sed binaries are byte-identical and are counted once. These are official Windows distributions, not a controlled compiler comparison. [Sed methods, provenance, compatibility and verification](results/sed/README.md). Median milliseconds; unsupported or rejected cells are shown as —.\n\n| Input bytes | Workload | GNU sed | BusyBox | uutils/sed | red | sd |\n|---:|---|---:|---:|---:|---:|---:|");
foreach(var group in summaries.Where(s=>s.Task.StartsWith("sed-",StringComparison.Ordinal)).GroupBy(s=>(s.InputBytes,s.Task))) {
    md.Append($"| {group.Key.InputBytes} | {group.Key.Task} |");
    foreach(string engine in new[]{"sed-gnu","sed-busybox","sed-uutils","sed-red","sd"}) {
        var entry=group.SingleOrDefault(s=>s.Engine==engine);md.Append(entry is null?" — |":FormattableString.Invariant($" {entry.Median:F3} |"));
    }
    md.AppendLine();
}
md.AppendLine("\nKnown edge incompatibilities remain failures, even when that implementation matches every timed ASCII/LF corpus cell. Every retained timing is checked byte-for-byte by output length and SHA-256; no newline normalization is used. Replacement-only sd uses preview mode; input hashes are rechecked to guard against accidental in-place edits.\n");
md.AppendLine("Sed verification: 279 edge/random equivalence passes and seven retained compatibility failures; ten malformed-program/missing-file checks; 26 command mutants detected with conforming controls; 24 oracle unit tests including 1,200 seeded byte-property cases. Stryker.NET 5.0.0 oracle score 91.67%: 33 killed, two survived, one uncovered; dispositions are in the sed methods. Fixture-only SedBench package coverage is 73.23% lines / 80.95% branches; oracle unit scope is 96.29% / 97.50%. These scopes do not measure the third-party binaries. Three offline download-script Pester tests passed, with 59.18% command coverage; line/branch metrics are unavailable in that Pester version.\n");
md.AppendLine("See [SETUP.md](SETUP.md) for the Windows Makefile workflow and dependency/source acquisition. Historical measurements retain their original host metadata. Current portability changes affect tool discovery, not the measured text algorithms. Fresh controls are labelled separately so medians from different sessions are never silently combined.\n");
long largest=summaries.Max(s=>s.InputBytes);
md.AppendLine("## Same-source GNU grep build flags: 32 MiB median milliseconds\n\n| Task | Rebuilt generic O2 | Rebuilt native O3 | Installed grep | O2 / O3 |\n|---|---:|---:|---:|---:|");
foreach(string task in new[]{"literal","regex","count","nonblank"}) {
    double Median(string engine)=>summaries.Single(s=>s.InputBytes==largest&&s.Task==task&&s.Engine==engine).Median;
    md.AppendLine(FormattableString.Invariant($"| {task} | {Median("grep-built-o2"):F2} | {Median("grep-native-o3"):F2} | {Median("grep-installed-fresh"):F2} | {Median("grep-built-o2")/Median("grep-native-o3"):F3}× |"));
}
md.AppendLine("\nBoth new grep 3.0 builds use GCC 16.2.0, the same MSYS2 runtime and configure choices. Generic flags are -O2 -march=nocona -msahf -mtune=generic -pipe. Tuned flags are -O3 -march=native -mtune=native -ftree-vectorize -pipe with vectorization diagnostics. GCC resolves native to Skylake/AVX2 on this i5-10505, and reports 32-byte vectorization in DFA code. This does not prove those loops dominate the benchmark. Installed grep is a separate GCC 13.3.0/runtime reference, not a controlled compiler-flags comparison. Seven repetitions, warm cache, fresh processes, seed 20261104; all startup/stdout hashing costs included. Small differences should be judged with the retained ranges. Complete flags, compiler, target features and configure/build logs are in results/grep-tuned.\n");
md.AppendLine("## Profile-driven Rust stdout buffering: 32 MiB median milliseconds\n\n| Implementation / task | Fresh original | 64 KiB buffer | Original / buffered |\n|---|---:|---:|---:|");
foreach(string engine in new[]{"rawk-stefan","awk-rs-quinn"})foreach(string task in new[]{"field","words","lines"}) {
    double Median(string suffix)=>summaries.Single(s=>s.InputBytes==largest&&s.Task==task&&s.Engine==engine+suffix).Median;
    md.AppendLine(FormattableString.Invariant($"| {engine} / {task} | {Median("-control"):F2} | {Median("-buffered"):F2} | {Median("-control")/Median("-buffered"):F2}× |"));
}
md.AppendLine("\nOnly CLI stdout buffering changed in these separately built variants. Patches and paired measurements are retained. Original source trees and executables remain unchanged. [Profiling explanation](results/profiles/EXPLANATION.md) contains wall-clock stack-sampling evidence and its limitations. It is not an ETW CPU-time or allocation-count profile. Buffering explains a substantial part of field-output overhead, while counting profiles still expose copying and field allocation.\n");
md.AppendLine("## mawk and frawk bytecode interpreter: median milliseconds\n\n| Input bytes | Task | mawk | frawk interpreter | Fresh GNU awk |\n|---:|---|---:|---:|---:|");
foreach(var s in summaries.Where(s=>s.Engine=="mawk")) {
    var f=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="frawk-interp");var g=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="awk-extra-control");
    md.AppendLine(FormattableString.Invariant($"| {s.InputBytes} | {s.Task} | {s.Median:F2} | {f.Median:F2} | {g.Median:F2} |"));
}
md.AppendLine("\nmawk is the author's native MinGW64 1.3.4 20260302 build, using BINMODE=3 for LF stdout. frawk 0.4.8 is built with default features disabled and a retained resolved dependency lock. **The Windows Cranelift JIT panics and has no successful timings here; these are bytecode-interpreter results.** Two long-field conformance checks failed in the original frawk and are recorded, not counted as passes. Every timed book-corpus output still matches exactly. The separate [long-field bug fix](results/frawk-bug/README.md) passes the minimized and randomized regressions; the benchmark retains the original frawk binary. These three tools were interleaved with seed 20261101, one warmup and seven repeats across five sizes.\n");
md.AppendLine("Downloaded [War and Peace from Project Gutenberg](https://www.gutenberg.org/cache/epub/2600/pg2600.txt) (3,359,610 bytes). Derivatives use ASCII substitutions and LF line endings, repeat the book as needed, and stop at a complete line near 1, 8 or 32 MiB. These are synthetic scaling inputs, not three independent corpora.\n");
md.AppendLine("Windows build 26200; Intel Core i5-10505, 6 cores/12 logical processors, 15.74 GiB RAM; .NET 10.0.14; PowerShell 7.6.5; GNU grep 3.0, sed 4.9, awk 5.4.1 from Git for Windows. GNU tools run natively through the MSYS runtime, not WSL.\n");
md.AppendLine("## Small files: median milliseconds\n\n| Size | Task | ripgrep | GNU grep | PS pipeline | PS reader | Select-String |\n|---|---|---:|---:|---:|---:|---:|");
foreach(var size in summaries.Where(s=>s.InputBytes<100000&&(s.Task is "literal" or "regex" or "count")).GroupBy(s=>s.InputBytes))foreach(var task in size.GroupBy(s=>s.Task)) {
    double Median(string engine)=>task.Single(s=>s.Engine==engine).Median;
    md.AppendLine(FormattableString.Invariant($"| {size.Key} bytes | {task.Key} | {Median("ripgrep"):F2} | {Median("grep-current"):F2} | {Median("pipeline"):F2} | {Median("reader"):F2} | {Median("selectstring"):F2} |"));
}
md.AppendLine("\nSmall files are contiguous slices of the existing ASCII/LF corpus near its first Prince match, ending at a complete line around 1 KiB and 64 KiB. All five implementations were interleaved in the same session, with seed 20261029, one warmup and seven measured repetitions. The timer includes starting a fresh process and hashing stdout. These results describe whole-command latency; they do not isolate startup or represent commands inside an already running PowerShell session. Exact sizes, source offset, match counts and SHA-256 values are retained in results/small-environment.json.\n");
md.AppendLine("## Both rawk implementations versus fresh GNU awk: median milliseconds\n\n| Input bytes | Task | stefanalfbo/rawk | quinnjr/awk-rs | GNU awk |\n|---:|---|---:|---:|---:|");
foreach(var s in summaries.Where(s=>s.Engine=="rawk-stefan")) {
    var q=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="awk-rs-quinn");
    var g=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="awk-current");
    md.AppendLine(FormattableString.Invariant($"| {s.InputBytes} | {s.Task} | {s.Median:F2} | {q.Median:F2} | {g.Median:F2} |"));
}
md.AppendLine("\nThese are [stefanalfbo/rawk](https://github.com/stefanalfbo/rawk) and [quinnjr/rawk](https://github.com/quinnjr/rawk), whose binary is awk-rs. Both are unmodified release builds using cargo build --release --locked and the checked-in release profiles. Rust is local to tools/, with no permanent shell PATH change. results/rawk-environment.json records compiler version, source commits, lockfile/executable hashes and clean-source status. The first rawk has no --version option, so its manifest version identifies it. All three tools ran sequentially in interleaved order with seed 20261030. Scripts are identical: NF {print $1}; {n+=NF} END {print n+0}; END {print NR+0}. Each cell has one warmup and seven repetitions. This is a native Windows MSVC versus Git/MSYS comparison, including startup and stdout hashing, with a 120-second per-process timeout. No upstream Rust source was modified or instrumented; benchmark fixture coverage is separate.\n");
md.AppendLine("## Largest file: median seconds\n\n| Workload | GNU | PS pipeline | PS reader | Pipeline / GNU | Reader / GNU |\n|---|---:|---:|---:|---:|---:|");
foreach(var g in summaries.Where(s=>s.InputBytes==largest&&(s.Engine is "gnu" or "pipeline" or "reader")).GroupBy(s=>s.Task)) {
    var n=g.Single(s=>s.Engine=="gnu");var p=g.Single(s=>s.Engine=="pipeline");var r=g.Single(s=>s.Engine=="reader");
    md.AppendLine(FormattableString.Invariant($"| {g.Key} | {n.Median/1000:F3} | {p.Median/1000:F3} | {r.Median/1000:F3} | {p.Median/n.Median:F1}× | {r.Median/n.Median:F1}× |"));
}
md.AppendLine("\n### Select-String, largest file\n\n| Workload | Select-String seconds | Select-String / GNU |\n|---|---:|---:|");
foreach(var s in summaries.Where(s=>s.InputBytes==largest&&s.Engine=="selectstring")) {
    var n=summaries.Single(n=>n.InputBytes==largest&&n.Task==s.Task&&n.Engine=="gnu");
    md.AppendLine(FormattableString.Invariant($"| {s.Task} | {s.Median/1000:F3} | {s.Median/n.Median:F1}× |"));
}
md.AppendLine("\n### Ripgrep versus contemporaneous GNU grep, largest file\n\n| Workload | ripgrep seconds | Fresh grep seconds | grep / ripgrep |\n|---|---:|---:|---:|");
foreach(var s in summaries.Where(s=>s.InputBytes==largest&&s.Engine=="ripgrep")) {
    var n=summaries.Single(n=>n.InputBytes==largest&&n.Task==s.Task&&n.Engine=="grep-current");
    md.AppendLine(FormattableString.Invariant($"| {s.Task} | {s.Median/1000:F4} | {n.Median/1000:F4} | {n.Median/s.Median:F2}× |"));
}
using(var environment=JsonDocument.Parse(File.ReadAllText("results/ripgrep-environment.json"))) {
    md.AppendLine($"\nInstalled winget package BurntSushi.ripgrep.MSVC: {environment.RootElement.GetProperty("RipgrepVersion").GetString()!.Split('\n')[0]}. The executable path and SHA-256 are recorded in results/ripgrep-environment.json.\n");
}
md.AppendLine("Ripgrep and fresh GNU grep were interleaved sequentially with seed 20261028, one warmup and seven measured repeats per cell. This gives a comparison made in the same session; the earlier GNU and PowerShell measurements are retained and can be affected by time drift. Ripgrep uses --no-config, explicit case-sensitive text output, and --include-zero with -c to count matching lines rather than occurrences. Patterns and input files are identical. It keeps its default I/O strategy; GNU grep runs through the Git for Windows MSYS runtime, while ripgrep is the native MSVC build.\n");
md.AppendLine("\n## Method and interpretation\n\nEach invocation is a fresh process, with startup, parsing/JIT, input reading, computation, stdout pipe transfer and a common C# SHA-256 sink included. Console rendering and network download are excluded. One warmup per cell precedes seven measured rounds. Each round shuffles the 72 cells using seed 20261026; runs are sequential. The OS file cache is warm and was not flushed. This measures end-to-end command usage, not an already running interactive PowerShell session or cold-disk throughput.\n");
md.AppendLine("PowerShell pipeline uses Get-Content and Where-Object/ForEach-Object. Reader uses a PowerShell while loop with .NET StreamReader and dispatches a switch per line; neither is a compiled C# implementation. These are representative scripts, not a claim about the fastest achievable PowerShell. Both use buffered UTF-8/LF stdout writers. Select-String reads the file directly, using -SimpleMatch where applicable; its match objects are written as lines or counted with Measure-Object. Its extension ran after the main matrix, with its own warmups and shuffle seed 20261027, so cross-suite ratios can be affected by time drift. GNU grep handles literal/regex/nonblank/count; sed handles replacement; awk handles fields/words/lines. Case-sensitive ASCII semantics are explicit. Regex is war|peace; literal is Prince; replacement is Prince → Count; fields split on spaces/tabs.\n");
md.AppendLine("Raw per-run measurements include CPU time. summary.csv/json retain medians, observed min/max and quartiles (order-statistic bounds, not confidence intervals). Seven samples are enough to show large differences but weak evidence for small rankings. Other processes, scheduler activity, antivirus and power management were not controlled. Ratios describe these implementations and this Windows host, not all possible implementations or Linux performance. Output hashing has a shared cost and can matter for output-heavy workloads.\n");
md.AppendLine("## Verification and coverage\n\nThe independent C# oracle verifies every timed output. Validation additionally includes empty input, blank/tab lines, repeated matches, case distinctions, a missing final newline, a 131,072-character line and three randomized 100-line fixtures with seed 20261026. The missing-newline sed replacement difference is detected explicitly and excluded from equivalence counts. Missing input and an unknown oracle operation must fail. Three real PowerShell mutations (wrong literal case, case-insensitive regex, wrong replacement) must all be killed by the output checks. Randomized fixtures are bounded fuzzing; no failure required shrinking/minimization. This is targeted mutation testing, not an exhaustive mutation score.\n");
if(File.Exists("results/coverage-tests.cobertura.xml")) {
    var root=XDocument.Load("results/coverage-tests.cobertura.xml").Root!;
    md.AppendLine($"Coverlet validation-run C# coverage: {100*double.Parse(root.Attribute("line-rate")!.Value,CultureInfo.InvariantCulture):F2}% lines ({root.Attribute("lines-covered")!.Value}/{root.Attribute("lines-valid")!.Value}), {100*double.Parse(root.Attribute("branch-rate")!.Value,CultureInfo.InvariantCulture):F2}% branches ({root.Attribute("branches-covered")!.Value}/{root.Attribute("branches-valid")!.Value}). The full benchmark was deliberately uninstrumented to avoid timing distortion. The matrix-generation/timing path is therefore not counted as test coverage. PowerShell scripts and GNU binaries have no instrumented line/branch measurements; cross-implementation checks cover behavior, not those coverage metrics.\n");
}
md.AppendLine("Select-String adds 18 independent fixture checks including three seeded fuzz fixtures. PowerShell script code coverage is unmeasured; the checks verify observable output.\n");
md.AppendLine("The ripgrep extension adds 64 independent equivalence checks (eight fixtures × four tasks × two tools), including three seeded fuzz fixtures, plus four negative checks and three killed argument mutants. The mutants change the literal, enable case-insensitive regex matching, and count occurrences instead of matching lines. Native GNU and ripgrep binaries have no instrumented code coverage. Fixture runs are instrumented separately from timed runs.\n");
md.AppendLine("The small-file extension adds 60 fixture checks across all five engines: empty input, case/repeated matches, blank/no-match input and one 100-line randomized fixture with seed 20261029. It reuses the native negative/mutation checks and independent oracle. Small-suite generation and timing are excluded from test-only coverage; every timed small output is still verified.\n");
md.AppendLine("The rawk extension adds 72 independent equivalence checks, seven negative checks (missing files, malformed programs and an invalid harness task), and nine deliberately incorrect programs detected across the three interpreters. Fixtures cover empty input, whitespace, tabs, missing final newline, a 131,072-character field and three 100-line bounded fuzz inputs with seed 20261030. These are targeted mutations and bounded fuzzing, not exhaustive conformance testing. Native rawk, awk-rs and GNU awk coverage is unmeasured. The fixture-only C# coverage omits metadata, matrix execution and timeout handling; timed output equivalence is checked separately without instrumentation.\n");
foreach(var (file,label) in new[]{("results/coverage-supplement.cobertura.xml","Supplemental C# fixture coverage"),("results/coverage-ripgrep.cobertura.xml","Ripgrep extension C# fixture coverage"),("results/coverage-rawk.cobertura.xml","Rawk comparison C# fixture coverage"),("results/coverage-awk-extra.cobertura.xml","mawk/frawk C# fixture coverage"),("results/coverage-buffering.cobertura.xml","Buffering C# fixture coverage"),("results/coverage-grep-tuned.cobertura.xml","Grep build comparison C# fixture coverage"),("results/coverage-profiler.cobertura.xml","Profiler control-test C# coverage"),("results/coverage-report.cobertura.xml","Report-generator execution coverage (not a test suite)")}) {
    if(File.Exists(file)) {
        var root=XDocument.Load(file).Root!;
        md.AppendLine($"{label}: {100*double.Parse(root.Attribute("line-rate")!.Value,CultureInfo.InvariantCulture):F2}% lines ({root.Attribute("lines-covered")!.Value}/{root.Attribute("lines-valid")!.Value}), {100*double.Parse(root.Attribute("branch-rate")!.Value,CultureInfo.InvariantCulture):F2}% branches ({root.Attribute("branches-covered")!.Value}/{root.Attribute("branches-valid")!.Value}).\n");
    }
}
md.AppendLine("Stable requirements BENCH-001 through BENCH-004 are maintained in requirements.sdoc; comments map requirements to implementation and evidence. StrictDoc HTML export is in results/requirements.\n");
md.AppendLine("Before generating the report, run the small-file extension with `dotnet run --project RipgrepBench.csproj -c Release -- --small`. For fixture-only verification, add `--test`. BENCH-005 and BENCH-006 trace the native-tool and small-file extensions.\n");
md.AppendLine("For the rawk extension (BENCH-007), build the retained source trees at the commits recorded in results/rawk-environment.json. Commands: `cargo build --release --locked --manifest-path tools/stefanalfbo-rawk/Cargo.toml -p rawk-cli` and `cargo build --release --locked --manifest-path tools/quinnjr-rawk/Cargo.toml`. The local Rust toolchain uses RUSTUP_HOME=tools/rustup and CARGO_HOME=tools/cargo; add the absolute tools/cargo/bin directory to PATH for that shell. Then run `dotnet run --project RawkBench.csproj -c Release` before generating the report. Add `-- --test` to run only fixture verification. The source repositories and local compiler remain available for reproduction.\n");
md.AppendLine("## Reproduce\n\n```powershell\nwinget install --id BurntSushi.ripgrep.MSVC --exact\nInvoke-WebRequest https://www.gutenberg.org/cache/epub/2600/pg2600.txt -OutFile source.txt\ndotnet build Bench.csproj -c Release\ndotnet bin/Release/net10.0/Bench.dll --test\ndotnet bin/Release/net10.0/Bench.dll\ndotnet run --project Supplement.csproj -c Release\ndotnet run --project RipgrepBench.csproj -c Release\ndotnet run --project RipgrepBench.csproj -c Release -- --small\ndotnet run --project Analyze.csproj -c Release\n```\n\nGNU executables are expected in C:\\Program Files\\Git\\usr\\bin. Set BENCH_PWSH to override PowerShell and BENCH_RG to override ripgrep. By default the ripgrep runner resolves the installed winget package, not Codex's bundled rg.exe. Ripgrep-only validation: dotnet run --project RipgrepBench.csproj -c Release -- --test. Restore .NET 10 SDK and the recorded native tool versions to reproduce the environment. The retained source hash allows detecting future Gutenberg changes. Raw JSONL includes warmups with Round=-1; exclude them from statistics.\n");
File.WriteAllText("README.md",md.ToString());
var html=new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><title>Text tool benchmark</title><style>body{font:16px system-ui;max-width:1200px;margin:40px auto;padding:20px;background:#101827;color:#e2e8f0}table{border-collapse:collapse;width:100%;margin:20px 0}th,td{padding:9px;text-align:right;border-bottom:1px solid #334155}th:first-child,td:first-child{text-align:left}h1,h2{color:#93c5fd}.gnu{background:#34d399}.pipeline{background:#fb923c}.reader{background:#60a5fa}.selectstring{background:#c084fc}.ripgrep{background:#facc15}.grep-current{background:#2dd4bf}.rawk-stefan{background:#f472b6}.awk-rs-quinn{background:#e2e8f0}.awk-current{background:#22c55e}.bar{height:12px;min-width:2px;display:inline-block}small{color:#94a3b8}a{color:#93c5fd}</style><h1>ripgrep, GNU text tools and PowerShell</h1><p>2079 measured invocations • 297 warmups • seven repetitions • verified outputs</p><p>Warm-cache, fresh-process wall time on Windows. Bars show median time, scaled independently within each file size. Hover for quartiles. See <a href='../README.md'>README</a> for methods and limitations.</p><p>GNU: green; PowerShell pipeline: orange; reader: blue; Select-String: purple; ripgrep: yellow; fresh grep: teal.</p><h2>New: ripgrep vs fresh GNU grep on 32 MiB</h2><p>Ripgrep 15.2.0 MSVC and GNU grep 3.0 were interleaved in this extension. The earlier GNU and PowerShell measurements remain in the tables below. Process startup and stdout hashing are included.</p><table><tr><th>Workload</th><th>ripgrep median ms</th><th>Fresh grep median ms</th><th>grep / ripgrep</th></tr>");
foreach(var s in summaries.Where(s=>s.InputBytes==largest&&s.Engine=="ripgrep")) {
    var n=summaries.Single(n=>n.InputBytes==largest&&n.Task==s.Task&&n.Engine=="grep-current");
    html.Append(FormattableString.Invariant($"<tr><td>{s.Task}</td><td>{s.Median:F2}</td><td>{n.Median:F2}</td><td>{n.Median/s.Median:F2}×</td></tr>"));
}
html.Append("</table>");
html.Append("<h2>Both rawk implementations vs fresh GNU awk</h2><p>Unmodified release builds of stefanalfbo/rawk and quinnjr/rawk (awk-rs), interleaved with GNU awk across all five sizes. Median milliseconds, including startup and stdout hashing. Source commits, compiler and executable hashes are recorded in rawk-environment.json.</p><table><tr><th>Input bytes / task</th><th>stefanalfbo/rawk</th><th>quinnjr/awk-rs</th><th>GNU awk</th></tr>");
foreach(var s in summaries.Where(s=>s.Engine=="rawk-stefan")) {
    var q=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="awk-rs-quinn");
    var g=summaries.Single(n=>n.InputBytes==s.InputBytes&&n.Task==s.Task&&n.Engine=="awk-current");
    html.Append(FormattableString.Invariant($"<tr><td>{s.InputBytes} / {s.Task}</td><td>{s.Median:F2}</td><td>{q.Median:F2}</td><td>{g.Median:F2}</td></tr>"));
}
html.Append("</table>");
html.Append("<h2>Small files: whole-command median milliseconds</h2><p>About 1 KiB and 64 KiB, with all five implementations interleaved. Includes fresh process startup and stdout hashing; warm file cache. See README for exact corpus slices and validation.</p><table><tr><th>Bytes / task</th><th>ripgrep</th><th>GNU grep</th><th>PS pipeline</th><th>PS reader</th><th>Select-String</th></tr>");
foreach(var size in summaries.Where(s=>s.InputBytes<100000&&(s.Task is "literal" or "regex" or "count")).GroupBy(s=>s.InputBytes))foreach(var task in size.GroupBy(s=>s.Task)) {
    html.Append($"<tr><td>{size.Key} / {task.Key}</td>");
    foreach(string engine in new[]{"ripgrep","grep-current","pipeline","reader","selectstring"})html.Append(FormattableString.Invariant($"<td>{task.Single(s=>s.Engine==engine).Median:F2}</td>"));
    html.Append("</tr>");
}
html.Append("</table>");
foreach(var size in summaries.GroupBy(s=>s.InputBytes)) {
    html.Append($"<h2>{size.Key:N0} bytes ({size.Key/1024.0:F2} KiB)</h2><table><tr><th>Workload / implementation</th><th>Median ms</th><th>Min–max ms</th><th>MiB/s</th><th style='width:32%'>Time</th></tr>");
    double max=size.Max(s=>s.Median);
    foreach(var s in size) html.Append(FormattableString.Invariant($"<tr><td>{s.Task} · {s.Engine}</td><td>{s.Median:F1}</td><td>{s.Min:F1}–{s.Max:F1}</td><td>{(s.Task=="sed-head"?"—":Rate(s,"F1"))}</td><td style='text-align:left'><span class='bar {s.Engine}' style='width:{s.Median/max*100:F2}%' title='Q1 {s.Q1:F1} / Q3 {s.Q3:F1} ms'></span></td></tr>"));
    html.Append("</table>");
}
html.Append("<p>Case-sensitive ASCII, LF-terminated text. Corpus: War and Peace, Project Gutenberg. Measurements include startup and output hashing; do not interpret as cold-disk or Linux performance.</p></html>");
var sedHtml=new StringBuilder("<h2>Sed implementations: five input sizes</h2><p>GNU sed, BusyBox sed, uutils/sed and red: six workloads. sd: two replacements only. Identical Git/MSYS2 sed binaries count once. Official distributions; compiler settings differ. Seven measured repetitions per cell; every retained output matches the independent byte reference. <a href='sed/README.md'>Methods, compatibility failures and verification</a>.</p><table><tr><th>Bytes / workload</th><th>GNU sed</th><th>BusyBox</th><th>uutils/sed</th><th>red</th><th>sd</th></tr>");
foreach(var group in summaries.Where(s=>s.Task.StartsWith("sed-",StringComparison.Ordinal)).GroupBy(s=>(s.InputBytes,s.Task))) {
    sedHtml.Append($"<tr><td>{group.Key.InputBytes} / {group.Key.Task}</td>");
    foreach(string engine in new[]{"sed-gnu","sed-busybox","sed-uutils","sed-red","sd"}) {
        var entry=group.SingleOrDefault(s=>s.Engine==engine);
        sedHtml.Append(entry is null?"<td>—</td>":FormattableString.Invariant($"<td title='Min {entry.Min:F3}, max {entry.Max:F3}, Q1 {entry.Q1:F3}, Q3 {entry.Q3:F3} ms'>{entry.Median:F3}</td>"));
    }
    sedHtml.Append("</tr>");
}
sedHtml.Append("</table><p>Median milliseconds. Head reads only through record 100, so its whole-file MiB/s is not a streaming throughput measure. Known missing-newline and binary-input failures remain failures; corpus timings use verified ASCII/LF inputs.</p>");
html.Replace("2079 measured invocations • 297 warmups",$"{rows.Length} measured invocations • {expectedWarmups} warmups");
html.Replace("<h2>New: ripgrep",sedHtml+"<h2>New: ripgrep");
html.Replace(".bar{",".sed-gnu{background:#34d399}.sed-busybox{background:#fbbf24}.sed-uutils{background:#c084fc}.sed-red{background:#f87171}.sd{background:#60a5fa}.bar{");
File.WriteAllText("results/report.html",html.ToString());
Console.WriteLine(md.ToString());

// A prefix command does not read the whole input, so whole-file throughput would be misleading.
static string Rate(Summary summary,string format)=>summary.Task=="sed-head"?"":(summary.InputBytes/1048576.0/(summary.Median/1000)).ToString(format,CultureInfo.InvariantCulture);

/// <summary>One observed invocation. CPU time is retained in raw evidence only.</summary>
record Row(string Task,string Engine,long InputBytes,int Round,double Milliseconds,long OutputBytes,string Sha256,double CpuMilliseconds);
/// <summary>Order-statistic summary of seven end-to-end samples, with time in milliseconds.</summary>
record Summary(string Task,string Engine,long InputBytes,int N,double Median,double Min,double Max,double Q1,double Q3);
