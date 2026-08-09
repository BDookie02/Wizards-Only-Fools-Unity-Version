[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'record', 'verify')]
    [string]$Action = 'plan',

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$snapshotRoot = 'D:\CodexProjects\Wizards-Only-Fools-React-Latest'
$historyRoot = 'D:\CodexProjects\Wizards-Only-Fools-React-GitHub-Main-0e293150'
$unityRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$stateRoot = 'D:\UnityAutomationState\Wizards-Only-Fools-Unity'
$inventoryPath = Join-Path $stateRoot 'react-oracle-inventory.json'
$portableGit = 'D:\UnityTools\GitForWindows\2.55.0.3\cmd\git.exe'
$expectedHistoryCommit = '0e293150cc9d92dcab19f8775889b3c43f2ee54a'

function Assert-DDrivePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals(
        [System.IO.Path]::GetPathRoot($fullPath),
        'D:\',
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing non-D path: $fullPath"
    }
    return $fullPath
}

function Assert-RequiredDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = Assert-DDrivePath -Path $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Required directory is missing: $fullPath"
    }
    return $fullPath
}

function Get-RelativeSlashPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    return $Path.Substring($Root.Length + 1).Replace('\', '/')
}

function Get-FileRecords {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][System.IO.FileInfo[]]$Files
    )

    $records = foreach ($file in @($Files | Sort-Object FullName)) {
        [PSCustomObject]@{
            path = Get-RelativeSlashPath -Root $Root -Path $file.FullName
            length = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    return @($records)
}

function Get-RecordSetEvidence {
    param([Parameter(Mandatory = $true)][object[]]$Records)

    $builder = [System.Text.StringBuilder]::new()
    [long]$totalBytes = 0
    foreach ($record in @($Records)) {
        [void]$builder.Append([string]$record.path)
        [void]$builder.Append('|')
        [void]$builder.Append([string]$record.length)
        [void]$builder.Append('|')
        [void]$builder.Append([string]$record.sha256)
        [void]$builder.Append("`n")
        $totalBytes += [long]$record.length
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digest = ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
    }
    return [PSCustomObject]@{
        fileCount = @($Records).Count
        totalBytes = $totalBytes
        treeSha256 = $digest
    }
}

function Get-SubsetEvidence {
    param(
        [Parameter(Mandatory = $true)][object[]]$AllRecords,
        [Parameter(Mandatory = $true)][scriptblock]$Predicate
    )

    $records = @($AllRecords | Where-Object $Predicate)
    return Get-RecordSetEvidence -Records $records
}

function Invoke-PortableGit {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $previousNoSystem = $env:GIT_CONFIG_NOSYSTEM
    $previousGlobal = $env:GIT_CONFIG_GLOBAL
    $previousHome = $env:HOME
    try {
        $env:GIT_CONFIG_NOSYSTEM = '1'
        $env:GIT_CONFIG_GLOBAL = 'D:\UnityGitProfile\global.gitconfig'
        $env:HOME = 'D:\UnityGitProfile'
        $output = @(& $portableGit @Arguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "PortableGit failed with exit code ${LASTEXITCODE}: $($output -join [Environment]::NewLine)"
        }
        return @($output | ForEach-Object { [string]$_ })
    }
    finally {
        $env:GIT_CONFIG_NOSYSTEM = $previousNoSystem
        $env:GIT_CONFIG_GLOBAL = $previousGlobal
        $env:HOME = $previousHome
    }
}

function Get-NormalizedGitRelationship {
    param([Parameter(Mandatory = $true)][string[]]$TrackedPaths)

    $same = 0
    $different = 0
    $missing = 0
    foreach ($relativePath in @($TrackedPaths)) {
        $historyPath = Join-Path $historyRoot $relativePath
        $snapshotPath = Join-Path $snapshotRoot $relativePath
        if (-not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) {
            $missing++
            continue
        }
        $historyBytes = [System.IO.File]::ReadAllBytes($historyPath)
        $snapshotBytes = [System.IO.File]::ReadAllBytes($snapshotPath)
        $historyIsText = -not ($historyBytes -contains 0)
        $snapshotIsText = -not ($snapshotBytes -contains 0)
        if ($historyIsText -and $snapshotIsText) {
            $historyText = [System.IO.File]::ReadAllText($historyPath).Replace("`r`n", "`n")
            $snapshotText = [System.IO.File]::ReadAllText($snapshotPath).Replace("`r`n", "`n")
            $matches = $historyText -ceq $snapshotText
        }
        else {
            $matches = (Get-FileHash -LiteralPath $historyPath -Algorithm SHA256).Hash -eq
                       (Get-FileHash -LiteralPath $snapshotPath -Algorithm SHA256).Hash
        }
        if ($matches) {
            $same++
        }
        else {
            $different++
        }
    }
    return [PSCustomObject]@{
        trackedFiles = @($TrackedPaths).Count
        normalizedIdentical = $same
        meaningfullyDifferent = $different
        missingAtOldPath = $missing
    }
}

function New-OracleInventory {
    $resolvedSnapshot = Assert-RequiredDirectory -Path $snapshotRoot
    $resolvedHistory = Assert-RequiredDirectory -Path $historyRoot
    Assert-RequiredDirectory -Path $unityRoot | Out-Null
    if (-not (Test-Path -LiteralPath $portableGit -PathType Leaf)) {
        throw "Pinned PortableGit is missing: $portableGit"
    }
    if (Test-Path -LiteralPath (Join-Path $resolvedSnapshot '.git')) {
        throw "The playable snapshot unexpectedly contains Git metadata: $resolvedSnapshot"
    }

    $head = (Invoke-PortableGit -Arguments @('-C', $resolvedHistory, 'rev-parse', 'HEAD') | Select-Object -First 1).Trim()
    if (-not [string]::Equals($head, $expectedHistoryCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "GitHub history oracle is at $head; expected $expectedHistoryCommit."
    }
    $porcelain = @(Invoke-PortableGit -Arguments @('-C', $resolvedHistory, '-c', 'core.autocrlf=false', 'status', '--porcelain'))
    if ($porcelain.Count -gt 0) {
        throw "GitHub history oracle is not clean:`n$($porcelain -join [Environment]::NewLine)"
    }
    $trackedPaths = @(Invoke-PortableGit -Arguments @('-C', $resolvedHistory, 'ls-files'))

    $snapshotFiles = @(Get-ChildItem -LiteralPath $resolvedSnapshot -File -Recurse | Where-Object {
        $_.FullName -notlike "$resolvedSnapshot\node_modules\*"
    })
    $snapshotRecords = @(Get-FileRecords -Root $resolvedSnapshot -Files $snapshotFiles)
    $snapshotEvidence = Get-RecordSetEvidence -Records $snapshotRecords
    $sourceEvidence = Get-SubsetEvidence -AllRecords $snapshotRecords -Predicate {
        $_.path -like 'src/*' -and [System.IO.Path]::GetExtension($_.path) -in @('.ts', '.tsx', '.js', '.jsx')
    }
    $publicEvidence = Get-SubsetEvidence -AllRecords $snapshotRecords -Predicate { $_.path -like 'public/*' }
    $distEvidence = Get-SubsetEvidence -AllRecords $snapshotRecords -Predicate { $_.path -like 'dist/*' }
    $workingEvidence = Get-SubsetEvidence -AllRecords $snapshotRecords -Predicate { $_.path -notlike 'dist/*' }

    $packagePath = Join-Path $resolvedSnapshot 'package.json'
    $lockPath = Join-Path $resolvedSnapshot 'package-lock.json'
    $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
    $featureAreas = foreach ($directory in @(Get-ChildItem -LiteralPath (Join-Path $resolvedSnapshot 'src\game\systems') -Directory | Sort-Object Name)) {
        $count = @(Get-ChildItem -LiteralPath $directory.FullName -File -Recurse | Where-Object {
            $_.Extension -in @('.ts', '.tsx', '.js', '.jsx')
        }).Count
        [PSCustomObject]@{ name = $directory.Name; sourceModules = $count }
    }

    return [PSCustomObject]@{
        schemaVersion = 1
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        playableOracle = [PSCustomObject]@{
            root = $resolvedSnapshot
            gitMetadataPresent = $false
            backendEntryPoint = 'server.js'
            packageName = [string]$package.name
            packageVersion = [string]$package.version
            packageJsonSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
            packageLockSha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant()
            completeSnapshotExcludingNodeModules = $snapshotEvidence
            workingFilesExcludingNodeModulesAndDist = $workingEvidence
            sourceModules = $sourceEvidence
            publicFiles = $publicEvidence
            distBuild = $distEvidence
            featureAreas = @($featureAreas)
            files = $snapshotRecords
        }
        githubHistoryOracle = [PSCustomObject]@{
            root = $resolvedHistory
            repository = 'https://github.com/BDookie02/Wizards-Only-Fools-'
            branch = 'main'
            commit = $head.ToLowerInvariant()
            clean = $true
            trackedFiles = @($trackedPaths).Count
        }
        relationship = Get-NormalizedGitRelationship -TrackedPaths $trackedPaths
    }
}

function Write-InventoryAtomic {
    param([Parameter(Mandatory = $true)]$Inventory)

    New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
    $temporaryPath = Join-Path $stateRoot ("react-oracle-inventory.$([Guid]::NewGuid().ToString('N')).tmp")
    $backupPath = Join-Path $stateRoot ("react-oracle-inventory.$([Guid]::NewGuid().ToString('N')).bak")
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    try {
        [System.IO.File]::WriteAllText($temporaryPath, ($Inventory | ConvertTo-Json -Depth 12) + [Environment]::NewLine, $utf8NoBom)
        Get-Content -LiteralPath $temporaryPath -Raw | ConvertFrom-Json | Out-Null
        if (Test-Path -LiteralPath $inventoryPath -PathType Leaf) {
            [System.IO.File]::Replace($temporaryPath, $inventoryPath, $backupPath)
            Remove-Item -LiteralPath $backupPath -Force
        }
        else {
            [System.IO.File]::Move($temporaryPath, $inventoryPath)
        }
    }
    finally {
        foreach ($path in @($temporaryPath, $backupPath)) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                Remove-Item -LiteralPath $path -Force
            }
        }
    }
}

