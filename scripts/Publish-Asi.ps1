param([string]$Version = '1.0.4')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid version.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot "dist\CrimsonHue-$Version-asi-win-x64"
$zip = "$output.zip"
if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $zip)) { throw 'Versioned output already exists. Choose a new version; releases are immutable.' }
& (Join-Path $PSScriptRoot 'Build-Asi.ps1') -Version $Version
$package = Join-Path $output 'CrimsonHue'
New-Item -ItemType Directory -Path $package | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts\asi\CrimsonHue.asi') -Destination $package
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $package
if ($Version -match '-openrgb\.') {
    $readme = Join-Path $package 'README.md'
    $previewGuide = @"
# CrimsonHue $Version — private OpenRGB test

This test build adds continuous OpenRGB output alongside Hue. Use your existing
CDT 2.2.7 and .NET 8 Desktop Runtime x64. The native menu protocol is unchanged.

1. Close the game and install this ZIP through DMM yourself, replacing the old
   CrimsonHue package. Keep the matching CDT package installed.
2. Start OpenRGB's SDK server on localhost port 6742. Exit Synapse/Chroma and
   other software controlling the same RGB devices for this test.
3. Start the game, open CrimsonHue with Ctrl+Shift+H and click Start lighting sync.
   Your existing Hue bridge/area setup is used. Loading the ASI alone sends nothing.
4. Hue and OpenRGB now run together. All eligible OpenRGB RGB zones use the common
   virtual position in front of the monitor, with a maximum RGB output of 25%.
   Existing lighting/smoothing/own-lantern controls also affect OpenRGB. A scene
   with no mapped front contribution and unavailable Ambient can correctly be dark.
5. The shared sync status includes OpenRGB's device count or connection error.
   Stop stops both outputs and restores prior states while still owned/reachable.
   Hide/close the panel keeps sync running. Stop before leaving the game.

Gamepads use only their Lightbar, keyboard matrix holes stay excluded and empty
RGB headers are never resized. Device selection and individual positions are not
in this first test build. OpenRGB failure does not stop an otherwise healthy Hue
stream; after an SDK failure use Stop, fix the server/conflicting app, then Start.

This private ZIP has build, automated and synthetic controller validation. Its
installed in-game Start/Stop and parallel lighting appearance still need testing.
The earlier explicit color test was visually accepted on keyboard, mouse,
DualSense and RTX 3080; that is separate from acceptance of this ZIP.

---

"@
    [IO.File]::WriteAllText($readme, $previewGuide + [IO.File]::ReadAllText($readme), [Text.UTF8Encoding]::new($false))
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts\native-asi\NATIVE-THIRD-PARTY-NOTICES.txt') -Destination $package
$guide = Join-Path $package 'docs'
New-Item -ItemType Directory -Path $guide | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USER-GUIDE.md'), (Join-Path $projectRoot 'docs\AMBIENT-INTEGRATION.md'), (Join-Path $projectRoot 'docs\ASI.md') -Destination $guide
& (Join-Path $projectRoot 'tests\Test-AsiPackage.ps1') -Folder $output
# Exact release candidate, hidden synthetic controller. GPU checks belong to CDT.
$smoke = Join-Path $projectRoot "artifacts\asi-package-$Version\controller.png"
New-Item -ItemType Directory -Path (Split-Path -Parent $smoke) -Force | Out-Null
$controllerArgs = @((Join-Path $package 'CrimsonHue.asi'), $smoke)
if ($Version -match '-openrgb\.') { $controllerArgs += '--openrgb-preview' }
& (Join-Path $projectRoot 'artifacts\native-asi\Release\CrimsonHue.ControllerSmoke.exe') @controllerArgs
if ($LASTEXITCODE -ne 0) { throw 'Packaged ASI controller smoke failed.' }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $output -Recurse -File) {
        $name = $file.FullName.Substring($output.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
& (Join-Path $projectRoot 'tests\Test-AsiPackage.ps1') -Folder $output -Zip $zip
Get-FileHash -Algorithm SHA256 -LiteralPath $zip
