param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Darrel dragon controller probe paths must stay on D:.'
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('darrel-dragon-controller-profile-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot 'darrel-dragon-controller-runtime.log'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
if (Test-Path -LiteralPath $logPath -PathType Leaf) {
    Remove-Item -LiteralPath $logPath -Force
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Dragon QA","darrelHealingCrystalsQuestStatus":"assigned","darrelHealingCrystalsAssignedAt":1000,"questUnlockedSpells":["blink"],"spellQuestAssignments":[{"npcId":"-64--48","townId":"base-village","displayName":"Darrel","questId":"spellquest:healingcrystals","spell":"healingcrystals","status":"assigned","assignedAt":1000}],"questFlags":[{"key":"darrel:healingcrystals:accepted","value":"true"},{"key":"darrel:garden-draught","value":"drunk"},{"key":"quest:spellquest:healingcrystals","value":"started"}],"inventory":[]}')

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-darrel-grove-view-probe', '--wof-darrel-dragon-controller-probe', '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Darrel dragon controller probe has no main window.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $shell.AppActivate($process.Id) | Out-Null

    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    $complete = $false
    $failed = $false
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $complete = Select-String -LiteralPath $logPath -Pattern 'DARREL_DRAGON_CONTROLLER_PROBE_COMPLETE worldPrompt=true interactX=true leftCast=true rightCast=true open=true fight=true death=true respawn=true navigation=true peace=true reward=true close=true crystals=2' -Quiet
            $failed = Select-String -LiteralPath $logPath -Pattern 'DARREL_DRAGON_CONTROLLER_PROBE_FAILED' -Quiet
        }
    } while (-not $complete -and -not $failed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if (-not $complete) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine
        }
        else {
            '<runtime log was not created>'
        }
        throw "Darrel dragon native-controller probe did not complete.`n$tail"
    }

    [PSCustomObject]@{
        ProcessId = $process.Id
        Complete = $complete
        Log = $logPath
        Marker = 'DARREL_DRAGON_CONTROLLER_PROBE_COMPLETE worldPrompt=true interactX=true leftCast=true rightCast=true open=true fight=true death=true respawn=true navigation=true peace=true reward=true close=true crystals=2'
    }
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
}
