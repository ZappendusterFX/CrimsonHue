param([string]$Version = '0.4.0-asi.15')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid version.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$nativeBuild = Join-Path $projectRoot 'artifacts\native-asi'
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
$stage = Join-Path $projectRoot ('artifacts\asi-component-' + [guid]::NewGuid().ToString('N'))
$component = Join-Path $stage 'component'
# Framework-dependent: only our assemblies and NuGet dependencies are published.
# End users install .NET 8 Desktop Runtime x64; it is never embedded in the ASI.
& dotnet publish (Join-Path $projectRoot 'src\CrimsonHue.App\CrimsonHue.App.csproj') -c Release -r win-x64 --self-contained false -o $component "-p:Version=$Version" -p:UseAppHost=false -p:DebugType=None "-p:RestorePackagesPath=$projectRoot\artifacts\packages"
if ($LASTEXITCODE -ne 0) { throw 'Managed publish failed.' }
$files = @(Get-ChildItem -LiteralPath $component -Recurse -File | Sort-Object FullName)
$expectedComponent = @('BouncyCastle.Cryptography.dll', 'CrimsonHue.Core.dll', 'CrimsonHue.dll', 'CrimsonHue.deps.json', 'CrimsonHue.runtimeconfig.json')
if (@(Compare-Object ($expectedComponent | Sort-Object) ($files.Name | Sort-Object)).Count -ne 0) { throw 'Embedded component differs from the application-only manifest; no runtime files may be bundled.' }
if ($files.Count -lt 4 -or $files.Count -gt 32) { throw 'Unexpected managed component size.' }
if ($files | Where-Object { $_.Extension -in @('.exe', '.secrets', '.pdb', '.obj') -or $_.Name -in @('hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'settings.json') }) {
    throw 'Unexpected executable, bundled runtime or local settings in component.'
}
$manifest = [Collections.Generic.List[string]]::new()
$directives = [Collections.Generic.List[string]]::new()
$directives.Add('.Set CabinetNameTemplate=component.cab')
$directives.Add('.Set DiskDirectoryTemplate="' + $stage + '"')
$directives.Add('.Set MaxDiskSize=0')
$directives.Add('.Set MaxCabinetSize=0')
$directives.Add('.Set MaxFolderSize=0')
$directives.Add('.Set CompressionType=MSZIP')
$directives.Add('.Set Cabinet=on')
$directives.Add('.Set Compress=on')
$directives.Add('.Set InfFileName="' + (Join-Path $stage 'component.inf') + '"')
$directives.Add('.Set RptFileName="' + (Join-Path $stage 'component.rpt') + '"')
foreach ($file in $files) {
    $name = $file.FullName.Substring($component.Length + 1)
    if ($name -match '[^\x20-\x7e]' -or $name.Contains('"')) { throw 'Component filenames must be ASCII.' }
    $relativeDirectory = Split-Path -Parent $name
    if (-not $relativeDirectory) { $relativeDirectory = '.' }
    $directives.Add('.Set DestinationDir="' + $relativeDirectory + '"')
    $directives.Add('"' + $file.FullName + '"')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest.Add("$hash $($file.Length) $name")
}
$manifestPath = Join-Path $stage 'manifest.txt'
[IO.File]::WriteAllLines($manifestPath, $manifest, [Text.Encoding]::ASCII)
$ddf = Join-Path $stage 'component.ddf'
[IO.File]::WriteAllLines($ddf, $directives, [Text.Encoding]::ASCII)
& makecab /V0 /F $ddf > (Join-Path $stage 'makecab.log')
if ($LASTEXITCODE -ne 0) { throw 'Component compression failed.' }
$cabinet = Join-Path $stage 'component.cab'
if (-not (Test-Path -LiteralPath $cabinet)) { throw 'Component cabinet missing.' }
& $cmake -S (Join-Path $projectRoot 'native\CrimsonHue.Asi') -B $nativeBuild -G 'Visual Studio 17 2022' -A x64 "-DDOTNET_HOST_INCLUDE=$headers" "-DPAYLOAD_CAB=$cabinet" "-DPAYLOAD_MANIFEST=$manifestPath"
if ($LASTEXITCODE -ne 0) { throw 'Native configuration failed.' }
& $cmake --build $nativeBuild --config Release
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
$output = Join-Path $projectRoot 'artifacts\asi'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $nativeBuild 'Release\CrimsonHue.asi') -Destination $output
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $output 'component-manifest.txt')
Write-Output "Built $output\CrimsonHue.asi with $($files.Count) embedded component files; .NET Desktop Runtime is required separately."
