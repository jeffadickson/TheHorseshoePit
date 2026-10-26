using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Owned-process Windows stack sampler. Samples wall-clock thread locations, not ETW CPU time.</summary>
internal static class NativeProfile
{
    const int Seed=20261031;
    record Sample(ulong Ip,string[] Frames);
    record Observation(string Engine,string Task,int Repetition,double WallMs,double CpuMs,int Samples,int ContextFailures,int SymbolFailures,int UnwindFailures,string ExeSha256,string OutputSha256,long OutputBytes,Dictionary<string,int> Leaf,Dictionary<string,int> Inclusive);
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};

    /// <summary>Collect separate diagnostic profiles; --test validates stack attribution against a known hot loop.</summary>
    public static async Task<int> Main(string[] args)
    {
        try {
            Directory.CreateDirectory("results/profiles");
            if(args.Contains("--test")){await SelfTest();return 0;}
            await SelfTest();
            var observations=new List<Observation>();
            foreach(string engine in new[]{"stefan","quinn"})foreach(string task in new[]{"field","words","lines"})for(int repeat=0;repeat<3;repeat++) {
                string exe=Path.GetFullPath($"tools/profiling/{engine}/release/{(engine=="stefan"?"rawk":"awk-rs")}.exe");
                string script=task switch{"field"=>"NF {print $1}","words"=>"{n+=NF} END {print n+0}",_=>"END {print NR+0}"};
                var r=await Collect(exe,[script,Path.GetFullPath("data/32MiB.txt")],engine,task,repeat);
                var oracle=File.ReadLines("results/rawk-runs.jsonl").Select(s=>JsonSerializer.Deserialize<Expected>(s)!).First(r=>r.Engine=="awk-current"&&r.Task==task&&r.InputBytes>33000000);
                Require(r.OutputBytes==oracle.OutputBytes&&r.OutputSha256==oracle.Sha256,"Profile output differs from benchmark");
                observations.Add(r);Console.WriteLine($"Profile {engine}/{task}/{repeat+1}: {r.Samples} samples, {r.ContextFailures} context failures");
                File.WriteAllText("results/profiles/observations.json",JsonSerializer.Serialize(observations,Json));
            }
            WriteSummary(observations);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }

    /// <summary>Briefly suspend only the child main thread, unwind up to 24 frames, then always resume it.</summary>
    static async Task<Observation> Collect(string exe,string[] arguments,string engine,string task,int repeat)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string a in arguments)info.ArgumentList.Add(a);
        info.Environment["LC_ALL"]="C";
        using var p=new Process{StartInfo=info};var watch=Stopwatch.StartNew();p.Start();
        var output=Digest(p.StandardOutput.BaseStream);var errors=p.StandardError.ReadToEndAsync();
        IntPtr thread=IntPtr.Zero,contextRaw=Marshal.AllocHGlobal(1248),frame=Marshal.AllocHGlobal(4096);
        IntPtr context=new((contextRaw.ToInt64()+15)&~15L);bool symbols=false;int contextFailures=0,symbolFailures=0,unwindFailures=0;
        var raw=new List<ulong[]>();var random=new Random(Seed+repeat);var zeroContext=new byte[1232];var zeroFrame=new byte[4096];
        try {
            // Exact local PDBs only; no symbol-server or network dependency during sampling.
            Native.SymSetOptions(0x2|0x4|0x10|0x200|0x400|0x80000);
            symbols=Native.SymInitialize(p.Handle,Path.GetDirectoryName(exe),false);Require(symbols,"SymInitialize failed: "+Marshal.GetLastWin32Error());
            // Process.Start may return before the loader has published the executable/DLL list.
            Thread.Sleep(30);
            if(!p.HasExited) {
                var image=p.MainModule!;ulong imageBase=unchecked((ulong)image.BaseAddress.ToInt64());
                foreach(ProcessModule module in p.Modules)Native.SymLoadModuleEx(p.Handle,IntPtr.Zero,module.FileName,null,unchecked((ulong)module.BaseAddress.ToInt64()),(uint)module.ModuleMemorySize,IntPtr.Zero,0);
                // Windows loader workers can have near-identical start timestamps. Identify the
                // primary thread by its Win32 entry address inside the executable, not its ordering.
                foreach(ProcessThread candidate in p.Threads) {
                    IntPtr handle=Native.OpenThread(0x0002|0x0008|0x0040,false,(uint)candidate.Id);if(handle==IntPtr.Zero)continue;
                    int status=Native.NtQueryInformationThread(handle,9,out IntPtr start,IntPtr.Size,out _);
                    ulong address=unchecked((ulong)start.ToInt64());
                    if(status==0&&address>=imageBase&&address<imageBase+(ulong)image.ModuleMemorySize){thread=handle;break;}
                    Native.CloseHandle(handle);
                }
                Require(thread!=IntPtr.Zero,"Could not identify the executable's primary thread");
            }
            Native.FunctionTableCallback functions=Native.SymFunctionTableAccess64;
            Native.ModuleBaseCallback modules=Native.SymGetModuleBase64;
            while(!p.HasExited&&watch.Elapsed<TimeSpan.FromSeconds(120)) {
                Marshal.Copy(zeroContext,0,context,zeroContext.Length);Marshal.WriteInt32(context,48,0x100003);
                bool suspended=thread!=IntPtr.Zero&&Native.SuspendThread(thread)!=uint.MaxValue;
                if(!suspended){contextFailures++;break;}
                try {
                    if(!Native.GetThreadContext(thread,context)){contextFailures++;continue;}
                    ulong ip=unchecked((ulong)Marshal.ReadInt64(context,248));
                    var stack=new List<ulong>{ip};
                    Marshal.Copy(zeroFrame,0,frame,zeroFrame.Length);
                    SetAddress(frame,0,ip);SetAddress(frame,32,unchecked((ulong)Marshal.ReadInt64(context,160)));SetAddress(frame,48,unchecked((ulong)Marshal.ReadInt64(context,152)));
                    for(int depth=0;depth<24;depth++) {
                        if(!Native.StackWalk64(0x8664,p.Handle,thread,frame,context,IntPtr.Zero,functions,modules,IntPtr.Zero))break;
                        ulong address=unchecked((ulong)Marshal.ReadInt64(frame,0));if(address==0)break;
                        if(address!=stack[^1])stack.Add(address);
                    }
                    if(stack.Count==1)unwindFailures++;
                    raw.Add(stack.ToArray());
                }finally{Native.ResumeThread(thread);}
                Thread.Sleep(random.Next(1,5));
            }
            if(!p.HasExited){p.Kill(true);throw new TimeoutException("Profile child exceeded 120 seconds");}
            await p.WaitForExitAsync();watch.Stop();var digest=await output;string error=await errors;
            Require(p.ExitCode==0,$"Profile child failed: {error}");
            var cache=new Dictionary<ulong,string>();
            foreach(ulong ip in raw.SelectMany(s=>s).Distinct()) {
                string? name=Resolve(p.Handle,ip);if(name==null){symbolFailures++;ulong module=Native.SymGetModuleBase64(p.Handle,ip);name=$"unknown@0x{module:X}+0x{ip-module:X}";}cache[ip]=name;
            }
            var samples=raw.Select(s=>new Sample(s[0],s.Select(ip=>cache[ip]).ToArray())).ToArray();
            using(var log=new StreamWriter($"results/profiles/{engine}-{task}-{repeat}.jsonl",false,new UTF8Encoding(false)))foreach(var s in samples)log.WriteLine(JsonSerializer.Serialize(s));
            var leaf=samples.GroupBy(s=>s.Frames[0]).ToDictionary(g=>g.Key,g=>g.Count());
            // Inclusive counts count each function at most once per sample, even under recursion.
            var inclusive=samples.SelectMany(s=>s.Frames.Distinct()).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count());
            return new(engine,task,repeat,watch.Elapsed.TotalMilliseconds,p.TotalProcessorTime.TotalMilliseconds,samples.Length,contextFailures,symbolFailures,unwindFailures,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe))),digest.Hash,digest.Bytes,leaf,inclusive);
        }finally {
            if(!p.HasExited){p.Kill(true);await p.WaitForExitAsync();}
            if(thread!=IntPtr.Zero)Native.CloseHandle(thread);if(symbols)Native.SymCleanup(p.Handle);
            Marshal.FreeHGlobal(contextRaw);Marshal.FreeHGlobal(frame);
        }
    }

    static void SetAddress(IntPtr frame,int offset,ulong value){Marshal.WriteInt64(frame,offset,unchecked((long)value));Marshal.WriteInt32(frame,offset+12,3);}
    static string? Resolve(IntPtr process,ulong address)
    {
        IntPtr buffer=Marshal.AllocHGlobal(4096);try {
            Marshal.Copy(new byte[4096],0,buffer,4096);Marshal.WriteInt32(buffer,0,88);Marshal.WriteInt32(buffer,80,2048);
            if(!Native.SymFromAddr(process,address,out _,buffer))return null;
            return Marshal.PtrToStringAnsi(IntPtr.Add(buffer,84),Marshal.ReadInt32(buffer,76));
        }finally{Marshal.FreeHGlobal(buffer);}
    }
    static async Task<(long Bytes,string Hash)> Digest(Stream stream)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long bytes=0;int n;
        while((n=await stream.ReadAsync(buffer))>0){hash.AppendData(buffer,0,n);bytes+=n;}
        return(bytes,Convert.ToHexString(hash.GetHashAndReset()));
    }
    static async Task SelfTest()
    {
        string exe=Path.GetFullPath("tools/profiling/canary.exe");
        var r=await Collect(exe,[],"canary","self-test",0);
        int hits=r.Leaf.Where(p=>p.Key.Contains("profiler_canary_hot",StringComparison.Ordinal)).Sum(p=>p.Value);
        File.WriteAllText("results/profiles/canary-observation.json",JsonSerializer.Serialize(r,Json));
        Require(r.Samples>=20&&hits>r.Samples/4,$"Canary hot function did not receive expected leaf samples ({hits}/{r.Samples})");
        Require(r.Inclusive.Any(p=>p.Key.Contains("main",StringComparison.Ordinal)),"Canary stacks did not unwind to main");
        File.WriteAllText("results/profiles/validation.json",JsonSerializer.Serialize(new{Seed,Canary=r,HotFunctionSamples=hits,Scope="Controlled exported native hot-loop attribution and caller unwinding; output equivalence on every profiled benchmark; no claim of CPU-time attribution"},Json));
        Console.WriteLine($"PASS: sampler canary {hits}/{r.Samples} hot-function samples");
    }
    static void WriteSummary(List<Observation> observations)
    {
        var md=new StringBuilder("# Native Rust diagnostic stack samples\n\nSeparate release builds add debug information only. Their timings are not benchmark results. Samples are wall-clock instruction-pointer/call-stack snapshots of the owned main thread, including waiting/kernel locations. They are not ETW CPU samples or allocation counts. The sampler briefly suspends the child and can perturb execution; percentages are diagnostic evidence, not exact CPU-time shares. Three profiles per case; seeded 1–4 ms sleeps; up to 24 unwind frames; local PDBs.\n\n");
        foreach(var group in observations.GroupBy(r=>(r.Engine,r.Task))) {
            int total=group.Sum(r=>r.Samples);md.AppendLine($"## {group.Key.Engine} / {group.Key.Task}\n\n{total} samples across three runs; {group.Sum(r=>r.UnwindFailures)} samples without a caller.\n\n| Leaf location | Samples | Percent |\n|---|---:|---:|");
            foreach(var row in group.SelectMany(r=>r.Leaf).GroupBy(p=>p.Key).Select(g=>(Name:g.Key,Count:g.Sum(p=>p.Value))).OrderByDescending(p=>p.Count).Take(12))md.AppendLine($"| {row.Name.Replace("|","/")} | {row.Count} | {100.0*row.Count/total:F1}% |");
            md.AppendLine("\nInclusive caller counts are retained in observations.json; one sample can include multiple callers.\n");
        }
        File.WriteAllText("results/profiles/README.md",md.ToString());
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    record Expected(string Engine,string Task,long InputBytes,long OutputBytes,string Sha256);

    // Native structures are passed as aligned buffers using Windows AMD64 ABI offsets.
    // DbgHelp is single-threaded: all symbol and unwind calls occur sequentially on this thread.
    static class Native
    {
        public delegate IntPtr FunctionTableCallback(IntPtr process,ulong address);
        public delegate ulong ModuleBaseCallback(IntPtr process,ulong address);
        [DllImport("kernel32.dll",SetLastError=true)]public static extern IntPtr OpenThread(uint access,bool inherit,uint id);
        [DllImport("kernel32.dll",SetLastError=true)]public static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll",SetLastError=true)]public static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetThreadContext(IntPtr thread,IntPtr context);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]public static extern bool CloseHandle(IntPtr handle);
        [DllImport("ntdll.dll")]public static extern int NtQueryInformationThread(IntPtr thread,int informationClass,out IntPtr start,int size,out int returned);
        [DllImport("dbghelp.dll",EntryPoint="SymInitializeW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool SymInitialize(IntPtr process,string? path,bool invade);
        [DllImport("dbghelp.dll")][return:MarshalAs(UnmanagedType.Bool)]public static extern bool SymCleanup(IntPtr process);
        [DllImport("dbghelp.dll")]public static extern uint SymSetOptions(uint options);
        [DllImport("dbghelp.dll",EntryPoint="SymLoadModuleExW",CharSet=CharSet.Unicode,SetLastError=true)]public static extern ulong SymLoadModuleEx(IntPtr process,IntPtr file,string image,string? name,ulong address,uint size,IntPtr data,uint flags);
        [DllImport("dbghelp.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool SymFromAddr(IntPtr process,ulong address,out ulong displacement,IntPtr symbol);
        [DllImport("dbghelp.dll")]public static extern IntPtr SymFunctionTableAccess64(IntPtr process,ulong address);
        [DllImport("dbghelp.dll")]public static extern ulong SymGetModuleBase64(IntPtr process,ulong address);
        [DllImport("dbghelp.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool StackWalk64(uint machine,IntPtr process,IntPtr thread,IntPtr stack,IntPtr context,IntPtr readMemory,FunctionTableCallback functions,ModuleBaseCallback modules,IntPtr translate);
    }
}
