using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

// Property-style CLI regression: varied buffer boundaries, leading whitespace, prior records,
// final-newline conventions and randomized long field lengths. Seed is retained in the evidence.
if(args.Length<2)throw new ArgumentException("Usage: FrawkRegression.cs EXE LABEL [--expect-failures]");
const int seed=20261105;
var random=new Random(seed);int failed=0,checks=0;
string exe=Path.GetFullPath(args[0]),label=args[1],path=Path.GetFullPath("data/frawk-property.txt");
Directory.CreateDirectory("results/frawk-bug");
using var log=new StreamWriter($"results/frawk-bug/property-{label}.csv");log.WriteLine("case,field_length,program,pass,expected_sha256,actual_sha256");
int[] lengths=new[]{8187,8188,8189,8190,8191,8192,8193,131072}.Concat(Enumerable.Range(0,64).Select(_=>random.Next(1,524289))).ToArray();
for(int index=0;index<lengths.Length;index++) {
    int n=lengths[index];bool previous=random.Next(2)==0,finalNewline=random.Next(2)==0,third=random.Next(2)==0;
    string leading=new string(' ',random.Next(4))+new string('\t',random.Next(3));string word=new string((char)('a'+random.Next(26)),n);
    string text=(previous?"prior record\n":"")+leading+word+" \ty"+(third?"  z":"")+(finalNewline?"\n":"");
    File.WriteAllText(path,text,new UTF8Encoding(false));
    foreach(var (name,program,expected) in new[]{("field","NF {print $1}",(previous?"prior\n":"")+word+"\n"),("counts","{print NF, length($1), length($2)}",(previous?"2 5 6\n":"")+$"{(third?3:2)} {n} 1\n")}) {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(string a in new[]{"-B","interp",program,path})info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var errors=p.StandardError.ReadToEndAsync();string output=await p.StandardOutput.ReadToEndAsync();await p.WaitForExitAsync();string error=await errors;
        bool pass=p.ExitCode==0&&output==expected;if(!pass)failed++;checks++;
        string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        log.WriteLine($"{index},{n},{name},{pass},{Hash(expected)},{Hash(output)}");
    }
}
File.WriteAllText($"results/frawk-bug/property-{label}.txt",$"Seed: {seed}\nChecks: {checks}\nFailed: {failed}\nExecutable SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe)))}\n");
Console.WriteLine($"{label}: {checks-failed}/{checks} passed; seed {seed}");
if(args.Contains("--expect-failures")?failed==0:failed!=0)Environment.ExitCode=1;
