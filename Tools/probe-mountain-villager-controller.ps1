param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mountain villager controller probe paths must stay on D:.'
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('mountain-villager-controller-profile-' + [Guid]::NewGuid().ToString('N'))
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logPath = Join-Path $logRoot 'mountain-villager-controller-runtime.log'
foreach ($requiredRoot in @($logRoot, $profileRoot, $playerTempRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Mountain Controller QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
if (Test-Path -LiteralPath $logPath -PathType Leaf) { Remove-Item -LiteralPath $logPath -Force }

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-mountain-village-view-probe', '--wof-mountain-villager-controller-probe',
    '--wof-auto-exit=90', "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Mountain villager controller probe has no main window.' }

    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $complete = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_VILLAGER_CONTROLLER_PROBE_COMPLETE interactX=true npc=3:0-mountain-hut-0 town=survival-mountain-villagers-3:0 assignment=(?!none)\S+ messages=3' -Quiet
            $failed = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_VILLAGER_CONTROLLER_PROBE_FAILED' -Quiet
        }
    } while (-not $complete -and -not $failed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if (-not $complete) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) { (Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine } else { '<runtime log was not created>' }
        throw "Mountain villager native-controller probe did not complete.`n$tail"
    }
    $marker = Select-String -LiteralPath $logPath -Pattern 'MOUNTAIN_VILLAGER_CONTROLLER_PROBE_COMPLETE' | Select-Object -Last 1 -ExpandProperty Line
    [PSCustomObject]@{ Complete=$complete; Marker=$marker; Log=$logPath }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
