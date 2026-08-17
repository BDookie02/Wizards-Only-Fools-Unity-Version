param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\engine-menu-controller'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Engine-menu controller probe paths must stay on D:.'
}

$tempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$profileRoot = Join-Path $resolvedOutputRoot 'profile'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot,$tempRoot,$profileRoot | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
foreach ($artifact in @(
    $logPath,
    (Join-Path $resolvedOutputRoot 'engine-menu-controller-selected.png'),
    (Join-Path $resolvedOutputRoot 'engine-menu-controller-placed.png'),
    (Join-Path $resolvedOutputRoot 'engine-menu-controller-lower-controls.png'))) {
    if (Test-Path -LiteralPath $artifact -PathType Leaf) { Remove-Item -LiteralPath $artifact -Force }
}

$player = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) {
    throw "Windows player not found: $player"
}

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', "--wof-engine-menu-controller-probe=$resolvedOutputRoot",
    "--wof-profile-root=$profileRoot", '--wof-auto-exit=90', '-logFile', $logPath
)
$process = Start-Process -FilePath $player -ArgumentList $arguments -PassThru
try {
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and
             [DateTime]::UtcNow -lt $windowDeadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Engine-menu controller probe has no main window.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $shell.AppActivate($process.Id) | Out-Null

    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    $complete = $false
    $failure = $null
    do {
        Start-Sleep -Milliseconds 100
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $failure = Select-String -LiteralPath $logPath -Pattern 'ENGINE_MENU_CONTROLLER_PROBE_FAIL|NullReferenceException|MissingReferenceException'
            $complete = Select-String -LiteralPath $logPath -Pattern 'ENGINE_MENU_CONTROLLER_PROBE_COMPLETE navigation=true select=true place=true scroll=true back=true placed=[1-9][0-9]*' -Quiet
        }
        $process.Refresh()
    } while (-not $failure -and -not $complete -and -not $process.HasExited -and
             [DateTime]::UtcNow -lt $deadline)

    if ($failure) { throw "Engine-menu controller probe failed: $($failure.Line -join '; ')" }
    if (-not $complete) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine
        }
        else { '<runtime log was not created>' }
        throw "Engine-menu native-controller probe did not complete.`n$tail"
    }

    $screenshots = @(
        (Join-Path $resolvedOutputRoot 'engine-menu-controller-selected.png'),
        (Join-Path $resolvedOutputRoot 'engine-menu-controller-placed.png'),
        (Join-Path $resolvedOutputRoot 'engine-menu-controller-lower-controls.png'))
    foreach ($screenshot in $screenshots) {
        if (-not (Test-Path -LiteralPath $screenshot -PathType Leaf) -or
            (Get-Item -LiteralPath $screenshot).Length -eq 0) {
            throw "Engine-menu controller screenshot is missing or empty: $screenshot"
        }
    }

    [pscustomobject]@{
        Status = 'PASS'
        Marker = 'ENGINE_MENU_CONTROLLER_PROBE_COMPLETE navigation=true select=true place=true scroll=true back=true placed=<positive>'
        SelectedScreenshot = $screenshots[0]
        PlacedScreenshot = $screenshots[1]
        LowerControlsScreenshot = $screenshots[2]
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
