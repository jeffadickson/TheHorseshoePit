using System.Diagnostics;
using System.Text;

// Exercise native builds against the installed GNU references before changing command resolution.
// Deterministic cases include buffer growth, blank records and a missing final newline.
string root=Directory.GetCurrentDirectory();
string native=Path.GetFullPath(args[0]);
string runtime=Path.Combine(root,"tools/msys64/usr/bin");
string reference=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git/usr/bin");
const int seed=20261106;var random=new Random(seed);int checks=0;
Directory.CreateDirectory("results/awk-tuned");
string input=Path.GetFullPath("results/awk-tuned/verification-input.txt");
async Task<(int,string)> Run(string exe,string program,bool grep=false) {
    var p=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
    p.Environment["PATH"]=(exe.StartsWith(native)?native+";"+runtime:reference)+";"+Environment.GetEnvironmentVariable("PATH");
    p.Environment["LC_ALL"]="C";
    if(grep)p.ArgumentList.Add("-E");p.ArgumentList.Add(program);p.ArgumentList.Add(input);
    using var child=Process.Start(p)!;var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();await error;return(child.ExitCode,await output);
}
for(int i=0;i<40;i++) {
    int length=i<8?new[]{0,1,8189,8190,8192,65536,131072,262144}[i]:random.Next(1,300000);
    File.WriteAllText(input,"  "+new string('x',length)+" y\n\nPrince war peace\n"+(i%2==0?"tail":"tail\n"),new UTF8Encoding(false));
    foreach(string script in new[]{"NF {print $1}","{n+=NF} END {print n}","END {print NR}","{print NF,length($1)}","BEGIN {RS=\"\"} /^x/ {print NF} /tail$/ {print length($0)}"}) {
        var expected=await Run(Path.Combine(reference,"awk.exe"),script);var actual=await Run(Path.Combine(native,"awk.exe"),script);
        if(actual!=expected||actual.Item1!=0)throw new Exception($"AWK mismatch case {i}: {script}");checks++;
    }
    foreach(string pattern in new[]{"Prince","war|peace","[^ \\t]"}) {
        if(await Run(Path.Combine(native,"grep.exe"),pattern,true)!=await Run(Path.Combine(reference,"grep.exe"),pattern,true))throw new Exception($"grep mismatch case {i}");checks++;
    }
}
// Retain exact corpus equivalence as an installation gate; these are checks, not timings.
foreach(string file in new[]{"1MiB.txt","8MiB.txt","32MiB.txt","small-1KiB.txt","small-64KiB.txt"}) {
    input=Path.GetFullPath(Path.Combine("data",file));
    if(!File.Exists(input))throw new FileNotFoundException("Required corpus fixture is missing",input);
    foreach(string script in new[]{"NF {print $1}","{n+=NF} END {print n}","END {print NR}"}) {
        if(await Run(Path.Combine(native,"awk.exe"),script)!=await Run(Path.Combine(reference,"awk.exe"),script))throw new Exception($"Corpus mismatch: {file}/{script}");checks++;
    }
}
// Malformed programs must fail. A deliberate semantic mutation must alter the output.
if((await Run(Path.Combine(native,"awk.exe"),"BEGIN {")).Item1==0)throw new Exception("Malformed AWK accepted");checks++;
if((await Run(Path.Combine(native,"grep.exe"),"[",true)).Item1!=2)throw new Exception("Malformed regex accepted");checks++;
if(await Run(Path.Combine(native,"awk.exe"),"END {print NR}")==await Run(Path.Combine(native,"awk.exe"),"END {print NR+1}"))throw new Exception("Mutation undetected");checks++;
string report=$"Passed: {checks}; seed: {seed}\nLine/branch coverage: not measured for native installation checks. GNU upstream make check is recorded separately.\n";
File.WriteAllText("results/awk-tuned/installation-verification.txt",report);Console.Write(report);
