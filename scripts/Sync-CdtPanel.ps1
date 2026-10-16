param([Parameter(Mandatory)][string]$TelemetryRoot)
$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'native\CrimsonHue.Panel'
$target = Join-Path ([IO.Path]::GetFullPath($TelemetryRoot)) 'native\CrimsonDesertTelemetry.Asi\crimsonhue-panel'
if (-not (Test-Path (Join-Path $TelemetryRoot 'native\CrimsonDesertTelemetry.Asi\CMakeLists.txt'))) { throw 'Not a CDT source repository.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
$manifest = foreach ($file in Get-ChildItem -LiteralPath $source -File | Sort-Object Name) {
    # Git pins this shared module to LF in both repositories; hash checkout-stable bytes.
    $normalized = [IO.File]::ReadAllText($file.FullName).Replace("`r`n", "`n")
    $destination = Join-Path $target $file.Name
    [IO.File]::WriteAllText($destination, $normalized, [Text.UTF8Encoding]::new($false))
    '{0} {1}' -f (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
}
[IO.File]::WriteAllLines((Join-Path $target 'SOURCE-SHA256.txt'), $manifest, [Text.Encoding]::ASCII)
Write-Output "Synchronized the renderer-free CrimsonHue panel into $target"
