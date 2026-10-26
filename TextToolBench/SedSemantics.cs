using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("SedBench.Tests")]

/// <summary>Independent byte-preserving reference for the suite's ASCII sed subset.</summary>
internal static class SedSemantics
{
    /// <summary>Transform bytes without decoding UTF-8, normalizing CRLF, or inventing a final newline.</summary>
    internal static byte[] Transform(byte[] input,string task)
    {
        // Latin-1 is a reversible byte mapping, not a claim about the corpus character encoding.
        string text=Encoding.Latin1.GetString(input);
        string output=task switch {
            "sed-literal"=>text.Replace("Prince","PRINCE",StringComparison.Ordinal),
            "sed-regex"=>Regex.Replace(text,"[0-9]+","#",RegexOptions.CultureInvariant),
            "sed-transliterate"=>string.Concat(text.Select(c=>c switch{'a'=>'A','e'=>'E','i'=>'I','o'=>'O','u'=>'U',_=>c})),
            "sed-nonblank" or "sed-select" or "sed-head"=>SelectRecords(text,task),
            _=>throw new ArgumentException("Unknown sed workload",nameof(task))
        };
        return Encoding.Latin1.GetBytes(output);
    }

    /// <summary>Keep the original record terminator; an empty file contains zero records.</summary>
    static string SelectRecords(string text,string task)
    {
        var output=new StringBuilder();int start=0,number=0;
        while(start<text.Length) {
            int end=text.IndexOf('\n',start);bool terminated=end>=0;if(!terminated)end=text.Length;
            string line=text[start..end];number++;
            bool keep=task switch {
                "sed-nonblank"=>line.Any(c=>c!=' '&&c!='\t'),
                "sed-select"=>line.Contains("Prince",StringComparison.Ordinal),
                "sed-head"=>number<=100,
                _=>throw new ArgumentException("Unknown selection workload",nameof(task))
            };
            if(keep){output.Append(line);if(terminated)output.Append('\n');}
            start=end+1;
        }
        return output.ToString();
    }
}
