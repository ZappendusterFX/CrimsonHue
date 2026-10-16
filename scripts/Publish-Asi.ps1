param([string]$Version = '0.4.0-asi.12')
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
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts\native-asi\NATIVE-THIRD-PARTY-NOTICES.txt') -Destination $package
$guide = Join-Path $package 'docs'
New-Item -ItemType Directory -Path $guide | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USER-GUIDE.md'), (Join-Path $projectRoot 'docs\AMBIENT-INTEGRATION.md'), (Join-Path $projectRoot 'docs\ASI.md') -Destination $guide
& (Join-Path $projectRoot 'tests\Test-AsiPackage.ps1') -Folder $output
# Exact release candidate, hidden synthetic controller. GPU checks belong to CDT.
$smoke = Join-Path $projectRoot "artifacts\asi-package-$Version\controller.png"
New-Item -ItemType Directory -Path (Split-Path -Parent $smoke) -Force | Out-Null
& (Join-Path $projectRoot 'artifacts\native-asi\Release\CrimsonHue.ControllerSmoke.exe') (Join-Path $package 'CrimsonHue.asi') $smoke
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
