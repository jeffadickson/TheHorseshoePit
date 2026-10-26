# Controlled stdout-only patches. Original pinned repositories remain unchanged.
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    foreach($name in @('stefan','quinn')){
        $destination="tools/variants/$name-buffered"
        if(-not(Test-Path "$destination/Cargo.toml")){
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            $archive=if($name -eq 'stefan'){'upstream/stefan-source.tar'}else{'upstream/quinn-source.tar'}
            & "$env:SystemRoot/System32/tar.exe" -xf $archive -C $destination
            if($LASTEXITCODE -ne 0){throw 'Source extraction failed'}
        }
    }
    $stefan='tools/variants/stefan-buffered/crates/rawk-cli/src/main.rs'
    $text=[IO.File]::ReadAllText((Join-Path $root $stefan))
    if(-not $text.Contains('BufWriter::with_capacity(64 * 1024')){
        $text=$text.Replace('use std::{io, path};',"use std::{io, path};`nuse std::io::Write;")
        $old="    for line in output_lines {`n        println!(`"{}`", line);`n    }"
        $new="    // Buffer redirected output to avoid a Windows write/console check per record.`n    let stdout = io::stdout();`n    let mut output = io::BufWriter::with_capacity(64 * 1024, stdout.lock());`n    for line in output_lines {`n        writeln!(output, `"{}`", line)?;`n    }`n    output.flush()?;"
        # Only the file-processing loop is changed; preserve the interactive path.
        $position=$text.IndexOf($old,[StringComparison]::Ordinal)
        if($position -lt 0){throw 'Pinned rawk source did not match stdout patch'}
        $text=$text.Substring(0,$position)+$new+$text.Substring($position+$old.Length)
        [IO.File]::WriteAllText((Join-Path $root $stefan),$text,[Text.UTF8Encoding]::new($false))
    }
    $quinn='tools/variants/quinn-buffered/src/main.rs'
    $text=[IO.File]::ReadAllText((Join-Path $root $quinn))
    if(-not $text.Contains('BufWriter::with_capacity(64 * 1024')){
        $text=$text.Replace('let mut output = stdout.lock();','let mut output = io::BufWriter::with_capacity(64 * 1024, stdout.lock());')
        $needle='let exit_code = interpreter.run(inputs, &mut output)?;'
        if(-not $text.Contains($needle)){throw 'Pinned awk-rs source did not match stdout patch'}
        $text=$text.Replace($needle,"$needle`n    std::io::Write::flush(&mut output)?;")
        [IO.File]::WriteAllText((Join-Path $root $quinn),$text,[Text.UTF8Encoding]::new($false))
    }
}finally{Pop-Location}
