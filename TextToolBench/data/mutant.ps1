param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Task, [ValidateSet('pipeline','reader')][string]$Style = 'pipeline')
$ErrorActionPreference = 'Stop'
# Explicit UTF-8/LF output makes text-tool results comparable across Windows runtimes.
$writer = [IO.StreamWriter]::new([Console]::OpenStandardOutput(), [Text.UTF8Encoding]::new($false), 65536)
$writer.NewLine = "`n"
[long]$total = 0
try {
    if ($Style -eq 'pipeline') {
        switch ($Task) {
            'literal' { Get-Content -LiteralPath $Path -Encoding utf8 | Where-Object { $_.Contains('Prince') } | ForEach-Object { $writer.WriteLine($_) } }
            'regex' { Get-Content -LiteralPath $Path -Encoding utf8 | Where-Object { $_ -cmatch 'war|peace' } | ForEach-Object { $writer.WriteLine($_) } }
            'nonblank' { Get-Content -LiteralPath $Path -Encoding utf8 | Where-Object { $_ -cmatch '[^ \t]' } | ForEach-Object { $writer.WriteLine($_) } }
            'replace' { Get-Content -LiteralPath $Path -Encoding utf8 | ForEach-Object { $writer.WriteLine($_.Replace('Prince','Duke')) } }
            'field' { Get-Content -LiteralPath $Path -Encoding utf8 | ForEach-Object { if ($_ -cmatch '[^ \t]+') { $writer.WriteLine($Matches[0]) } } }
            'words' { Get-Content -LiteralPath $Path -Encoding utf8 | ForEach-Object { $total += [regex]::Matches($_,'[^ \t]+').Count }; $writer.WriteLine($total) }
            'lines' { Get-Content -LiteralPath $Path -Encoding utf8 | ForEach-Object { $total++ }; $writer.WriteLine($total) }
            'count' { Get-Content -LiteralPath $Path -Encoding utf8 | Where-Object { $_.Contains('Prince') } | ForEach-Object { $total++ }; $writer.WriteLine($total) }
            default { throw "Unknown task: $Task" }
        }
    } else {
        # StreamReader avoids a PowerShell pipeline object and scriptblock invocation per line.
        $reader = [IO.StreamReader]::new($Path, [Text.Encoding]::UTF8)
        try {
            while ($null -ne ($line = $reader.ReadLine())) {
                switch ($Task) {
                    'literal' { if ($line.Contains('Prince')) { $writer.WriteLine($line) } }
                    'regex' { if ($line -cmatch 'war|peace') { $writer.WriteLine($line) } }
                    'nonblank' { if ($line -cmatch '[^ \t]') { $writer.WriteLine($line) } }
                    'replace' { $writer.WriteLine($line.Replace('Prince','Duke')) }
                    'field' { if ($line -cmatch '[^ \t]+') { $writer.WriteLine($Matches[0]) } }
                    'words' { $total += [regex]::Matches($line,'[^ \t]+').Count }
                    'lines' { $total++ }
                    'count' { if ($line.Contains('Prince')) { $total++ } }
                    default { throw "Unknown task: $Task" }
                }
            }
            if ($Task -in @('words','lines','count')) { $writer.WriteLine($total) }
        } finally { $reader.Dispose() }
    }
} finally { $writer.Dispose() }
