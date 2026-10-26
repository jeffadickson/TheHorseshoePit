using System.Reflection;
using System.Text.Json;

namespace SedBench.Tests;

/// <summary>Protect units, cohort boundaries and incomplete-data behavior of the overview generator.</summary>
public class OverviewTests
{
    static OverviewCell Valid=>new("literal","gnu",1048616,7,3,1,5,2,4);
    static OverviewCell[] Campaign()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("overview-summary.json")!;
        return JsonSerializer.Deserialize<OverviewCell[]>(stream)!;
    }

    /// <summary>Validate retained totals, completeness and one-cohort-per-cell assignment.</summary>
    [Test]
    public void CompleteCampaignPreservesAllSessions()
    {
        var result=OverviewModel.Build(Campaign());
        Assert.Multiple(()=>{
            Assert.That(result.Measured,Is.EqualTo(2989));Assert.That(result.Warmups,Is.EqualTo(427));
            Assert.That(result.Cohorts,Has.Length.EqualTo(9));
            Assert.That(result.Cohorts.SelectMany(c=>c.Cells).Count(),Is.EqualTo(427));
            Assert.That(result.Cohorts.Single(c=>c.Id=="grep").Cells.All(c=>c.Engine is "ripgrep" or "grep-current"),Is.True);
            Assert.That(result.Cohorts.Single(c=>c.Id=="tuning").Cells.Any(c=>c.Engine=="ripgrep"),Is.False);
        });
    }

    /// <summary>Reject incomplete, duplicate and unknown-session input rather than silently publishing partial results.</summary>
    [Test]
    public void CorruptedCampaignFailsClosed()
    {
        var cells=Campaign();Assert.That(()=>OverviewModel.Build(cells[..^1]),Throws.TypeOf<InvalidDataException>());
        cells[0]=cells[1];Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();var sameSession=Enumerable.Range(0,cells.Length).Where(i=>OverviewModel.Assign(cells[i])=="tuning").Take(2).ToArray();
        cells[sameSession[0]]=cells[sameSession[1]];Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();cells[0]=cells[0] with{Engine="unknown"};Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();cells[0]=cells[0] with{Engine="grep-built-o2",InputBytes=1234567};Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();int index=Array.FindIndex(cells,c=>c.Engine=="sed-gnu");cells[index]=cells[index] with{Task="field"};Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();for(int i=0;i<cells.Length;i++)if(cells[i].Engine=="selectstring"&&cells[i].InputBytes>=1048616)cells[i]=cells[i] with{Engine="sed-red"};
        Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        cells=Campaign();index=Array.FindIndex(cells,c=>OverviewModel.Assign(c)=="buffer");cells[index]=cells[index] with{InputBytes=1071};
        Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
        Assert.That(()=>OverviewModel.Build(null!),Throws.ArgumentNullException);
    }

    /// <summary>Separate reused engine names by input-size cohort; fresh controls have explicit identities.</summary>
    [TestCase("grep-current",33554436,"grep")][TestCase("grep-current",65580,"small")]
    [TestCase("ripgrep",33554436,"grep")][TestCase("ripgrep",1071,"small")]
    [TestCase("pipeline",1048616,"baseline")][TestCase("reader",65580,"small")]
    [TestCase("selectstring",1048616,"selectstring")][TestCase("selectstring",1071,"small")]
    [TestCase("awk-current",33554436,"rawk")][TestCase("awk-extra-control",33554436,"awk")]
    [TestCase("rawk-stefan-control",33554436,"buffer")][TestCase("sed-uutils",33554436,"sed")]
    public void CohortBoundaries(string engine,long bytes,string expected)=>Assert.That(OverviewModel.Assign(Valid with{Engine=engine,InputBytes=bytes}),Is.EqualTo(expected));

    /// <summary>Valid global operations cannot be substituted into a session that never measured them.</summary>
    [TestCase("baseline","sed-head")][TestCase("selectstring","words")][TestCase("grep","field")]
    [TestCase("small","field")][TestCase("buffer","literal")]
    public void WrongOperationCannotMasqueradeAsACompleteCohort(string cohort,string task)
    {
        var cells=Campaign();int index=Array.FindIndex(cells,c=>OverviewModel.Assign(c)==cohort);
        cells[index]=cells[index] with{Task=task};
        Assert.That(()=>OverviewModel.Build(cells),Throws.TypeOf<InvalidDataException>());
    }

    /// <summary>Malformed identities, counts, units, NaN/infinity and quartile inversions are invalid.</summary>
    [Test]
    public void InvalidCellsAreRejected()
    {
        foreach(var cell in new[]{Valid with{Engine=" "},Valid with{Task="unknown"},Valid with{InputBytes=0},Valid with{InputBytes=-1},Valid with{N=6},Valid with{Median=double.NaN},Valid with{Max=double.PositiveInfinity},Valid with{Min=0},Valid with{Q1=4},Valid with{Q3=2}})
            Assert.That(()=>OverviewModel.ValidateCell(cell),Throws.TypeOf<InvalidDataException>());
        Assert.That(()=>OverviewModel.ValidateCell(null!),Throws.ArgumentNullException);
    }

    /// <summary>Seeded valid order statistics, including equal-valued samples, remain accepted.</summary>
    [Test]
    public void SeededValidSummaries()
    {
        var random=new Random(20261026);
        for(int i=0;i<500;i++) {
            double[] v=Enumerable.Range(0,7).Select(_=>1+random.NextDouble()*1000).Order().ToArray();
            var cell=Valid with{Min=v[0],Q1=v[1],Median=v[3],Q3=v[5],Max=v[6]};
            Assert.That(()=>OverviewModel.ValidateCell(cell),Throws.Nothing,$"seed 20261026 case {i}");
        }
        Assert.That(()=>OverviewModel.ValidateCell(Valid with{Min=1,Q1=1,Median=1,Q3=1,Max=1}),Throws.Nothing);
    }

    /// <summary>Malformed JSON/templates and absent directories fail without publishing a replacement page.</summary>
    [Test]
    public void GeneratorRejectsMalformedInputsAndWritesDeterministically()
    {
        string parent=Path.GetTempPath();Assert.That(Directory.Exists(parent),Is.True);
        string root=Path.Combine(parent,"TextToolBench-overview-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            Assert.That(global::Overview.Main([Path.Combine(root,"missing")]),Is.EqualTo(1));
            string results=Path.Combine(root,"results");Directory.CreateDirectory(results);
            Assert.That(global::Overview.Main([root]),Is.EqualTo(1));
            string summary=Path.Combine(results,"summary.json");File.WriteAllText(summary,"{broken");
            Assert.That(global::Overview.Main([root]),Is.EqualTo(1));
            File.WriteAllText(summary,"null");Assert.That(global::Overview.Main([root]),Is.EqualTo(1));
            File.WriteAllText(summary,JsonSerializer.Serialize(Campaign()));
            File.WriteAllText(Path.Combine(root,"Overview.template.html"),"__DATA__ __DATA__");
            Assert.That(global::Overview.Main([root]),Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(results,"overview.html")),Is.False);
            File.WriteAllText(Path.Combine(root,"Overview.template.html"),"<!doctype html>__DATA__ __MEASURED__ __WARMUPS__");
            Assert.That(global::Overview.Main([root]),Is.Zero);
            byte[] first=File.ReadAllBytes(Path.Combine(results,"overview.html"));
            Assert.That(global::Overview.Main([root]),Is.Zero);
            Assert.That(File.ReadAllBytes(Path.Combine(results,"overview.html")),Is.EqualTo(first));
        }finally{
            // Only this test's newly created GUID directory is eligible for removal.
            if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("TextToolBench-overview-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
    }
}
