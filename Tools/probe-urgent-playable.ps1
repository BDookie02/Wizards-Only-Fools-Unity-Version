param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\urgent-playable-probe',
    [int]$Width = 412,
    [int]$Height = 915
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Urgent playable probe paths must stay on D:.'
}

$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
New-Item -ItemType Directory -Force -Path $playerTempRoot | Out-Null
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Windows player is missing: $executable"
}

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
if (Test-Path -LiteralPath $logPath -PathType Leaf) {
    Remove-Item -LiteralPath $logPath -Force
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-mobile-ui', "--wof-urgent-controller-probe=$resolvedOutputRoot",
    '--wof-auto-exit=90', '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
$windowDeadline = [DateTime]::UtcNow.AddSeconds(25)
do {
    Start-Sleep -Milliseconds 200
    $process.Refresh()
} while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
    throw 'Urgent playable controller probe has no foregroundable Unity window.'
}
$shell = New-Object -ComObject WScript.Shell
$shell.AppActivate($process.Id) | Out-Null
Start-Sleep -Milliseconds 350
$deadline = [DateTime]::UtcNow.AddSeconds(115)
do {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    $log = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Get-Content -LiteralPath $logPath -Raw
    } else { '' }
    $complete = $log -match 'URGENT_PLAYABLE_PROBE_(PASS|FAIL)'
} while (-not $complete -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
if (-not $process.HasExited) {
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
}
if (-not $complete) {
    throw 'Urgent playable runtime probe timed out before a pass/fail result.'
}

$log = Get-Content -LiteralPath $logPath -Raw
if ($log -match 'URGENT_PLAYABLE_PROBE_FAIL') {
    $failure = Select-String -LiteralPath $logPath -Pattern 'URGENT_PLAYABLE_PROBE_FAIL' | Select-Object -Last 1
    throw "Urgent playable runtime probe failed: $($failure.Line)"
}
foreach ($marker in @(
    'MOBILE_CONTROLLER_UI_PASS',
    'CONTROLLER_SPELL_MENU_PASS',
    'CONTROLLER_NAVIGATION_MAP_PASS',
    'CONTROLLER_FAST_TRAVEL_PASS',
    'CONTROLLER_LILY_COIL_FAST_TRAVEL_PASS',
    'NORTH_GATE_TRAVERSAL_PASS',
    'JUMP_THRUSTER_PASS',
    'URGENT_PLAYABLE_PROBE_PASS')) {
    if ($log -notmatch [regex]::Escape($marker)) {
        throw "Urgent playable runtime marker is missing: $marker"
    }
}

$screenshots = @(
    'mobile-controller-hud-hidden.png',
    'controller-spell-menu.png',
    'controller-navigation-map.png',
    'controller-lily-coil-fast-travel.png',
    'north-gate-traversed.png',
    'controller-thruster.png'
) | ForEach-Object {
    $path = Join-Path $resolvedOutputRoot $_
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-Item -LiteralPath $path).Length -lt 1024) {
        throw "Urgent playable screenshot is missing or empty: $path"
    }
    $path
}

[pscustomobject]@{
    Status = 'PASS'
    Viewport = "${Width}x${Height}"
    Log = $logPath
    Screenshots = $screenshots
}
