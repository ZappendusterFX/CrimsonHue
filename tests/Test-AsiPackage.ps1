param([Parameter(Mandatory)][string]$Folder, [string]$Zip)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Folder).Path
$expected = @('CrimsonHue/CrimsonHue.asi', 'CrimsonHue/README.md', 'CrimsonHue/THIRD-PARTY-NOTICES.md',
    'CrimsonHue/NATIVE-THIRD-PARTY-NOTICES.txt', 'CrimsonHue/docs/USER-GUIDE.md', 'CrimsonHue/docs/AMBIENT-INTEGRATION.md', 'CrimsonHue/docs/ASI.md')
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File)
$names = @($files | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') })
if (@(Compare-Object ($expected | Sort-Object) ($names | Sort-Object)).Count -ne 0) { throw 'Unexpected package payload. Exactly one package root, one ASI and the maintained guides/notices are required.' }
if (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Package contains reparse points.' }
$asi = Join-Path $root 'CrimsonHue\CrimsonHue.asi'
$bytes = [IO.File]::ReadAllBytes($asi)
if ($bytes.Length -lt 100000 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { throw 'ASI is not a native Windows image.' }
$pe = [BitConverter]::ToInt32($bytes, 0x3C)
if ($pe -lt 0 -or $pe + 24 -ge $bytes.Length -or [BitConverter]::ToUInt32($bytes, $pe) -ne 0x4550 -or
    [BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664 -or ([BitConverter]::ToUInt16($bytes, $pe + 22) -band 0x2000) -eq 0) { throw 'ASI must be an x64 DLL image.' }
if ($Zip) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Zip).Path)
    try {
        $entries = @($archive.Entries)
        $entryNames = @($entries | ForEach-Object { $_.FullName })
        if (@(Compare-Object ($expected | Sort-Object) ($entryNames | Sort-Object)).Count -ne 0) { throw 'ZIP payload differs from the exact package contract.' }
        if (@($entryNames | Sort-Object -Unique).Count -ne $entries.Count) { throw 'Duplicate ZIP entries.' }
        foreach ($file in $files) {
            $name = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
            $entry = $archive.GetEntry($name)
            if (-not $entry -or $entry.Length -ne $file.Length) { throw "Archive entry missing or wrong size: $name" }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
                if ($hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "Archive bytes differ: $name" }
            } finally { $sha.Dispose(); $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
}
Write-Output "PASS ASI package: $($expected.Count) exact files, single CrimsonHue/ root, x64 ASI; no loose DLL/JSON/CFG/EXE, settings or credentials."
