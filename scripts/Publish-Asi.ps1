param([string]$Version = '0.4.0-asi.2')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid version.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot "dist\CrimsonHue-$Version-asi-win-x64"
$zip = "$output.zip"
if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $zip)) { throw 'Versioned output already exists. Choose a new version; releases are immutable.' }
& (Join-Path $PSScriptRoot 'Build-Asi.ps1') -Version $Version
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts\asi\CrimsonHue.asi') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts\asi\CrimsonHue') -Destination $output -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $output
$guide = Join-Path $output 'docs'
New-Item -ItemType Directory -Path $guide | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USER-GUIDE.md'), (Join-Path $projectRoot 'docs\AMBIENT-INTEGRATION.md'), (Join-Path $projectRoot 'docs\ASI.md') -Destination $guide
if (Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Extension -in @('.exe', '.secrets', '.pdb', '.obj') -or $_.Name -in @('settings.json', 'CrimsonDesertTelemetry.asi') }) {
    throw 'Unexpected executable, local settings, secrets or development file in package.'
}
# Run the exact candidate through an ordinary native process; no game or bridge.
$smoke = Join-Path $projectRoot ('artifacts\asi-package-smoke-' + $Version + '.png')
& (Join-Path $projectRoot 'artifacts\native-asi\Release\CrimsonHue.AsiSmoke.exe') (Join-Path $output 'CrimsonHue.asi') $smoke
if ($LASTEXITCODE -ne 0) { throw 'Packaged ASI smoke failed.' }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$files = @(Get-ChildItem -LiteralPath $output -Recurse -File)
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $name = $file.FullName.Substring($output.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { $_.Name })
    if ($entries.Count -ne $files.Count) { throw 'Archive file count differs from the verified candidate.' }
    foreach ($file in $files) {
        $name = $file.FullName.Substring($output.Length + 1).Replace('\', '/')
        $entry = $archive.GetEntry($name)
        if (-not $entry -or $entry.Length -ne $file.Length) { throw "Archive entry missing or wrong size: $name" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
            if ($hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
                throw "Archive entry differs from verified candidate: $name"
            }
        }
        finally { $sha.Dispose(); $stream.Dispose() }
    }
}
finally { $archive.Dispose() }
Write-Output "Verified $($files.Count) archive files against the native-smoke-tested candidate; no EXEs, credentials or local settings."
Get-FileHash -Algorithm SHA256 -LiteralPath $zip
