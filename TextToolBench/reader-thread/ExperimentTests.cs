using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

/// <summary>Independent examples protect benchmark output and summary calculations.</summary>
public sealed class ExperimentTests
{
    [Test]
    public void DiagnosticAggregationPreservesMissingClocksAndHighWaterMark()
    {
        string[] records = ["{\"copy_bytes\":4,\"max_ready\":2,\"producer_cpu_ns\":5}",
            "{\"copy_bytes\":6,\"max_ready\":3,\"producer_cpu_ns\":null}"];
        Assert.That(ProfileReport.AggregateMetric(records, "copy_bytes"), Is.EqualTo(10));
        Assert.That(ProfileReport.AggregateMetric(records, "max_ready"), Is.EqualTo(3));
        Assert.That(ProfileReport.AggregateMetric(records, "lifetimes"), Is.EqualTo(2));
        Assert.That(ProfileReport.AggregateMetric(records, "producer_cpu_ns"), Is.Null);
        Assert.That(ProfileReport.AggregateMetric(["{\"copy_bytes\":0}"], "copy_bytes"), Is.EqualTo(0));
        Assert.Throws<ArgumentException>(() => ProfileReport.AggregateMetric([], "copy_bytes"));
        Assert.Throws<ArgumentException>(() => ProfileReport.AggregateMetric(["{\"copy_bytes\":-1}"], "copy_bytes"));
    }
    [Test]
    public void ProfilesIncludeEmptyAndRestartedReaders()
    {
        Experiment.ValidateProfileRecords([
            "{\"copy_bytes\":4,\"read_bytes\":8,\"max_ready\":2,\"copy_calls\":1}",
            "{\"copy_bytes\":0,\"read_bytes\":0,\"max_ready\":0,\"copy_calls\":0}",
            "{\"copy_bytes\":6,\"read_bytes\":6,\"max_ready\":3,\"copy_calls\":2}"
        ], 10, 3);
    }
    [TestCase(-1, 8, 2, 1)]
    [TestCase(4, 3, 2, 1)]
    [TestCase(4, 8, -1, 1)]
    [TestCase(4, 8, 4, 1)]
    [TestCase(4, 8, 2, -1)]
    [TestCase(4, 8, 2, 0)]
    public void ProfileRejectsInvalidCounters(long copied, long read, long high, long calls)
    {
        string json = $"{{\"copy_bytes\":{copied},\"read_bytes\":{read},\"max_ready\":{high},\"copy_calls\":{calls}}}";
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords([json], 4, 3));
    }
    [Test]
    public void ProfileRejectsMissingRecordsAndWrongTotal()
    {
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords([], 1, 3));
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords([], 0, 1));
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords(
            ["{\"copy_bytes\":0,\"read_bytes\":0,\"max_ready\":0,\"copy_calls\":0}"], 1, 3));
        Assert.Throws<OverflowException>(() => Experiment.ValidateProfileRecords([
            "{\"copy_bytes\":9223372036854775807,\"read_bytes\":9223372036854775807,\"max_ready\":1,\"copy_calls\":1}",
            "{\"copy_bytes\":1,\"read_bytes\":1,\"max_ready\":1,\"copy_calls\":1}"
        ], 0, 1));
    }
    [Test]
    public void EmptyFileProfileIsValidButInvalidEnvelopeIsRejected()
    {
        string[] empty = ["{\"copy_bytes\":0,\"read_bytes\":0,\"max_ready\":0,\"copy_calls\":0}"];
        Experiment.ValidateProfileRecords(empty, 0, 1);
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords(empty, -1, 1));
        Assert.Throws<InvalidOperationException>(() => Experiment.ValidateProfileRecords(empty, 0, 0));
    }
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
