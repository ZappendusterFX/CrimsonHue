param([string]$Version = '1.0.6')
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

This test build adds OpenRGB device selection and a 3D room alongside Hue.
Use the paired CDT 2.2.8-openrgb.2 test ZIP for the HUD light-response controls, plus the existing
.NET 8 Desktop Runtime x64. The native menu protocol is unchanged.

1. Close the game and install this ZIP through DMM yourself, replacing the old
   CrimsonHue package. Install the paired CDT test ZIP as well.
2. Start OpenRGB's SDK server on localhost port 6742. Exit Synapse/Chroma and
   other software controlling the same RGB devices for this test.
3. Start the game, open CrimsonHue with Ctrl+Shift+H and select the OpenRGB tab.
   Read devices, select the ones to include and place them around your seat in
   the 3D room. Drag to move across the room, adjust Height to move up/down,
   and right-drag to rotate the view. Settings save locally, including unavailable
   devices' positions. Connection and output limit includes port and brightness.
   Light coverage and Background light are separate OpenRGB controls in the tab.
   Normal desk height is a valid placement.
4. Click Start lighting sync to run Hue and OpenRGB together. Your existing Hue
   bridge/area setup is used. Loading the ASI alone sends nothing. Devices start
   in front of the monitor; their saved individual positions determine output.
   The OpenRGB output limit defaults to 25% and is adjustable. Its own source
   coverage defaults to 90 degrees and Background light to 25% of the common
   background setting. This keeps desk-device colors from being excluded or
   washed out by the shared narrow Hue footprint and neutral room fill. Shared lighting,
   smoothing and own-lantern controls also affect OpenRGB. A scene
   with no mapped front contribution and unavailable Ambient can correctly be dark.
5. The shared sync status includes OpenRGB's device count or connection error.
   Stop stops both outputs and restores prior states while still owned/reachable.
   Hide/close the panel keeps sync running. Stop before leaving the game.

Gamepads use only their Lightbar, keyboard matrix holes stay excluded and empty
RGB headers are never resized. OpenRGB failure does not stop an otherwise healthy Hue
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
