using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

/// <summary>Independent examples protect benchmark output and summary calculations.</summary>
public sealed class ExperimentTests
{
    [Test]
    public void ProfileSummaryReadsClosedLogInSelectedCohort()
    {
        string temp = Path.GetTempPath();
        Assert.That(Directory.Exists(temp), Is.True);
        string root = Path.Combine(temp, "texttool-profile-" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(root), Is.False);
        Directory.CreateDirectory(root);
        try {
            string directory = root;
            foreach (string part in new[] { "results", "copy", "profile" }) {
                directory = Path.Combine(directory, part);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            }
            var counters = new Dictionary<string, long?>();
            foreach (string metric in new[] { "wall_ns", "producer_read_ns", "producer_wait_ns", "producer_lock_ns",
                "consumer_wait_ns", "consumer_lock_ns", "copy_ns", "stop_ns", "read_calls", "copy_calls", "read_bytes",
                "copy_bytes", "full_waits", "empty_waits", "max_ready", "producer_cpu_ns", "main_cpu_ns" })
                counters[metric] = metric.EndsWith("_ns", StringComparison.Ordinal) ? 1000000 : 3;
            counters["producer_cpu_ns"] = null;
            string record = System.Text.Json.JsonSerializer.Serialize(counters);
            string line = System.Text.Json.JsonSerializer.Serialize(new {
                Observation = new { Engine = "avx2", Task = "count" }, QueueRecords = new[] { record }
            });
            // Deliberately close the writer before invoking the report, matching
            // the production lifecycle that Windows file-sharing rules require.
            using (var writer = new StreamWriter(Path.Combine(directory, "observations.jsonl"))) writer.WriteLine(line);
            ProfileReport.Write(root, "copy/profile");
            string summary = File.ReadAllText(Path.Combine(directory, "summary.csv")).Replace("\r\n", "\n");
            Assert.That(summary, Does.Contain("avx2,count,copy_ms,ms,1,1,1.000000\n"));
            Assert.That(summary, Does.Contain("avx2,count,producer_cpu_ms,ms,1,0,NA\n"));
            Assert.That(summary, Does.Contain("avx2,count,lifetimes,count,1,1,1.000000\n"));
            Assert.That(File.ReadAllLines(Path.Combine(directory, "summary.csv")), Has.Length.EqualTo(19));
            Assert.Throws<DirectoryNotFoundException>(() => ProfileReport.Write(root));
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

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
