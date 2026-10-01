param([string]$Version = '0.4.0-asi.2')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid version.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$nativeBuild = Join-Path $projectRoot 'artifacts\native-asi'
$payload = Join-Path $projectRoot 'artifacts\asi\CrimsonHue'
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
$cmake = if ($cmakeCommand) { $cmakeCommand.Source } else {
    $locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $locator)) { throw 'Install Visual Studio with Desktop development with C++ and CMake tools.' }
    $installation = & $locator -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    Join-Path $installation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
}
if (-not (Test-Path -LiteralPath $cmake)) { throw 'CMake was not found.' }
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$hostPack = Get-ChildItem -Directory -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Host.win-x64') |
    Where-Object { $_.Name -match '^8\.0\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $hostPack) { throw 'Install the .NET 8 SDK (x64 host headers are required).' }
$headers = Join-Path $hostPack.FullName 'runtimes\win-x64\native'
& $cmake -S (Join-Path $projectRoot 'native\CrimsonHue.Asi') -B $nativeBuild -G 'Visual Studio 17 2022' -A x64 "-DDOTNET_HOST_INCLUDE=$headers"
if ($LASTEXITCODE -ne 0) { throw 'Native configuration failed.' }
& $cmake --build $nativeBuild --config Release
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
# A library with an app-local runtime, no apphost executable. The ASI calls the
# managed entry points through hostfxr; no dotnet.exe is required by users.
$stage = Join-Path $projectRoot ('artifacts\asi-payload-' + [guid]::NewGuid().ToString('N'))
& dotnet publish (Join-Path $projectRoot 'src\CrimsonHue.App\CrimsonHue.App.csproj') -c Release -r win-x64 --self-contained true -o $stage "-p:Version=$Version" -p:UseAppHost=false -p:DebugType=None "-p:RestorePackagesPath=$projectRoot\artifacts\packages"
if ($LASTEXITCODE -ne 0) { throw 'Managed publish failed.' }
# createdump is an optional .NET crash-dump helper. It is never needed by the
# hosted UI and must not reintroduce an EXE into the mod package.
Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.exe' | ForEach-Object {
    if ($_.Name -ne 'createdump.exe') { throw "Unexpected executable in ASI payload: $($_.Name)" }
    Remove-Item -LiteralPath $_.FullName
}
$packages = Join-Path $projectRoot 'artifacts\packages'
foreach ($name in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
    $root = Join-Path $packages $name
    $runtime = Get-ChildItem -Directory -LiteralPath $root | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    foreach ($notice in @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')) {
        $file = Join-Path $runtime.FullName $notice
        if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $stage "$name-$notice") }
    }
}
# Preserve every previous build. Use a fresh runtime directory so stale files
# cannot enter the next package; the old payload stays under artifacts.
if (Test-Path -LiteralPath $payload) {
    $previous = Join-Path $projectRoot ('artifacts\asi-payload-previous-' + [guid]::NewGuid().ToString('N'))
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    foreach ($target in @($payload, $previous)) {
        if (-not [IO.Path]::GetFullPath($target).StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Payload move escaped the workspace artifacts directory.'
        }
    }
    Move-Item -LiteralPath $payload -Destination $previous
}
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Get-ChildItem -LiteralPath $stage | Copy-Item -Destination $payload -Recurse
Copy-Item -LiteralPath (Join-Path $nativeBuild 'Release\CrimsonHue.asi') -Destination (Join-Path $projectRoot 'artifacts\asi\CrimsonHue.asi')
Write-Output "Built $projectRoot\artifacts\asi\CrimsonHue.asi"
