#requires -Version 7.4
# Prepared outside the game. Run only on the owner's go; no settings or output commands.
[CmdletBinding()]
param(
    [ValidateRange(1,30)][int]$Seconds = 30,
    [string]$TelemetryRepository = 'C:\DEV\CrimsonDesertTelemetry',
    [string]$OutputDirectory,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$hueRepository = Split-Path -Parent $PSScriptRoot
$visibilityExe = Join-Path $TelemetryRepository 'artifacts/tools/visibility-recorder/VisibilityRecorder.exe'
$hueExe = Join-Path $hueRepository 'artifacts/tools/hue-state-recorder/HueStateRecorder.exe'
foreach ($executable in @($visibilityExe, $hueExe)) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Build the diagnostic tool first: $executable" }
}
Get-Command Start-ThreadJob -ErrorAction Stop | Out-Null
if ($ValidateOnly) {
    [pscustomobject]@{ Ready=$true; Seconds=$Seconds; VisibilityRecorder=$visibilityExe; HueRecorder=$hueExe }
    return
}
$games = @(Get-Process -Name CrimsonDesert -ErrorAction SilentlyContinue)
if ($games.Count -ne 1) { throw 'Exactly one running Crimson Desert process is required.' }
$gameId = $games[0].Id
$hostIds = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$gameId AND Name='dotnet.exe'" | ForEach-Object ProcessId)
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $hueRepository ('artifacts/skill-window-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Evidence directory already exists; choose a fresh path.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$gameDirectory = Split-Path -Parent $games[0].Path
function Save-Logs([string]$Phase) {
    $destination = Join-Path $OutputDirectory $Phase
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($name in @('CrimsonDesertTelemetry.native.log','CrimsonDesertTelemetry.bootstrap.log','CrimsonDesertTelemetry.host.log','CrimsonDesertTelemetry.overlay.log')) {
        $source = Join-Path $gameDirectory $name
        if (Test-Path -LiteralPath $source) {
            if ((Get-Item -LiteralPath $source).Length -le 2MB) { Copy-Item -LiteralPath $source -Destination (Join-Path $destination $name) }
            else { Get-Content -LiteralPath $source -Tail 500 | Set-Content -LiteralPath (Join-Path $destination ($name + '.tail.txt')) }
        }
    }
}
Save-Logs 'before'
$startedAt = [DateTimeOffset]::UtcNow
$stateJob = Start-ThreadJob -ScriptBlock {
    param($Executable,$GameId,$Directory,$Duration)
    & $Executable $GameId (Join-Path $Directory 'hue.jsonl') $Duration *> (Join-Path $Directory 'hue-recorder.log')
    if ($LASTEXITCODE -ne 0) { throw "Hue reader exited with code $LASTEXITCODE" }
} -ArgumentList $hueExe,$gameId,$OutputDirectory,$Seconds
$performanceJob = Start-ThreadJob -ScriptBlock {
    param($GameId,$HostIds,$Directory,$Duration)
    $ErrorActionPreference = 'Stop'
    $logical = [Environment]::ProcessorCount
    $previous = @{}
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $file = [IO.File]::Open((Join-Path $Directory 'performance.jsonl'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $writer = [IO.StreamWriter]::new($file)
    $writer.AutoFlush = $true
    try {
        while ($clock.Elapsed.TotalSeconds -lt $Duration) {
            $began = $clock.Elapsed.TotalMilliseconds
            $at = [DateTimeOffset]::UtcNow
            $processes = foreach ($processId in @($GameId) + @($HostIds)) {
                try {
                    $process = Get-Process -Id $processId -ErrorAction Stop
                    $now = $clock.Elapsed.TotalSeconds
                    $cpu = $process.TotalProcessorTime.TotalSeconds
                    $percent = $null
                    if ($previous.ContainsKey($processId) -and $now -gt $previous[$processId].at) {
                        $percent = 100 * ($cpu - $previous[$processId].cpu) / ($now - $previous[$processId].at) / $logical
                    }
                    $previous[$processId] = @{ at=$now; cpu=$cpu }
                    [ordered]@{ id=$processId; name=$process.ProcessName; machineCpuPercent=$percent; threads=$process.Threads.Count; workingSet=$process.WorkingSet64 }
                    $process.Dispose()
                } catch { [ordered]@{ id=$processId; errorType=$_.Exception.GetType().Name } }
            }
            $gpu = @()
            $gpuError = $null
            try {
                $gpu = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -OperationTimeoutSec 2 |
                    Where-Object { $_.Name -like "pid_${GameId}_*" -and $_.Name -like '*engtype_3D*' } |
                    Select-Object Name,UtilizationPercentage)
            } catch { $gpuError = $_.Exception.GetType().Name }
            $writer.WriteLine(([ordered]@{ at=$at; elapsedMs=$began; sampleMs=($clock.Elapsed.TotalMilliseconds-$began); logicalProcessors=$logical; processes=@($processes); gpu=$gpu; gpuError=$gpuError } | ConvertTo-Json -Depth 6 -Compress))
            $remaining = [Math]::Min($Duration*1000-$clock.Elapsed.TotalMilliseconds,1000-($clock.Elapsed.TotalMilliseconds-$began))
            if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int]$remaining) }
        }
    } finally { $writer.Dispose(); $file.Dispose() }
} -ArgumentList $gameId,$hostIds,$OutputDirectory,$Seconds
$recorderExit = -1
try {
    & $visibilityExe $Seconds --lighting --output (Join-Path $OutputDirectory 'cdt')
    $recorderExit = $LASTEXITCODE
} finally {
    $jobs = @($stateJob,$performanceJob)
    $jobs | Wait-Job -Timeout 5 | Out-Null
    $jobStates = @($jobs | Select-Object Name,State)
    $jobErrors = @($jobs | ForEach-Object { $_.ChildJobs | ForEach-Object { $_.Error | ForEach-Object { $_.Exception.GetType().Name } } })
    $jobs | Where-Object State -eq Running | Stop-Job
    $jobs | Receive-Job -ErrorAction Continue | Out-Null
    $jobs | Remove-Job -Force
    Save-Logs 'after'
    [ordered]@{ startedAt=$startedAt; endedAt=[DateTimeOffset]::UtcNow; seconds=$Seconds; gameProcessId=$gameId; hostProcessIds=$hostIds;
        recorderExitCode=$recorderExit; jobs=$jobStates; jobErrorTypes=$jobErrors;
        evidence='Read-only neutral API streams and non-suspending immutable Hue UI reads; CPU/GPU counters are sampled, not a Present/FPS trace. No output commands or deployment changes.'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
}
Write-Output "SAVED $OutputDirectory"
if ($recorderExit -ne 0 -or @($jobStates | Where-Object State -ne Completed).Count -gt 0) { throw 'Recording incomplete; preserved evidence and manifest explain partial results.' }
