using System.Diagnostics;
using System.Text;

// Reproducible boundary probe. Standalone: dotnet run --file scripts/FrawkProbe.cs.
// Compare byte counts rather than printing the deliberately very long field.
string exe=Path.GetFullPath(args.Length>0?args[0]:"tools/frawk/target/release/frawk.exe");
Directory.CreateDirectory("results/frawk-bug");Directory.CreateDirectory("data");
string path=Path.GetFullPath("data/frawk-boundary.txt");
string label=args.Length>1?args[1]:"original";
using var csv=new StreamWriter($"results/frawk-bug/boundaries-{label}.csv");csv.WriteLine("field_length,program,exit,output_bytes,expected_bytes,pass,short_output");
foreach(int n in new[]{1,31,63,64,65,127,128,129,511,512,513,4095,4096,4097,32767,32768,32769,65535,65536,65537,131071,131072,131073,262144}) {
    File.WriteAllText(path,new string('x',n)+" y\n",new UTF8Encoding(false));
    foreach(var (name,program,expected) in new[]{("field","NF {print $1}",new string('x',n)+"\n"),("nf","{print NF}","2\n"),("length","{print length($1)}",n+"\n"),("whole","{print length($0), NF, length($1), $2}",$"{n+2} 2 {n} y\n")}) {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string a in new[]{"-B","interp",program,path})info.ArgumentList.Add(a);
        using var process=Process.Start(info)!;var errors=process.StandardError.ReadToEndAsync();string output=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();string error=await errors;
        bool pass=process.ExitCode==0&&output==expected;
        string excerpt=output.Length<100?output.Replace("\n","\\n").Replace("\r","\\r").Replace(",",";"):"<long field>";
        csv.WriteLine($"{n},{name},{process.ExitCode},{Encoding.UTF8.GetByteCount(output)},{Encoding.UTF8.GetByteCount(expected)},{pass},{excerpt}");csv.Flush();
        if(!pass)Console.WriteLine($"FAIL n={n} {name}: {excerpt} {error}");
    }
}
// Shrink the single-field failure by binary search, then check adjacent lengths explicitly.
if(!await FieldWorks(131072)) {
    int low=1,high=131072;
    while(low<high){int middle=low+(high-low)/2;if(await FieldWorks(middle))low=middle+1;else high=middle;}
    var text=new StringBuilder($"First failing length in the monotonic search range: {low}\n");
    for(int n=Math.Max(1,low-3);n<=low+3;n++)text.AppendLine($"{n}: {(await FieldWorks(n)?"PASS":"FAIL")}");
    File.WriteAllText($"results/frawk-bug/minimized-{label}.txt",text.ToString());Console.WriteLine(text);
}
async Task<bool> FieldWorks(int n) {
    File.WriteAllText(path,new string('x',n)+" y\n",new UTF8Encoding(false));
    var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string a in new[]{"-B","interp","NF {print $1}",path})start.ArgumentList.Add(a);
    using var p=Process.Start(start)!;var error=p.StandardError.ReadToEndAsync();string output=await p.StandardOutput.ReadToEndAsync();await p.WaitForExitAsync();await error;return p.ExitCode==0&&output==new string('x',n)+"\n";
}
