using System.Reflection;
using System.Text.Json;

/// <summary>Challenge campaign completeness and the actual HTML generator in isolated temporary checkouts.</summary>
[NonParallelizable]
public class ReportTests
{
    string original="",folder="";

    [SetUp]
    public void CreateIsolatedCheckout()
    {
        original=Directory.GetCurrentDirectory();string parent=Path.GetTempPath();
        if(!Directory.Exists(parent))throw new DirectoryNotFoundException(parent);
        folder=Path.Combine(parent,"TextToolBench-DiffReport-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(folder,"data"));Directory.CreateDirectory(Path.Combine(folder,"results/diff"));
        Directory.SetCurrentDirectory(folder);
    }

    [TearDown]
    public void RemoveOnlyOwnedTemporaryCheckout()
    {
        Directory.SetCurrentDirectory(original);
        string parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(folder).StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(folder).StartsWith("TextToolBench-DiffReport-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe cleanup target");
        if(Directory.Exists(folder))Directory.Delete(folder,true);
    }

    static object Row(int round,string engine="example-engine")=>new{Fixture="example",Scale=1024,BeforeBytes=1024,AfterBytes=1024,Engine=engine,Round=round,Milliseconds=round+1d,CpuMilliseconds=0d,PeakWorkingSetBytes=1048576L,ExitCode=1,PatchBytes=50L,PatchSha256="example",Added=1,Deleted=1,Hunks=1};
    static void WriteCampaign(int[] rounds,int expected=1,string engine="example-engine")
    {
        File.WriteAllText("results/diff/environment.json",JsonSerializer.Serialize(new{Cells=expected}));
        File.WriteAllLines("results/diff/runs.jsonl",rounds.Select(r=>JsonSerializer.Serialize(Row(r,engine))));
    }
    static async Task<int> Generate()
    {
        // Invoke the real entry point; no third-party processes are launched in report mode.
        MethodInfo main=typeof(DiffModel).Assembly.GetType("DiffBench")!.GetMethod("Main",BindingFlags.Public|BindingFlags.Static)!;
        return await (Task<int>)main.Invoke(null,[new[]{"--report"}])!;
    }
    [TestCase("example-engine")][TestCase("git-myers")]
    public async Task CompleteCampaignProducesCorrectStatisticsAndOfflineHtml(string engine)
    {
        WriteCampaign([-1,6,1,4,0,3,5,2],engine:engine);Assert.That(await Generate(),Is.Zero);
        using var json=JsonDocument.Parse(File.ReadAllText("results/diff/summary.json"));var cell=json.RootElement[0];
        Assert.Multiple(()=>{
            Assert.That(cell.GetProperty("N").GetInt32(),Is.EqualTo(7));Assert.That(cell.GetProperty("Median").GetDouble(),Is.EqualTo(4));
            Assert.That(cell.GetProperty("Min").GetDouble(),Is.EqualTo(1));Assert.That(cell.GetProperty("Max").GetDouble(),Is.EqualTo(7));
            Assert.That(cell.GetProperty("Q1").GetDouble(),Is.EqualTo(2));Assert.That(cell.GetProperty("Q3").GetDouble(),Is.EqualTo(6));
            Assert.That(File.ReadAllText("results/diff/report.html"),Does.Contain(engine).And.Contain("4.00").And.Not.Contain("<script"));
            Assert.That(cell.GetProperty("PeakWorkingSetBytes").ValueKind,Is.EqualTo(engine.StartsWith("git-")?JsonValueKind.Null:JsonValueKind.Number));
        });
    }
    [Test]
    public async Task ProducerClosesItsWriterBeforeReportReadsTheLog()
    {
        WriteCampaign([]);
        MethodInfo save=typeof(DiffModel).Assembly.GetType("DiffBench")!.GetMethod("SaveAndReport",BindingFlags.NonPublic|BindingFlags.Static)!;
        Func<StreamWriter,Task> produce=async writer=>{
            foreach(int round in new[]{-1,0,1,2,3,4,5,6})await writer.WriteLineAsync(JsonSerializer.Serialize(Row(round)));
        };
        await (Task)save.Invoke(null,[produce])!;
        Assert.That(File.Exists("results/diff/report.html"),Is.True);
        Assert.That(File.ReadAllLines("results/diff/runs.jsonl"),Has.Length.EqualTo(8));
    }
    [TestCase("missing")][TestCase("no-warmup")][TestCase("duplicate-round")][TestCase("out-of-range")][TestCase("missing-cell")][TestCase("empty")]
    public async Task IncompleteCampaignDoesNotPublish(string problem)
    {
        int[] rounds=problem switch {
            "missing"=>[-1,0,1,2,3,4,5],"no-warmup"=>[0,0,1,2,3,4,5,6],"duplicate-round"=>[-1,0,0,2,3,4,5,6],
            "out-of-range"=>[-1,0,1,2,3,4,5,7],"empty"=>[],_=>[-1,0,1,2,3,4,5,6]
        };
        WriteCampaign(rounds,problem=="missing-cell"?2:problem=="empty"?0:1);
        Assert.That(await Generate(),Is.EqualTo(1));Assert.That(File.Exists("results/diff/report.html"),Is.False);
    }
    [Test]
    public async Task MalformedJsonDoesNotPublish()
    {
        WriteCampaign([-1,0,1,2,3,4,5,6]);File.WriteAllText("results/diff/runs.jsonl","{ malformed");
        Assert.That(await Generate(),Is.EqualTo(1));Assert.That(File.Exists("results/diff/report.html"),Is.False);
    }
}
