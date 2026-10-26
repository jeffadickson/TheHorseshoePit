# Pester 3-compatible bootstrap tests; no package installation or network writes.
Describe 'TextToolBench bootstrap invariants' {
    . "$PSScriptRoot/manage.ps1" -LibraryOnly
    It 'accepts a correct digest and rejects a deliberately mutated digest' {
        $file=Join-Path $TestDrive 'corpus with spaces.txt'
        [IO.File]::WriteAllText($file,'alpha',[Text.UTF8Encoding]::new($false))
        $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        { Assert-Digest $file $hash } | Should Not Throw
        { Assert-Digest $file ('0'*64) } | Should Throw
    }
    It 'rejects a missing source' {
        { Assert-Digest (Join-Path $TestDrive 'missing.txt') ('0'*64) } | Should Throw
    }
    It 'checks reproducible random byte fixtures without modifying them' {
        $random=[Random]::new(20261103)
        foreach($length in @(0,1,31,1024,65537)){
            $bytes=New-Object byte[] $length;$random.NextBytes($bytes)
            $file=Join-Path $TestDrive "random-$length.bin";[IO.File]::WriteAllBytes($file,$bytes)
            $sha=[Security.Cryptography.SHA256]::Create()
            try{$expected=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','')}finally{$sha.Dispose()}
            {Assert-Digest $file $expected} | Should Not Throw
        }
    }
    It 'rejects a child command reporting failure' {
        {Invoke-Checked powershell.exe @('-NoProfile','-Command','exit 7')} | Should Throw
    }
    It 'uses the pinned source and distinct source revisions' {
        $script:Lock.source.sha256.Length | Should Be 64
        @($script:Lock.repositories | Select-Object -ExpandProperty commit -Unique).Count | Should Be 3
    }
}
