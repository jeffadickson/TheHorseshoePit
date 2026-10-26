using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("SedBench.Tests")]

/// <summary>One seven-repeat summary; milliseconds are end-to-end wall time, never CPU time.</summary>
internal sealed record OverviewCell(string Task,string Engine,long InputBytes,int N,double Median,double Min,double Max,double Q1,double Q3);
/// <summary>A comparison session. Cells from different sessions must not share a ranking.</summary>
internal sealed record OverviewCohort(string Id,string Title,string Description,int Measured,int Warmups,OverviewCell[] Cells);
/// <summary>Complete embedded dataset, suitable for offline exploration without network dependencies.</summary>
internal sealed record OverviewData(int Measured,int Warmups,OverviewCohort[] Cohorts);

/// <summary>Validate finite measurements and preserve the original nine experimental cohorts.</summary>
internal static class OverviewModel
{
    internal static readonly string[] Tasks=["literal","regex","count","nonblank","replace","field","words","lines","sed-literal","sed-regex","sed-nonblank","sed-select","sed-head","sed-transliterate"];
    static readonly long[] InputSizes=[1071,65580,1048616,8388671,33554436];
    static readonly (string Id,string Title,int Cells,string Description)[] Definitions=[
        ("baseline","GNU & PowerShell",72,"Eight equivalent operations, three file sizes, three approaches: GNU tools, PowerShell pipelines and buffered .NET readers."),
        ("selectstring","Select-String",9,"Three search operations at three large-file sizes. A separate supplemental session; historical GNU timings are not treated as paired controls."),
        ("grep","ripgrep & GNU grep",24,"Four search operations, interleaved fresh processes. This is the direct ripgrep/GNU comparison at 1, 8 and 32 MiB."),
        ("small","Small-file search",30,"Literal, regex and count at about 1 KiB and 64 KiB. Five approaches interleaved; process startup can dominate."),
        ("rawk","Two Rust AWKs",45,"Unmodified release builds of stefanalfbo/rawk and quinnjr/awk-rs against fresh GNU awk, across five input sizes."),
        ("awk","mawk & frawk",45,"mawk, the frawk bytecode interpreter and a fresh GNU awk control. The frawk Cranelift JIT failed and has no successful timing results."),
        ("buffer","Rust stdout buffering",12,"Paired fresh controls versus separate 64 KiB BufWriter variants. Only command-line stdout buffering changed; three 32 MiB operations."),
        ("tuning","GNU grep build flags",60,"Same source/compiler/runtime: generic O2 versus native O3. The installed binary is an additional reference. Ripgrep was not in this session."),
        ("sed","Sed implementations",130,"GNU sed, BusyBox, uutils/sed and red: six operations, five sizes. sd participates in two replacements only. Identical GNU binaries count once.")
    ];

    /// <summary>Reject invalid units, impossible quartiles, unknown operations and incomplete repetition counts.</summary>
    internal static void ValidateCell(OverviewCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if(string.IsNullOrWhiteSpace(cell.Engine)||!Tasks.Contains(cell.Task,StringComparer.Ordinal)||!InputSizes.Contains(cell.InputBytes)||cell.N!=7)
            throw new InvalidDataException("Invalid operation, identity, input size or repetition count.");
        double[] values=[cell.Min,cell.Q1,cell.Median,cell.Q3,cell.Max];
        if(values.Any(v=>!double.IsFinite(v)||v<=0)||!values.SequenceEqual(values.Order()))
            throw new InvalidDataException("Times must be finite, positive and ordered min/Q1/median/Q3/max.");
    }

    /// <summary>Assign only observed engine/size combinations; an unknown source cannot silently join a comparison.</summary>
    internal static string Assign(OverviewCell cell)
    {
        ValidateCell(cell);bool small=cell.InputBytes<100000;
        return cell.Engine switch {
            "gnu" when !small=>"baseline",
            "pipeline" or "reader"=>small?"small":"baseline",
            "selectstring"=>small?"small":"selectstring",
            "ripgrep" or "grep-current"=>small?"small":"grep",
            "rawk-stefan" or "awk-rs-quinn" or "awk-current"=>"rawk",
            "mawk" or "frawk-interp" or "awk-extra-control"=>"awk",
            "rawk-stefan-control" or "rawk-stefan-buffered" or "awk-rs-quinn-control" or "awk-rs-quinn-buffered"=>"buffer",
            "grep-built-o2" or "grep-native-o3" or "grep-installed-fresh"=>"tuning",
            "sed-gnu" or "sed-busybox" or "sed-uutils" or "sed-red" or "sd"=>"sed",
            _=>throw new InvalidDataException("Unknown experimental cohort.")
        };
    }

    /// <summary>Require all 427 distinct cells, so the overview cannot make a partial campaign look complete.</summary>
    internal static OverviewData Build(OverviewCell[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if(cells.Length!=427||cells.Select(c=>(c.Task,c.Engine,c.InputBytes)).Distinct().Count()!=cells.Length)
            throw new InvalidDataException("Expected 427 distinct summary cells.");
        var groups=cells.GroupBy(Assign).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var cohorts=new List<OverviewCohort>();
        foreach(var definition in Definitions) {
            if(!groups.TryGetValue(definition.Id,out var members)||members.Length!=definition.Cells)
                throw new InvalidDataException($"Incomplete cohort: {definition.Id}");
            if(members.Any(c=>!Allowed(definition.Id,c)))throw new InvalidDataException("An operation or size does not belong to its recorded cohort.");
            cohorts.Add(new(definition.Id,definition.Title,definition.Description,members.Sum(c=>c.N),members.Length,
                members.OrderBy(c=>c.InputBytes).ThenBy(c=>c.Task,StringComparer.Ordinal).ThenBy(c=>c.Engine,StringComparer.Ordinal).ToArray()));
        }
        return new(cohorts.Sum(c=>c.Measured),cohorts.Sum(c=>c.Warmups),cohorts.ToArray());
    }

    // Counts alone are insufficient: an edited summary could substitute a different
    // valid task while leaving the total unchanged. Preserve the actual task domains.
    static bool Allowed(string id,OverviewCell cell)=>id switch {
        "baseline"=>!cell.Task.StartsWith("sed-",StringComparison.Ordinal)&&cell.InputBytes>=1048616,
        "selectstring"=>cell.Task is "literal" or "regex" or "count"&&cell.InputBytes>=1048616,
        "grep"=>cell.Task is "literal" or "regex" or "count" or "nonblank"&&cell.InputBytes>=1048616,
        "small"=>cell.Task is "literal" or "regex" or "count"&&cell.InputBytes<100000,
        "rawk" or "awk"=>cell.Task is "field" or "words" or "lines",
        "buffer"=>cell.Task is "field" or "words" or "lines"&&cell.InputBytes==33554436,
        "tuning"=>cell.Task is "literal" or "regex" or "count" or "nonblank",
        "sed"=>cell.Task.StartsWith("sed-",StringComparison.Ordinal)&&(cell.Engine!="sd"||cell.Task is "sed-literal" or "sed-regex"),
        _=>false
    };
}
