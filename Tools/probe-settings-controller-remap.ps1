param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\settings-controller-remap'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Controller-remap verification paths must stay on D:.'
}

$tempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$profileRoot = Join-Path $resolvedOutputRoot 'profile'
New-Item -ItemType Directory -Force -Path $tempRoot,$profileRoot | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
$player = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', "--wof-settings-remap-probe=$resolvedOutputRoot", "--wof-profile-root=$profileRoot",
    '--wof-auto-exit=60', '-logFile', $logPath
)
$process = Start-Process -FilePath $player -ArgumentList $arguments -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(65)
    $pass = $null
    $failure = $null
    do {
        Start-Sleep -Milliseconds 100
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $failure = Select-String -LiteralPath $logPath -Pattern 'SETTINGS_CONTROLLER_REMAP_FAIL|NullReferenceException|MissingReferenceException'
            $pass = Select-String -LiteralPath $logPath -Pattern 'SETTINGS_CONTROLLER_REMAP_PASS action=leftCast button=y isolatedFrom=menuSelect'
        }
        $process.Refresh()
    } while (-not $failure -and -not $pass -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if ($failure) { throw "Settings remap probe failed: $($failure.Line -join '; ')" }
    if (-not $pass) { throw 'Settings remap probe exited without its pass marker.' }
    $settingsPath = Join-Path $profileRoot 'settings-v1.json'
    if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) { throw 'Remapped settings file was not persisted.' }
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $leftCast = $settings.controllerBindings | Where-Object action -eq 'leftCast' | Select-Object -First 1
    $menuSelect = $settings.controllerBindings | Where-Object action -eq 'menuSelect' | Select-Object -First 1
    if ($leftCast.button -ne 'y' -or $menuSelect.button -ne 'a') {
        throw "Persisted binding isolation failed: leftCast=$($leftCast.button) menuSelect=$($menuSelect.button)"
    }
    [pscustomobject]@{
        Status = 'PASS'
        LeftCast = $leftCast.button
        MenuSelect = $menuSelect.button
        Screenshot = Join-Path $resolvedOutputRoot 'controller-remap-left-cast-y.png'
        Settings = $settingsPath
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
