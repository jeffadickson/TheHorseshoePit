using System.Text;

/// <summary>Deterministic line fixtures and independent edit-distance and patch statistics references.</summary>
public static class DiffModel
{
    /// <summary>Create a pair with LF delimiters. The size is a lower bound, not an invented exact byte count.</summary>
    public static (byte[] Before,byte[] After) Pair(string text,int size,string workload)
    {
        if(size<1||string.IsNullOrEmpty(text))throw new ArgumentException("Positive size and nonempty source required.");
        string[] source=text.Replace("\r","").Split('\n').Where(x=>x.Length>0).ToArray();
        if(source.Length==0)throw new ArgumentException("Source has no nonempty lines.");
        var lines=new List<string>();int bytes=0;
        for(int i=0;bytes<size;i++) {
            // Unique prefixes expose alignment; the repeated workload deliberately removes them.
            string line=workload=="repeated"?$"repeat {i%7}":$"{i:D8} {source[i%source.Length]}";
            lines.Add(line);bytes+=Encoding.UTF8.GetByteCount(line)+1;
        }
        var target=new List<string>(lines);
        switch(workload) {
            case "identical":break;
            case "scattered":for(int i=0;i<target.Count;i+=Math.Max(1,target.Count/20))target[i]+=" EDIT";break;
            case "insert-delete":
                int block=Math.Max(1,target.Count/10);target.RemoveRange(target.Count/3,block);
                target.InsertRange(target.Count/2,Enumerable.Range(0,block).Select(i=>$"inserted {i}"));break;
            case "moved":
                int count=Math.Max(1,target.Count/4);var moved=target.Take(count).ToArray();target.RemoveRange(0,count);target.AddRange(moved);break;
            case "repeated":target.Insert(target.Count/2,"unique insertion");target.RemoveAt(target.Count/3);break;
            case "different":for(int i=0;i<target.Count;i++)target[i]=$"new content {i:D8}";break;
            case "long-line":return (Encoding.ASCII.GetBytes(new string('x',size)+"\n"),Encoding.ASCII.GetBytes(new string('x',size/2)+"y"+new string('x',size-size/2-1)+"\n"));
            default:throw new ArgumentException("Unknown workload",nameof(workload));
        }
        return (Encoding.UTF8.GetBytes(string.Join('\n',lines)+"\n"),Encoding.UTF8.GetBytes(string.Join('\n',target)+"\n"));
    }

    /// <summary>Count unified patch additions/deletions, excluding headers and no-newline markers.</summary>
    public static (int Added,int Deleted,int Hunks) Statistics(byte[] patch)
    {
        int added=0,deleted=0,hunks=0;bool body=false;
        foreach(string line in Encoding.UTF8.GetString(patch).Split('\n')) {
            if(line.StartsWith("@@ ",StringComparison.Ordinal)){hunks++;body=true;continue;}
            if(!body)continue;
            if(line.StartsWith('+'))added++;
            else if(line.StartsWith('-'))deleted++;
        }
        return (added,deleted,hunks);
    }

    /// <summary>Exact insertion/deletion distance for bounded fixtures, using independent dynamic programming.</summary>
    public static int MinimumEdits(string[] before,string[] after)
    {
        var row=Enumerable.Range(0,after.Length+1).ToArray();
        for(int i=1;i<=before.Length;i++) {
            int diagonal=row[0];row[0]=i;
            for(int j=1;j<=after.Length;j++) {
                int old=row[j];row[j]=before[i-1]==after[j-1]?diagonal:Math.Min(row[j]+1,row[j-1]+1);diagonal=old;
            }
        }
        return row[^1];
    }

    /// <summary>Conservative LCS-table estimate after equal-end trimming; exact for benchmark LF-terminated pairs.</summary>
    public static long QuadraticTableBytes(byte[] before,byte[] after)
    {
        string[] a=Encoding.Latin1.GetString(before).Split('\n'),b=Encoding.Latin1.GetString(after).Split('\n');
        int leading=0,trailing=0;
        while(leading<Math.Min(a.Length,b.Length)&&a[leading]==b[leading])leading++;
        while(trailing<Math.Min(a.Length,b.Length)-leading&&a[^(trailing+1)]==b[^(trailing+1)])trailing++;
        return checked(4L*(a.Length-leading-trailing+1)*(b.Length-leading-trailing+1));
    }
}
