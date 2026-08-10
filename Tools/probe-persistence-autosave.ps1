param(
    [string]$OutputRoot = 'D:\tmp\wof-unity\persistence-autosave',
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'
$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$playerPath = Join-Path $projectRoot 'Builds\Windows\WizardsOnlyFools.exe'
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$allowedRoot = [System.IO.Path]::GetFullPath('D:\tmp\wof-unity')
if (-not $resolvedOutputRoot.StartsWith($allowedRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output root must remain below $allowedRoot"
}
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) {
    throw "Windows player is missing: $playerPath"
}
if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutputRoot | Out-Null

$profileRoot = Join-Path $resolvedOutputRoot 'profile'
$profilePath = Join-Path $profileRoot 'survival-save-v1.json'
$backupPath = $profilePath + '.bak'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$screenshotPath = Join-Path $resolvedOutputRoot 'recovered-autosave.png'
New-Item -ItemType Directory -Path $profileRoot | Out-Null

[System.IO.File]::WriteAllText($profilePath, '{ definitely-not-valid-json')
$versionOneBackup = @{
    version = 1
    playerName = 'Persistence QA'
    survivalLevel = 6
    survivalXp = 245
    lastMode = 'solo-survival'
    questUnlockedSpells = @('blink', 'fireball')
    spellQuestAssignments = @()
    questFlags = @()
    inventory = @()
} | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($backupPath, $versionOneBackup)

$arguments = @(
    '--wof-solo',
    '--wof-auto-exit=23',
    "--wof-profile-root=$profileRoot",
    "--wof-screenshot=$screenshotPath",
    '-logFile',
    $logPath
)
$startArguments = @{
    FilePath = $playerPath
    ArgumentList = $arguments
    PassThru = $true
}
if (-not $Visible) {
    $startArguments.WindowStyle = 'Hidden'
}
$process = Start-Process @startArguments
if (-not $process.WaitForExit(60000)) {
    Stop-Process -Id $process.Id -Force
    throw 'Persistence probe player did not exit within 60 seconds.'
}
if ($process.ExitCode -ne 0) {
    throw "Persistence probe player exited with code $($process.ExitCode). Log: $logPath"
}
if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
    throw "Persistence probe log is missing: $logPath"
}

$requiredMarkers = @(
    '[WOF-AUTOMATION] PROFILE_RECOVERED source=backup',
    '[WOF-AUTOMATION] SESSION_READY mode=Solo',
    '[WOF-AUTOMATION] SURVIVAL_AUTOSAVE_SAVED reason=interval version=2'
)
foreach ($marker in $requiredMarkers) {
    if (-not (Select-String -LiteralPath $logPath -SimpleMatch $marker -Quiet)) {
        throw "Persistence probe marker is missing: $marker. Log: $logPath"
    }
}
$fatalPatterns = @(
    'NullReferenceException',
    'InvalidOperationException',
    'ArgumentException:',
    'PROFILE_RECOVERY_FAILED',
    'SURVIVAL_AUTOSAVE_FAILED'
)
foreach ($pattern in $fatalPatterns) {
    if (Select-String -LiteralPath $logPath -SimpleMatch $pattern -Quiet) {
        throw "Persistence probe found a runtime failure matching '$pattern'. Log: $logPath"
    }
}
if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {
    throw "Recovered primary profile is missing: $profilePath"
}
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
if ([int]$profile.version -ne 2 -or
    [string]$profile.playerName -ne 'Persistence QA' -or
    [int]$profile.survivalLevel -ne 6 -or
    [int]$profile.survivalXp -ne 245 -or
    [long]$profile.savedAtUnixMilliseconds -le 0) {
    throw "Recovered profile identity is incorrect: $profilePath"
}
$quarantined = @(Get-ChildItem -LiteralPath $profileRoot -Filter 'survival-save-v1.corrupt-*.json' -File)
if ($quarantined.Count -ne 1) {
    throw "Expected one quarantined corrupt save; found $($quarantined.Count)."
}
if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "The previous valid generation backup is missing: $backupPath"
}
if (-not (Test-Path -LiteralPath $screenshotPath -PathType Leaf)) {
    throw "Persistence probe screenshot is missing: $screenshotPath"
}

[pscustomobject]@{
    status = 'passed'
    profileVersion = [int]$profile.version
    savedAtUnixMilliseconds = [long]$profile.savedAtUnixMilliseconds
    recoveredPlayer = [string]$profile.playerName
    quarantinedCorruptFiles = $quarantined.Count
    screenshot = $screenshotPath
    log = $logPath
} | ConvertTo-Json -Depth 4
