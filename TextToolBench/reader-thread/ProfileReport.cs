using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Summarizes diagnostic counters across queue restarts and repetitions.
/// Wall intervals from different threads overlap; each metric stays separate.</summary>
internal static class ProfileReport
{
    private static readonly string[] Metrics = ["lifetimes", "wall_ns", "producer_read_ns",
        "producer_wait_ns", "producer_lock_ns", "consumer_wait_ns", "consumer_lock_ns",
        "copy_ns", "stop_ns", "read_calls", "copy_calls", "read_bytes", "copy_bytes",
        "full_waits", "empty_waits", "max_ready", "producer_cpu_ns", "main_cpu_ns"];

    /// <summary>Sum additive counters, take the queue high-water maximum, and
    /// preserve unsupported clocks as missing rather than substituting zero.</summary>
    internal static double? AggregateMetric(string[] records, string metric)
    {
        if (records.Length == 0) throw new ArgumentException("A profile requires queue records");
        if (metric == "lifetimes") return records.Length;
        double result = 0;
        foreach (string record in records) {
            using var json = JsonDocument.Parse(record);
            var value = json.RootElement.GetProperty(metric);
            if (value.ValueKind == JsonValueKind.Null) return null;
            long number = value.GetInt64();
            if (number < 0) throw new ArgumentException("Negative profiling counter");
            result = metric == "max_ready" ? Math.Max(result, number) : result + number;
        }
        return result;
    }

    /// <summary>Write medians for each task/engine/metric without combining
    /// diagnostic intervals into an invalid total CPU-time percentage.</summary>
    internal static void Write(string root, string cohort = "profile")
    {
        string directory = Path.Combine(root, "results", cohort);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var samples = new List<(string Engine, string Task, string Metric, double? Value)>();
        foreach (string line in File.ReadLines(Path.Combine(directory, "observations.jsonl"))) {
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            string engine = row.GetProperty("Observation").GetProperty("Engine").GetString()!;
            string task = row.GetProperty("Observation").GetProperty("Task").GetString()!;
            string[] records = row.GetProperty("QueueRecords").EnumerateArray().Select(r => r.GetString()!).ToArray();
            foreach (string metric in Metrics) samples.Add((engine, task, metric, AggregateMetric(records, metric)));
        }
        var csv = new StringBuilder("Engine,Task,Metric,Unit,Repetitions,Available,Median\n");
        foreach (var group in samples.GroupBy(s => (s.Engine, s.Task, s.Metric))
            .OrderBy(g => g.Key.Engine).ThenBy(g => g.Key.Task).ThenBy(g => g.Key.Metric)) {
            string metric = group.Key.Metric;
            bool duration = metric.EndsWith("_ns", StringComparison.Ordinal);
            string unit = duration ? "ms" : metric.EndsWith("_bytes", StringComparison.Ordinal) ? "bytes" : "count";
            var values = group.Where(s => s.Value.HasValue).Select(s => s.Value!.Value / (duration ? 1e6 : 1)).ToArray();
            string median = values.Length == 0 ? "NA" : Experiment.Quantile(values, 0.5).ToString("F6", CultureInfo.InvariantCulture);
            if (duration) metric = metric[..^3] + "_ms";
            csv.AppendLine($"{group.Key.Engine},{group.Key.Task},{metric},{unit},{group.Count()},{values.Length},{median}");
        }
        File.WriteAllText(Path.Combine(directory, "summary.csv"), csv.ToString());
        Console.WriteLine($"Summarized {samples.Count / Metrics.Length} validated profiling observations.");
    }
}
