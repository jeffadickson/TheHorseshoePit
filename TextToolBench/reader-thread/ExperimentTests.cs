using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

/// <summary>Independent examples protect benchmark output and summary calculations.</summary>
public sealed class ExperimentTests
{
    [TestCase("", "literal", "", 1)]
    [TestCase("\n", "literal", "", 1)]
    [TestCase("Prince", "literal", "Prince\n", 0)]
    [TestCase("Prince\n", "literal", "Prince\n", 0)]
    [TestCase("prince\nPrince Prince\n", "literal", "Prince Prince\n", 0)]
    [TestCase("war\npeace\nWAR\nwar peace", "regex", "war\npeace\nwar peace\n", 0)]
    [TestCase(" \t\n\n x\n\t", "nonblank", " x\n", 0)]
    [TestCase("Prince Prince\nnone\nPrince", "count", "2\n", 0)]
    [TestCase("", "count", "0\n", 1)]
    [TestCase("none\n", "absent", "0\n", 1)]
    [TestCase("missing\n", "absent", "1\n", 0)]
    public void OracleMatchesExplicitExpectedBytes(string input, string task, string expected, int exit)
    {
        var spec = new Experiment.TaskSpec(task, task is "count" or "absent" ? ["-Fc"] : ["-F"], "missing");
        var actual = Experiment.Oracle(Encoding.ASCII.GetBytes(input), spec);
        byte[] bytes = Encoding.ASCII.GetBytes(expected);
        Assert.That(actual, Is.EqualTo(((long)bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), exit)));
    }
    [Test]
    public void UnknownOracleTaskFails() => Assert.Throws<ArgumentException>(() =>
        Experiment.Oracle("x"u8.ToArray(), new("bad", [], "x")));

    [TestCase(0, 1)] [TestCase(0.25, 2)] [TestCase(0.5, 3)] [TestCase(0.75, 4)] [TestCase(1, 5)]
    public void QuantilesInterpolateSortedObservations(double p, double expected) =>
        Assert.That(Experiment.Quantile([5, 1, 3], p), Is.EqualTo(expected));

    [Test]
    public void MedianEvenCountAndSingleton()
    {
        Assert.That(Experiment.Quantile([10, 4, 2, 6], 0.5), Is.EqualTo(5));
        Assert.That(Experiment.Quantile([7], 0.75), Is.EqualTo(7));
    }
}