function Assert-InventoryMatches {
    param(
        [Parameter(Mandatory = $true)]$Recorded,
        [Parameter(Mandatory = $true)]$Observed
    )

    $checks = [ordered]@{
        playableRoot = [string]::Equals([string]$Recorded.playableOracle.root, [string]$Observed.playableOracle.root, [System.StringComparison]::OrdinalIgnoreCase)
        snapshotFileCount = $Recorded.playableOracle.completeSnapshotExcludingNodeModules.fileCount -eq $Observed.playableOracle.completeSnapshotExcludingNodeModules.fileCount
        snapshotTree = $Recorded.playableOracle.completeSnapshotExcludingNodeModules.treeSha256 -eq $Observed.playableOracle.completeSnapshotExcludingNodeModules.treeSha256
        sourceModules = $Recorded.playableOracle.sourceModules.fileCount -eq $Observed.playableOracle.sourceModules.fileCount
        sourceTree = $Recorded.playableOracle.sourceModules.treeSha256 -eq $Observed.playableOracle.sourceModules.treeSha256
        publicTree = $Recorded.playableOracle.publicFiles.treeSha256 -eq $Observed.playableOracle.publicFiles.treeSha256
        distTree = $Recorded.playableOracle.distBuild.treeSha256 -eq $Observed.playableOracle.distBuild.treeSha256
        packageLock = $Recorded.playableOracle.packageLockSha256 -eq $Observed.playableOracle.packageLockSha256
        historyCommit = $Recorded.githubHistoryOracle.commit -eq $Observed.githubHistoryOracle.commit
        historyClean = [bool]$Observed.githubHistoryOracle.clean
        relationship = ($Recorded.relationship.normalizedIdentical -eq $Observed.relationship.normalizedIdentical) -and
                       ($Recorded.relationship.meaningfullyDifferent -eq $Observed.relationship.meaningfullyDifferent) -and
                       ($Recorded.relationship.missingAtOldPath -eq $Observed.relationship.missingAtOldPath)
    }
    $failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object { $_.Key })
    if ($failed.Count -gt 0) {
        throw "React oracle inventory no longer matches: $($failed -join ', ')"
    }
}

