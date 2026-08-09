param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Darrel grove return probe paths must stay on D:.'
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('darrel-grove-return-profile-' + [Guid]::NewGuid().ToString('N'))
$profilePath = Join-Path $profileRoot 'survival-save-v1.json'
$logPath = Join-Path $logRoot 'darrel-grove-return-runtime.log'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
if (Test-Path -LiteralPath $logPath -PathType Leaf) {
    Remove-Item -LiteralPath $logPath -Force
}
[System.IO.File]::WriteAllText(
    $profilePath,
    '{"version":1,"playerName":"Return QA","darrelHealingCrystalsQuestStatus":"assigned","darrelHealingCrystalsAssignedAt":1000,"questUnlockedSpells":["blink"],"spellQuestAssignments":[{"npcId":"-64--48","townId":"base-village","displayName":"Darrel","questId":"spellquest:healingcrystals","spell":"healingcrystals","status":"assigned","assignedAt":1000}],"questFlags":[{"key":"darrel:healingcrystals:accepted","value":"true"},{"key":"quest:spellquest:healingcrystals","value":"started"},{"key":"darrel:garden-draught","value":"brewed"}],"inventory":[{"itemId":"garden-draught","quantity":1,"acquiredAt":1000}]}')

$arguments = @(
    '-batchmode', '-nographics', '-force-d3d11',
    '--wof-solo', '--wof-darrel-grove-return-probe', '--wof-auto-exit=60',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru -WindowStyle Hidden
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $complete = $false
    $failed = $false
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $complete = Select-String -LiteralPath $logPath -Pattern 'DARREL_GROVE_RETURN_PROBE_COMPLETE drink=true grove=true gate=true returned=true completed=true crystals=1' -Quiet
            $failed = Select-String -LiteralPath $logPath -Pattern 'DARREL_GROVE_RETURN_PROBE_FAILED' -Quiet
        }
    } while (-not $complete -and -not $failed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if (-not $complete) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine
        } else { '<runtime log was not created>' }
        throw "Darrel grove return probe did not complete.`n$tail"
    }

    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $assignment = @($profile.spellQuestAssignments | Where-Object { $_.questId -eq 'spellquest:healingcrystals' })[0]
    $crystals = @($profile.inventory | Where-Object { $_.itemId -eq 'healing-crystals' })[0]
    $groveFlag = @($profile.questFlags | Where-Object { $_.key -eq 'quest:darrel-grove' })[0]
    if ($profile.darrelHealingCrystalsQuestStatus -ne 'completed' -or
        $assignment.status -ne 'completed' -or
        $groveFlag.value -ne 'completed' -or
        $crystals.quantity -ne 1 -or
        -not (@($profile.questUnlockedSpells) -contains 'healingcrystals')) {
        throw 'Darrel grove return profile persistence did not match the exact completion contract.'
    }

    [PSCustomObject]@{
        ProcessId = $process.Id
        Complete = $complete
        Log = $logPath
        QuestStatus = $profile.darrelHealingCrystalsQuestStatus
        AssignmentStatus = $assignment.status
        Crystals = $crystals.quantity
        SpellUnlocked = $true
        Marker = 'DARREL_GROVE_RETURN_PROBE_COMPLETE drink=true grove=true gate=true returned=true completed=true crystals=1'
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
