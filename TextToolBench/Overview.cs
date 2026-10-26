using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Generate a deterministic, self-contained HTML overview from validated retained measurements.</summary>
internal static class Overview
{
    /// <summary>Read from the checkout root; no experiments, downloads or system settings are changed.</summary>
    public static int Main(string[] args)
    {
        try {
            string root=args.Length==0?Directory.GetCurrentDirectory():Path.GetFullPath(args[0]);
            if(!Directory.Exists(root)||!Directory.Exists(Path.Combine(root,"results")))throw new DirectoryNotFoundException("Run from a benchmark checkout.");
            var cells=JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(root,"results/summary.json")),OverviewJson.Default.OverviewCellArray)
                ??throw new InvalidDataException("Missing summary data.");
            OverviewData data=OverviewModel.Build(cells);
            string template=File.ReadAllText(Path.Combine(root,"Overview.template.html"));
            if(template.Split("__DATA__",StringSplitOptions.None).Length!=2)throw new InvalidDataException("Expected exactly one embedded-data placeholder.");
            // Source-generated serialization escapes HTML-sensitive characters and supports Native AOT.
            string html=template.Replace("__DATA__",JsonSerializer.Serialize(data,OverviewJson.Default.OverviewData),StringComparison.Ordinal)
                .Replace("__MEASURED__",data.Measured.ToString("N0",CultureInfo.InvariantCulture),StringComparison.Ordinal)
                .Replace("__WARMUPS__",data.Warmups.ToString("N0",CultureInfo.InvariantCulture),StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(root,"results/overview.html"),html,new UTF8Encoding(false));
            Console.WriteLine($"Overview: {data.Cohorts.Length} cohorts, {data.Measured} measured commands, {data.Warmups} warmups.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
    }
}

[JsonSerializable(typeof(OverviewCell[]))]
[JsonSerializable(typeof(OverviewData))]
internal partial class OverviewJson:JsonSerializerContext;
