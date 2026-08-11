param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mountain access controller probe paths must stay on D:.'
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Windows player was not found at $executable"
}

$logRoot = Join-Path $resolvedOutputRoot 'logs'
$captureRoot = Join-Path $resolvedOutputRoot 'mountain-access-controller'
$profileRoot = Join-Path $resolvedOutputRoot ('mountain-access-controller-profile-' + [Guid]::NewGuid().ToString('N'))
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logPath = Join-Path $logRoot 'mountain-access-controller-runtime.log'
$captureNames = @(
    'mountain-trail-start.png',
    'mountain-trail-midpoint.png',
    'mountain-trail-summit.png'
)
foreach ($requiredRoot in @($logRoot, $captureRoot, $profileRoot, $playerTempRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Mountain Access Controller QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
if (Test-Path -LiteralPath $logPath -PathType Leaf) {
    Remove-Item -LiteralPath $logPath -Force
}
foreach ($captureName in $captureNames) {
    $capturePath = Join-Path $captureRoot $captureName
    if (Test-Path -LiteralPath $capturePath -PathType Leaf) {
        Remove-Item -LiteralPath $capturePath -Force
    }
}

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-mountain-village-view-probe=profile', '--wof-mountain-access-path-probe',
    "--wof-mountain-access-controller-probe=$captureRoot", '--wof-auto-exit=180',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Mountain access controller probe has no main window.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $shell.AppActivate($process.Id) | Out-Null

    $deadline = [DateTime]::UtcNow.AddSeconds(165)
    $complete = $false
    $failed = $false
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $complete = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_ACCESS_CONTROLLER_PROBE_COMPLETE nativeGamepad=true' -Quiet
            $failed = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED' -Quiet
        }
    } while (-not $complete -and -not $failed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if (-not $complete) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 160) -join [Environment]::NewLine
        } else {
            '<runtime log was not created>'
        }
        throw "Mountain access native-controller probe did not complete.`n$tail"
    }

    Add-Type -AssemblyName System.Drawing
    $capturePaths = @()
    foreach ($captureName in $captureNames) {
        $capturePath = Join-Path $captureRoot $captureName
        if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf) -or
            (Get-Item -LiteralPath $capturePath).Length -eq 0) {
            throw "Mountain access controller probe did not create $capturePath"
        }
        $image = [System.Drawing.Image]::FromFile($capturePath)
        try {
            if ($image.Width -ne 1280 -or $image.Height -ne 720) {
                throw "Unexpected capture dimensions for ${captureName}: $($image.Width)x$($image.Height)"
            }
        } finally {
            $image.Dispose()
        }
        $capturePaths += $capturePath
    }

    $marker = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_ACCESS_CONTROLLER_PROBE_COMPLETE' |
        Select-Object -Last 1 -ExpandProperty Line
    [PSCustomObject]@{
        Complete = $complete
        Marker = $marker
        Log = $logPath
        Captures = $capturePaths
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) {
            Stop-Process -Id $process.Id -Force
        }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
