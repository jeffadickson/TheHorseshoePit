param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][ValidateSet('literal','regex','count')][string]$Task)
$ErrorActionPreference = 'Stop'
# Select-String reads the file directly, avoiding Get-Content's per-line pipeline overhead.
$writer = [IO.StreamWriter]::new([Console]::OpenStandardOutput(), [Text.UTF8Encoding]::new($false), 65536)
$writer.NewLine = "`n"
try {
    switch ($Task) {
        'literal' { Select-String -LiteralPath $Path -Pattern 'Prince' -SimpleMatch -CaseSensitive -Encoding utf8 | ForEach-Object { $writer.WriteLine($_.Line) } }
        'regex' { Select-String -LiteralPath $Path -Pattern 'war|peace' -CaseSensitive -Encoding utf8 | ForEach-Object { $writer.WriteLine($_.Line) } }
        'count' { $n = (Select-String -LiteralPath $Path -Pattern 'Prince' -SimpleMatch -CaseSensitive -Encoding utf8 | Measure-Object).Count; $writer.WriteLine($n) }
    }
} finally { $writer.Dispose() }
