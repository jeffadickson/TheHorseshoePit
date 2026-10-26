namespace SedBench.Tests;

/// <summary>Independent examples and seeded properties for the byte reference, not timing assertions.</summary>
public class Tests
{
    /// <summary>Challenge case, digit groups, record selection and missing terminators.</summary>
    [TestCase("sed-literal","Prince PrincePrince prince\n","PRINCE PRINCEPRINCE prince\n")]
    [TestCase("sed-regex","12x0 456\n7","#x# #\n#")]
    [TestCase("sed-regex","no digits\n","no digits\n")]
    [TestCase("sed-nonblank","\n \t\nword\n\r\n","word\n\r\n")]
    [TestCase("sed-nonblank"," \t","")]
    [TestCase("sed-nonblank","word","word")]
    [TestCase("sed-select","prince\nPrince\nPrince","Prince\nPrince")]
    [TestCase("sed-select","other\n","")]
    [TestCase("sed-transliterate","aeiou AEIOU y\r\n","AEIOU AEIOU y\r\n")]
    [TestCase("sed-head","","")]
    public void Examples(string task,string input,string expected)=>Assert.That(SedSemantics.Transform(System.Text.Encoding.Latin1.GetBytes(input),task),Is.EqualTo(System.Text.Encoding.Latin1.GetBytes(expected)));

    /// <summary>Check either side of the 100-record boundary, with and without a final LF.</summary>
    [TestCase(0,true)][TestCase(1,true)][TestCase(99,true)][TestCase(100,true)][TestCase(101,true)][TestCase(250,true)]
    [TestCase(0,false)][TestCase(1,false)][TestCase(99,false)][TestCase(100,false)][TestCase(101,false)][TestCase(250,false)]
    public void HeadBoundary(int records,bool finalLf)
    {
        string input=string.Join('\n',Enumerable.Range(1,records).Select(i=>i.ToString()));if(records>0&&finalLf)input+='\n';
        string expected=string.Join('\n',Enumerable.Range(1,Math.Min(records,100)).Select(i=>i.ToString()));
        if(records>0&&(finalLf||records>100))expected+='\n';
        Assert.That(SedSemantics.Transform(System.Text.Encoding.ASCII.GetBytes(input),"sed-head"),Is.EqualTo(System.Text.Encoding.ASCII.GetBytes(expected)));
    }

    /// <summary>Each transformation is a projection; arbitrary binary input stays byte-representable.</summary>
    [Test]
    public void SeededByteProperties()
    {
        var random=new Random(20261107);
        foreach(string task in new[]{"sed-literal","sed-regex","sed-nonblank","sed-select","sed-head","sed-transliterate"})
            for(int i=0;i<200;i++) {
                byte[] input=new byte[random.Next(0,4097)];random.NextBytes(input);
                byte[] once=SedSemantics.Transform(input,task);
                Assert.That(SedSemantics.Transform(once,task),Is.EqualTo(once),$"seed 20261107, case {i}, {task}");
            }
        byte[] binary=[0,255,128,80,114,105,110,99,101,13,10];
        Assert.That(SedSemantics.Transform(binary,"sed-literal"),Is.EqualTo(new byte[]{0,255,128,80,82,73,78,67,69,13,10}));
    }

    /// <summary>Unsupported identifiers must fail explicitly, even on empty input.</summary>
    [Test]
    public void UnknownTaskIsRejected()=>Assert.That(()=>SedSemantics.Transform([],"unknown"),Throws.ArgumentException);
}
