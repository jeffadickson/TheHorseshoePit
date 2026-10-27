using System.Text;
using FsCheck;
using FsCheck.Fluent;

/// <summary>Independent bounded references, malformed inputs and library-based property checks.</summary>
public class ModelTests
{
    [TestCase("identical")][TestCase("scattered")][TestCase("insert-delete")][TestCase("moved")]
    [TestCase("repeated")][TestCase("different")][TestCase("long-line")]
    public void FixturesAreDeterministicAndRespectIdentity(string workload)
    {
        var a=DiffModel.Pair("Alpha\nBeta\nGamma\n",1024,workload);var b=DiffModel.Pair("Alpha\nBeta\nGamma\n",1024,workload);
        Assert.Multiple(()=>{
            Assert.That(a.Before,Is.EqualTo(b.Before));Assert.That(a.After,Is.EqualTo(b.After));
            Assert.That(a.Before.Length,Is.GreaterThanOrEqualTo(1024));
            Assert.That(a.Before.SequenceEqual(a.After),Is.EqualTo(workload=="identical"));
            Assert.That(a.Before[^1],Is.EqualTo(10));Assert.That(a.After[^1],Is.EqualTo(10));
        });
    }

    [TestCase(0,"x")][TestCase(-1,"x")][TestCase(1,"")][TestCase(1,"\n\r\n")]
    public void InvalidFixtureInputsAreRejected(int size,string text)=>Assert.Throws<ArgumentException>(()=>DiffModel.Pair(text,size,"identical"));
    [Test] public void UnknownWorkloadIsRejected()=>Assert.Throws<ArgumentException>(()=>DiffModel.Pair("x",1,"unknown"));
    [TestCase(1)][TestCase(2)][TestCase(3)]
    public void TinyFixturesRemainValid(int size)
    {
        foreach(string task in new[]{"identical","scattered","insert-delete","moved","repeated","different","long-line"}) {
            var pair=DiffModel.Pair("x",size,task);Assert.That(pair.Before,Is.Not.Empty);Assert.That(pair.After,Is.Not.Empty);
        }
    }
    [Test]
    public void PatchStatisticsCountContentThatResemblesHeaders()
    {
        var stats=DiffModel.Statistics(Encoding.ASCII.GetBytes("diff --git a/a b/b\n--- a\n+++ b\n@@ -1,2 +1,2 @@\n--- content\n+++ content\n same\n\\ No newline at end of file\n@@ -5 +5 @@\n-x\n+y\n"));
        Assert.That(stats,Is.EqualTo((2,2,2)));Assert.That(DiffModel.Statistics([]),Is.EqualTo((0,0,0)));
        Assert.That(DiffModel.Statistics("--- a\n+++ b\n"u8.ToArray()),Is.EqualTo((0,0,0)));
    }
    [Test]
    public void ExactDistanceExamples()
    {
        Assert.Multiple(()=>{
            Assert.That(DiffModel.MinimumEdits([],[]),Is.Zero);Assert.That(DiffModel.MinimumEdits(["x"],[]),Is.EqualTo(1));
            Assert.That(DiffModel.MinimumEdits([],["x","y"]),Is.EqualTo(2));Assert.That(DiffModel.MinimumEdits(["x"],["y"]),Is.EqualTo(2));
            Assert.That(DiffModel.MinimumEdits(["a","b","c"],["b","c","a"]),Is.EqualTo(2));
            Assert.That(DiffModel.MinimumEdits(["a","a","b"],["a","b","b"]),Is.EqualTo(2));
        });
    }
    [Test]
    public void QuadraticEstimateTrimsEqualEndsAndCountsEmptyLines()
    {
        Assert.Multiple(()=>{
            Assert.That(DiffModel.QuadraticTableBytes([],[]),Is.EqualTo(4));
            Assert.That(DiffModel.QuadraticTableBytes("a\nb\nc\n"u8.ToArray(),"a\nx\nc\n"u8.ToArray()),Is.EqualTo(16));
            Assert.That(DiffModel.QuadraticTableBytes("a\nb\n"u8.ToArray(),"x\ny\n"u8.ToArray()),Is.EqualTo(36));
            Assert.That(DiffModel.QuadraticTableBytes("x"u8.ToArray(),"x\ny"u8.ToArray()),Is.EqualTo(8));
            Assert.That(DiffModel.QuadraticTableBytes("a\nb\n"u8.ToArray(),"a\nb"u8.ToArray()),Is.EqualTo(8));
            Assert.That(DiffModel.QuadraticTableBytes("a\nb\nc"u8.ToArray(),"a\nc"u8.ToArray()),Is.EqualTo(8));
            Assert.That(DiffModel.QuadraticTableBytes([],"a\nb\n"u8.ToArray()),Is.EqualTo(12));
        });
    }
    [Test]
    public void FixturesRepresentTheirNamedEdits()
    {
        string[] Lines(byte[] bytes)=>Encoding.UTF8.GetString(bytes).TrimEnd('\n').Split('\n');
        var exact=DiffModel.Pair("A\n",11,"identical");Assert.That(exact.Before.Length,Is.EqualTo(11));
        var source=DiffModel.Pair("東京\r\nBeta\n",1024,"identical");
        Assert.That(Encoding.UTF8.GetString(source.Before),Does.Contain("東京").And.Not.Contain("\r"));
        var repeat=DiffModel.Pair("original source\n",1024,"repeated");
        Assert.That(Lines(repeat.Before).Distinct().Count(),Is.EqualTo(7));Assert.That(Lines(repeat.Before).All(x=>x.StartsWith("repeat ")),Is.True);
        Assert.That(Lines(repeat.After).Length,Is.EqualTo(Lines(repeat.Before).Length));
        Assert.That(Lines(repeat.After).Count(x=>x=="unique insertion"),Is.EqualTo(1));
        var moved=DiffModel.Pair("A\nB\nC\n",1024,"moved");
        Assert.That(Lines(moved.After).Order(),Is.EqualTo(Lines(moved.Before).Order()));
        Assert.That(Lines(moved.After)[^1],Is.EqualTo(Lines(moved.Before)[Lines(moved.Before).Length/4-1]));
        var inserted=DiffModel.Pair("A\nB\n",1024,"insert-delete");var before=Lines(inserted.Before);var after=Lines(inserted.After);
        Assert.That(after.Length,Is.EqualTo(before.Length));Assert.That(after.Count(x=>x.StartsWith("inserted ")),Is.EqualTo(before.Length/10));
        Assert.That(before.Except(after).Count(),Is.EqualTo(before.Length/10));
        var different=DiffModel.Pair("original source\n",1024,"different");
        Assert.That(Lines(different.After).All(x=>x.StartsWith("new content ")),Is.True);Assert.That(Lines(different.Before).Intersect(Lines(different.After)),Is.Empty);
        var scattered=DiffModel.Pair("A\nB\n",1024,"scattered");
        Assert.That(Lines(scattered.After).Count(x=>x.EndsWith(" EDIT")),Is.GreaterThan(1));
        foreach(int size in new[]{1,2,3,131072}) {
            var longLine=DiffModel.Pair("source",size,"long-line");
            Assert.That(longLine.Before.Length,Is.EqualTo(size+1));Assert.That(longLine.After.Length,Is.EqualTo(size+1));
            Assert.That(longLine.After.Take(size).Count(x=>x==(byte)'y'),Is.EqualTo(1));
            Assert.That(longLine.Before.Zip(longLine.After).Count(x=>x.First!=x.Second),Is.EqualTo(1));
        }
    }
    /// <summary>FsCheck generates and shrinks bounded arrays; fixed replay seed is retained in the report.</summary>
    [Test]
    public void EditDistanceAgreesWithIndependentLongestCommonSubsequence()
    {
        var arrays=Arb.From(Gen.Choose(0,10).SelectMany(n=>Gen.ArrayOf(Gen.Choose(0,3),n)),
            values=>values.Length==0?Enumerable.Empty<int[]>():new[]{values[..(values.Length/2)]});
        var property=Prop.ForAll(arrays,arrays,(int[] a,int[] b)=>{
            string[] x=a.Select(i=>i.ToString()).ToArray(),y=b.Select(i=>i.ToString()).ToArray();
            var lcs=new int[x.Length+1,y.Length+1];
            for(int i=1;i<=x.Length;i++)for(int j=1;j<=y.Length;j++)lcs[i,j]=x[i-1]==y[j-1]?lcs[i-1,j-1]+1:Math.Max(lcs[i-1,j],lcs[i,j-1]);
            int distance=DiffModel.MinimumEdits(x,y);
            return distance==x.Length+y.Length-2*lcs[x.Length,y.Length]&&distance==DiffModel.MinimumEdits(y,x);
        });
        Check.One(Config.QuickThrowOnFailure.WithMaxTest(1000).WithReplay(new Replay(new Rnd(20261027UL,17UL),null)),property);
    }
}