foreach ($controlledPath in @($snapshotRoot, $historyRoot, $unityRoot, $stateRoot, $inventoryPath, $portableGit)) {
    Assert-DDrivePath -Path $controlledPath | Out-Null
}

switch ($Action) {
    'plan' {
        [ordered]@{
            action = 'plan'
            mutationPerformed = $false
            playableOracle = $snapshotRoot
            githubHistoryOracle = $historyRoot
            output = $inventoryPath
            excludes = @('node_modules only')
            recordCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' record -Apply"
            verifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' verify"
        } | ConvertTo-Json -Depth 5
    }
    'record' {
        if (-not $Apply) {
            throw "Action 'record' changes D-drive state. Re-run with -Apply."
        }
        $inventory = New-OracleInventory
        Write-InventoryAtomic -Inventory $inventory
        Write-Output "React oracle inventory recorded: $inventoryPath"
        Write-Output "Snapshot files excluding node_modules: $($inventory.playableOracle.completeSnapshotExcludingNodeModules.fileCount)"
        Write-Output "Source modules: $($inventory.playableOracle.sourceModules.fileCount)"
        Write-Output "Public files: $($inventory.playableOracle.publicFiles.fileCount)"
        Write-Output "GitHub commit: $($inventory.githubHistoryOracle.commit)"
    }
    'verify' {
        if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) {
            throw "Recorded React oracle inventory is missing: $inventoryPath"
        }
        $recorded = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
        if ($recorded.schemaVersion -ne 1) {
            throw "Unsupported React oracle inventory schema: $($recorded.schemaVersion)"
        }
        $observed = New-OracleInventory
        Assert-InventoryMatches -Recorded $recorded -Observed $observed
        Write-Output "React oracle inventory verification passed: $inventoryPath"
    }
}
