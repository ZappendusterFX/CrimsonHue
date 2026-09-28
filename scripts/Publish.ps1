param([string]$Version = '0.2.2')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid version.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot "dist\CrimsonHue-$Version-win-x64"
$zip = "$output.zip"
if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $zip)) { throw 'Versioned output already exists. Choose a new version; releases are immutable.' }
$stage = Join-Path $projectRoot ('artifacts\publish-' + [guid]::NewGuid().ToString('N'))
& dotnet publish (Join-Path $projectRoot 'src\CrimsonHue.App\CrimsonHue.App.csproj') -c Release -r win-x64 --self-contained true -o $stage "-p:Version=$Version" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:RestorePackagesPath=$projectRoot\artifacts\packages"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
$runtimeRoots = Get-ChildItem -Directory -LiteralPath (Join-Path $projectRoot 'artifacts\packages') | Where-Object { $_.Name -in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64') }
foreach ($runtimeRoot in $runtimeRoots) {
    $runtimeVersion = Get-ChildItem -Directory -LiteralPath $runtimeRoot.FullName | Sort-Object Name -Descending | Select-Object -First 1
    foreach ($name in @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')) {
        $licenseFile = Join-Path $runtimeVersion.FullName $name
        if (Test-Path -LiteralPath $licenseFile) { Copy-Item -LiteralPath $licenseFile -Destination (Join-Path $stage ($runtimeRoot.Name + '-' + $name)) }
    }
}
New-Item -ItemType Directory -Path $output | Out-Null
Get-ChildItem -File -LiteralPath $stage | Copy-Item -Destination $output
Compress-Archive -LiteralPath $output -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash -Algorithm SHA256 -LiteralPath $zip
Get-ChildItem -File -LiteralPath $output | Select-Object Name,Length
