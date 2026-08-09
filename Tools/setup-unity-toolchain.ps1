[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'install', 'open-target', 'upgrade-project', 'upgrade-ngo-patch', 'verify')]
    [string]$Action = 'plan',

    [switch]$Apply,

    [switch]$UseVerifiedComponentCheckpoint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Pinned from Unity's official 6000.3.21f1 release page on 2026-08-06:
# https://unity.com/releases/editor/whats-new/6000.3.21f1
$targetVersion = '6000.3.21f1'
$targetChangeset = 'c02631ffc030'
$expectedUnityCliVersion = '1.0.0-beta.3'

$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$unityCliRoot = 'D:\UnityCli'
$unityCliExecutable = 'D:\UnityCli\LocalAppData\Unity\bin\unity.exe'
$unityCliUserProfile = 'D:\UnityEditorProfile'
$unityCliLocalAppData = Join-Path $unityCliUserProfile 'LocalAppData'
$unityCliRoamingAppData = Join-Path $unityCliUserProfile 'RoamingAppData'
$unityWindowsLocalAppData = Join-Path $unityCliUserProfile 'AppData\Local'
$unityWindowsRoamingAppData = Join-Path $unityCliUserProfile 'AppData\Roaming'
$unityCliTempRoot = 'D:\tmp\wof-unity'
$editorInstallRoot = 'D:\UnityEditors'
$targetEditorRoot = Join-Path $editorInstallRoot $targetVersion
$toolchainStateRoot = 'D:\UnityCli\WofToolchain'
$toolchainLogRoot = Join-Path $toolchainStateRoot 'logs'
$packageCacheRoot = 'D:\UnityPackageCache'
$sourceBackupRoot = 'D:\UnityProjectBackups\Wizards-Only-Fools-Unity'
$activationProjectRoot = 'D:\UnityActivation\6000.3.21f1'
$activationStatePath = Join-Path $toolchainStateRoot 'activation-editor.json'
$automationCheckpointPath = 'D:\UnityAutomationState\Wizards-Only-Fools-Unity\automation-checkpoints.json'
$componentReceiptPath = 'D:\UnityInstallers\6000.3.21f1\component-install-receipts.json'
$componentInstaller = Join-Path $projectRoot 'Tools\install-unity-components.ps1'
$portableGitInitializer = Join-Path $projectRoot 'Tools\initialize-unity-git-environment.ps1'
$packageMigrationTool = Join-Path $projectRoot 'Tools\migrate-unity-packages.ps1'
$packageManifestPath = Join-Path $projectRoot 'Packages\manifest.json'
$packageLockPath = Join-Path $projectRoot 'Packages\packages-lock.json'

# Unity CLI module identifiers are from the official Unity CLI reference:
# https://docs.unity.com/en-us/unity-cli/unity-cli-reference
$requiredModuleIds = @(
    'windows-il2cpp',
    'webgl',
    'windows-server',
    'android',
    'android-sdk-ndk-tools',
    'android-open-jdk'
)

function Assert-DDrivePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)
    if (-not [string]::Equals($pathRoot, 'D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing non-D path: $fullPath"
    }

    return $fullPath
}

function Initialize-DOnlyEnvironment {
    param([switch]$RequirePortableGit)
    $pathsToCreate = @(
        $unityCliRoot,
        $unityCliLocalAppData,
        $unityCliRoamingAppData,
        $unityWindowsLocalAppData,
        $unityWindowsRoamingAppData,
        (Join-Path $unityWindowsLocalAppData 'Unity\Caches'),
        $unityCliTempRoot,
        $unityCliUserProfile,
        $toolchainStateRoot,
        $toolchainLogRoot,
        $packageCacheRoot,
        (Join-Path $packageCacheRoot 'npm'),
        (Join-Path $packageCacheRoot 'packages'),
        (Join-Path $packageCacheRoot 'git-lfs'),
        $sourceBackupRoot
    )

    foreach ($path in $pathsToCreate) {
        $validatedPath = Assert-DDrivePath -Path $path
        New-Item -ItemType Directory -Force -Path $validatedPath | Out-Null
    }

    # These values apply only to this PowerShell process and its children.
    $env:TEMP = $unityCliTempRoot
    $env:TMP = $unityCliTempRoot
    # Preserve the normal Windows identity environment so the Editor can see the
    # Unity Hub-authenticated licensing client. Large mutable caches remain pinned
    # to D: through the explicit UPM, temp, project, build, and Android paths below.
    $env:UPM_CACHE_ROOT = $packageCacheRoot
    $env:UPM_NPM_CACHE_PATH = Join-Path $packageCacheRoot 'npm'
    $env:UPM_CACHE_PATH = Join-Path $packageCacheRoot 'packages'
    $env:UPM_GIT_LFS_CACHE_PATH = Join-Path $packageCacheRoot 'git-lfs'
    $env:UNITY_NON_INTERACTIVE = '1'
    $env:UNITY_NO_UPDATE_CHECK = '1'
    $env:UNITY_NO_CONSENT_PROMPT = '1'

    if ($RequirePortableGit) {
        if (-not (Test-Path -LiteralPath $portableGitInitializer -PathType Leaf)) {
            throw "Portable Git environment initializer is missing: $portableGitInitializer"
        }
        $additionalPathEntries = @(
            'D:\UnityRuntimeCompat',
            (Join-Path $targetEditorRoot 'Editor'),
            (Join-Path $targetEditorRoot 'Editor\Data\Tools'),
            (Join-Path $targetEditorRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin'),
            (Join-Path $targetEditorRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools'),
            (Join-Path $targetEditorRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
        & $portableGitInitializer -AdditionalDPath $additionalPathEntries -SkipFullPayloadVerification | Out-Null
    }
}

function Get-ProjectEditorIdentity {
    $versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "Unity project version file not found: $versionFile"
    }

    $versionLine = Get-Content -LiteralPath $versionFile |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    $revisionLine = Get-Content -LiteralPath $versionFile |
        Where-Object { $_ -match '^m_EditorVersionWithRevision:\s*(.+)$' } |
        Select-Object -First 1

    if (-not $versionLine) {
        throw "Could not parse m_EditorVersion from $versionFile"
    }

    $version = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
    $revision = if ($revisionLine) {
        ($revisionLine -replace '^m_EditorVersionWithRevision:\s*', '').Trim()
    }
    else {
        ''
    }

    return [PSCustomObject]@{
        Version = $version
        Revision = $revision
    }
}

function Get-SourceRunnerIdentity {
    $runnerPath = Join-Path $projectRoot 'Tools\wof-unity.ps1'
    if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
        return [PSCustomObject]@{ Description = 'missing'; Ready = $false }
    }

    $runnerText = Get-Content -LiteralPath $runnerPath -Raw
    $usesProjectVersion = $runnerText -match 'ProjectVersion\.txt'
    if ($usesProjectVersion) {
        return [PSCustomObject]@{
            Description = 'resolved from ProjectVersion.txt'
            Ready = $true
        }
    }

    $pinMatch = [regex]::Match($runnerText, 'Unity\\Hub\\Editor\\([^\\''"]+)\\Editor')
    if ($pinMatch.Success) {
        $pinnedVersion = $pinMatch.Groups[1].Value
        return [PSCustomObject]@{
            Description = "pinned $pinnedVersion"
            Ready = ($pinnedVersion -eq $targetVersion)
        }
    }

    return [PSCustomObject]@{
        Description = 'no verifiable Editor resolver'
        Ready = $false
    }
}

function Test-TargetEditorInstalled {
    $unityExecutable = Join-Path $targetEditorRoot 'Editor\Unity.exe'
    $unityConsole = Join-Path $targetEditorRoot 'Editor\Unity.com'
    if (-not ((Test-Path -LiteralPath $unityExecutable -PathType Leaf) -and
        (Test-Path -LiteralPath $unityConsole -PathType Leaf))) {
        return $false
    }

    # The signed Editor executable embeds both the exact version and changeset.
    # This remains authoritative when an Editor is installed directly and has
    # not yet been registered with Unity CLI (so modules.json does not exist).
    $productVersion = (Get-Item -LiteralPath $unityExecutable).VersionInfo.ProductVersion
    return [string]::Equals(
        $productVersion,
        "$targetVersion`_$targetChangeset",
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-RequiredModule {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('windows-il2cpp', 'webgl', 'windows-server', 'android', 'android-sdk-ndk-tools', 'android-open-jdk')]
        [string]$ModuleId
    )

    $playbackRoot = Join-Path $targetEditorRoot 'Editor\Data\PlaybackEngines'
    switch ($ModuleId) {
        'windows-il2cpp' {
            $variationsRoot = Join-Path $playbackRoot 'windowsstandalonesupport\Variations'
            if (-not (Test-Path -LiteralPath $variationsRoot -PathType Container)) {
                return $false
            }

            $il2CppVariation = Get-ChildItem -LiteralPath $variationsRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^win64_.*il2cpp$' } |
                Select-Object -First 1
            return ($null -ne $il2CppVariation)
        }
        'webgl' {
            return Test-Path -LiteralPath (Join-Path $playbackRoot 'WebGLSupport\BuildTools') -PathType Container
        }
        'windows-server' {
            $variationsRoot = Join-Path $playbackRoot 'windowsstandalonesupport\Variations'
            if (-not (Test-Path -LiteralPath $variationsRoot -PathType Container)) {
                return $false
            }

            $serverVariation = Get-ChildItem -LiteralPath $variationsRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^win64_.*server.*$|^win64_server_.*$' } |
                Select-Object -First 1
            if ($null -ne $serverVariation) {
                return $true
            }

            # Unity's module layout can change between Editor streams. The Hub-
            # compatible manifest is the fallback proof for the exact module.
            $moduleManifest = Join-Path $targetEditorRoot 'modules.json'
            if (-not (Test-Path -LiteralPath $moduleManifest -PathType Leaf)) {
                return $false
            }
            $manifestText = Get-Content -LiteralPath $moduleManifest -Raw
            $manifestPattern = '"id"\s*:\s*"windows-server".{0,4096}?"isInstalled"\s*:\s*true'
            return [regex]::IsMatch($manifestText, $manifestPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
        }
        'android' {
            return Test-Path -LiteralPath (Join-Path $playbackRoot 'AndroidPlayer\modules.asset') -PathType Leaf
        }
        'android-sdk-ndk-tools' {
            $adb = Join-Path $playbackRoot 'AndroidPlayer\SDK\platform-tools\adb.exe'
            $ndkBuild = Join-Path $playbackRoot 'AndroidPlayer\NDK\ndk-build.cmd'
            return ((Test-Path -LiteralPath $adb -PathType Leaf) -and (Test-Path -LiteralPath $ndkBuild -PathType Leaf))
        }
        'android-open-jdk' {
            return Test-Path -LiteralPath (Join-Path $playbackRoot 'AndroidPlayer\OpenJDK\bin\java.exe') -PathType Leaf
        }
    }
}

function Get-ToolchainStatus {
    $editorInstalled = Test-TargetEditorInstalled
    $moduleStatus = foreach ($moduleId in $requiredModuleIds) {
        [PSCustomObject]@{
            Component = $moduleId
            Ready = if ($editorInstalled) { Test-RequiredModule -ModuleId $moduleId } else { $false }
        }
    }

    $projectIdentity = Get-ProjectEditorIdentity
    $sourceRunnerIdentity = Get-SourceRunnerIdentity
    $projectAtTarget = (
        $projectIdentity.Version -eq $targetVersion -and
        $projectIdentity.Revision -match [regex]::Escape($targetChangeset)
    )

    return [PSCustomObject]@{
        EditorInstalled = $editorInstalled
        ModuleStatus = @($moduleStatus)
        MissingModules = @($moduleStatus | Where-Object { -not $_.Ready } | ForEach-Object { $_.Component })
        ProjectIdentity = $projectIdentity
        ProjectAtTarget = $projectAtTarget
        SourceRunnerIdentity = $sourceRunnerIdentity
    }
}

function Write-ToolchainStatus {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Status
    )

    $drive = Get-PSDrive -Name D
    $freeGiB = [Math]::Round($drive.Free / 1GB, 2)

    Write-Output "Pinned Editor: $targetVersion ($targetChangeset)"
    Write-Output "Unity CLI (informational; not used for component installation): $unityCliExecutable"
    Write-Output "Expected Unity CLI version (informational): $expectedUnityCliVersion"
    Write-Output "Direct component installer: $componentInstaller"
    Write-Output "Package migration tool: $packageMigrationTool"
    Write-Output "Editor root: $targetEditorRoot"
    Write-Output "D: free space: $freeGiB GiB"
    Write-Output "Exact Editor installed: $($Status.EditorInstalled)"
    Write-Output "Project Editor: $($Status.ProjectIdentity.Revision)"
    Write-Output "Project migrated to pinned Editor: $($Status.ProjectAtTarget)"
    Write-Output "Source runner: $($Status.SourceRunnerIdentity.Description)"
    Write-Output "Source runner safe for target Editor: $($Status.SourceRunnerIdentity.Ready)"
    $Status.ModuleStatus | Format-Table -AutoSize | Out-String | Write-Output
}

function Assert-ApplyRequested {
    if (-not $Apply) {
        throw "Action '$Action' changes local state. Re-run with -Apply after reviewing the plan."
    }
}

function Assert-VerifiedComponentCheckpoint {
    if (-not $UseVerifiedComponentCheckpoint) {
        throw 'The verified component checkpoint switch was not supplied.'
    }
    if (-not (Test-Path -LiteralPath $automationCheckpointPath -PathType Leaf)) {
        throw "The D-drive component checkpoint is missing: $automationCheckpointPath"
    }
    if (-not (Test-Path -LiteralPath $componentReceiptPath -PathType Leaf)) {
        throw "The verified component receipt is missing: $componentReceiptPath"
    }

    $document = Get-Content -LiteralPath $automationCheckpointPath -Raw | ConvertFrom-Json
    if (($document.schemaVersion -ne 1) -or
        (-not [string]::Equals([string]$document.projectRoot, $projectRoot, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "The D-drive automation checkpoint identity is invalid: $automationCheckpointPath"
    }
    $stagesProperty = $document.PSObject.Properties['stages']
    $stageProperty = if ($null -ne $stagesProperty) { $stagesProperty.Value.PSObject.Properties['platform-components'] } else { $null }
    if ($null -eq $stageProperty -or $null -eq $stageProperty.Value) {
        throw "The platform-components checkpoint is absent: $automationCheckpointPath"
    }
    if (@($stageProperty.Value.requiredPaths).Count -ne 12) {
        throw "The platform-components checkpoint does not contain the exact 12 required D-drive markers: $automationCheckpointPath"
    }

    $savedReceipt = @($stageProperty.Value.evidence | Where-Object {
        [string]::Equals([string]$_.path, $componentReceiptPath, [System.StringComparison]::OrdinalIgnoreCase)
    }) | Select-Object -First 1
    $observedReceiptHash = (Get-FileHash -LiteralPath $componentReceiptPath -Algorithm SHA256).Hash
    if ($null -eq $savedReceipt -or
        -not [string]::Equals([string]$savedReceipt.sha256, $observedReceiptHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The platform-components checkpoint no longer matches its verified receipt: $componentReceiptPath"
    }

    foreach ($requiredPath in @($stageProperty.Value.requiredPaths)) {
        $fullPath = Assert-DDrivePath -Path ([string]$requiredPath)
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "A path required by the verified platform-components checkpoint is missing: $fullPath"
        }
    }
    Write-Output 'Accepted the hash-matched D-drive platform-components checkpoint; skipped redundant full component payload hashing.'
}

function Confirm-ComponentIntegrity {
    if ($UseVerifiedComponentCheckpoint) {
        Assert-VerifiedComponentCheckpoint
    }
    else {
        Invoke-ComponentInstaller -ComponentAction verify
    }
}

function Get-ObservedUnityCliVersion {
    $cliLog = Join-Path $unityCliRoamingAppData 'UnityHub\logs\cli-log.json'
    if (-not (Test-Path -LiteralPath $cliLog -PathType Leaf)) {
        return ''
    }

    $versionRecord = Select-String -LiteralPath $cliLog -Pattern 'Unity CLI started.+version\s+([^" ]+)' |
        Select-Object -Last 1
    if (-not $versionRecord -or $versionRecord.Line -notmatch 'Unity CLI started.+version\s+([^" ]+)') {
        return ''
    }

    return $Matches[1]
}

function Assert-ComponentInstallerReady {
    $validatedPath = Assert-DDrivePath -Path $componentInstaller
    if (-not (Test-Path -LiteralPath $validatedPath -PathType Leaf)) {
        throw "Direct Unity component installer not found: $validatedPath"
    }
}

function Invoke-ComponentInstaller {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('install', 'verify')]
        [string]$ComponentAction,

        [switch]$ApplyComponents
    )

    Assert-ComponentInstallerReady

    if ($ApplyComponents) {
        & $componentInstaller -Action $ComponentAction -Apply
    }
    else {
        & $componentInstaller -Action $ComponentAction
    }

    if (-not $?) {
        throw "Direct Unity component installer action '$ComponentAction' failed."
    }
}

function Assert-PackageMigrationToolReady {
    $validatedPath = Assert-DDrivePath -Path $packageMigrationTool
    if (-not (Test-Path -LiteralPath $validatedPath -PathType Leaf)) {
        throw "Package migration tool not found: $validatedPath"
    }
}

function Invoke-PackageMigration {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('plan', 'apply', 'verify', 'apply-ngo-patch')]
        [string]$PackageAction,

        [Parameter(Mandatory = $true)]
        [string]$PhaseName
    )

    Assert-PackageMigrationToolReady
    try {
        & $packageMigrationTool -Action $PackageAction
        if (-not $?) {
            throw "Package tool returned an unsuccessful status for action '$PackageAction'."
        }
    }
    catch {
        throw "Package migration phase '$PhaseName' failed. $($_.Exception.Message)"
    }
}

function Get-PackageVersionFromJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$PackageName,

        [switch]$LockFile
    )

    $validatedPath = Assert-DDrivePath -Path $Path
    if (-not (Test-Path -LiteralPath $validatedPath -PathType Leaf)) {
        throw "Package JSON file not found: $validatedPath"
    }

    try {
        $json = Get-Content -LiteralPath $validatedPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON in ${validatedPath}: $($_.Exception.Message)"
    }

    $dependenciesProperty = $json.PSObject.Properties['dependencies']
    if ($null -eq $dependenciesProperty -or $null -eq $dependenciesProperty.Value) {
        throw "Package JSON does not contain a dependencies object: $validatedPath"
    }

    $packageProperty = $dependenciesProperty.Value.PSObject.Properties[$PackageName]
    if ($null -eq $packageProperty -or $null -eq $packageProperty.Value) {
        throw "Package JSON does not contain ${PackageName}: $validatedPath"
    }

    if (-not $LockFile) {
        return [string]$packageProperty.Value
    }

    $versionProperty = $packageProperty.Value.PSObject.Properties['version']
    if ($null -eq $versionProperty -or [string]::IsNullOrWhiteSpace([string]$versionProperty.Value)) {
        throw "Package lock entry does not contain a version for ${PackageName}: $validatedPath"
    }

    return [string]$versionProperty.Value
}

function Assert-PackageManifestNgoVersion {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('2.13.0', '2.13.1')]
        [string]$ExpectedVersion,

        [Parameter(Mandatory = $true)]
        [string]$PhaseName
    )

    $manifestVersion = Get-PackageVersionFromJson -Path $packageManifestPath -PackageName 'com.unity.netcode.gameobjects'
    if ($manifestVersion -ne $ExpectedVersion) {
        throw "Package migration phase '$PhaseName' left manifest NGO at $manifestVersion; expected $ExpectedVersion."
    }
}

function Assert-PackageNgoState {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('2.13.0', '2.13.1')]
        [string]$ExpectedVersion,

        [Parameter(Mandatory = $true)]
        [string]$PhaseName
    )

    Assert-PackageManifestNgoVersion -ExpectedVersion $ExpectedVersion -PhaseName $PhaseName
    $lockVersion = Get-PackageVersionFromJson -Path $packageLockPath -PackageName 'com.unity.netcode.gameobjects' -LockFile
    if ($lockVersion -ne $ExpectedVersion) {
        throw "Package migration phase '$PhaseName' left packages-lock NGO at $lockVersion; expected $ExpectedVersion."
    }
}

function Assert-UnityProjectClosed {
    $lockFile = Join-Path $projectRoot 'Temp\UnityLockfile'
    Assert-DDrivePath -Path $lockFile | Out-Null
    if (Test-Path -LiteralPath $lockFile) {
        $normalizedProjectRoot = $projectRoot.Replace('\', '[\\/]')
        $liveProjectEditors = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -in @('Unity.exe', 'Unity.com') -and
            [string]$_.CommandLine -match $normalizedProjectRoot
        })
        if ($liveProjectEditors.Count -gt 0) {
            throw "Unity has this project open in PID(s) $($liveProjectEditors.ProcessId -join ', ') ($lockFile exists). Close the Editor before batch migration."
        }

        $lockProbe = $null
        try {
            $lockProbe = [System.IO.File]::Open(
                $lockFile,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
        }
        catch {
            throw "Unity's project lock is still held even though no matching Editor process was discoverable: $lockFile. $($_.Exception.Message)"
        }
        finally {
            if ($null -ne $lockProbe) {
                $lockProbe.Dispose()
            }
        }

        $staleLockRoot = 'D:\UnityAutomationState\Wizards-Only-Fools-Unity\stale-unity-locks'
        Assert-DDrivePath -Path $staleLockRoot | Out-Null
        New-Item -ItemType Directory -Force -Path $staleLockRoot | Out-Null
        $staleLockPath = Join-Path $staleLockRoot ("UnityLockfile-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))")
        Move-Item -LiteralPath $lockFile -Destination $staleLockPath
        Write-Output "Quarantined an unheld stale Unity project lock after confirming no matching Editor process: $staleLockPath"
    }

    if (Test-Path -LiteralPath $activationStatePath -PathType Leaf) {
        try {
            $activationState = Get-Content -LiteralPath $activationStatePath -Raw | ConvertFrom-Json
            $activationProcess = Get-Process -Id ([int]$activationState.processId) -ErrorAction SilentlyContinue
            if ($null -ne $activationProcess) {
                $expectedEditor = Join-Path $targetEditorRoot 'Editor\Unity.exe'
                $observedPath = $activationProcess.Path
                if ([string]::Equals($observedPath, $expectedEditor, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "The Unity activation Editor is still running as PID $($activationState.processId). Close it before batch migration."
                }
            }
        }
        catch {
            if ($_.Exception.Message -match '^The Unity activation Editor is still running') {
                throw
            }
            throw "Could not safely validate the D-drive activation Editor state at $activationStatePath. $($_.Exception.Message)"
        }
    }
}

function Invoke-UnityProjectResolution {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PhaseName
    )

    Assert-UnityProjectClosed

    $unityConsole = Join-Path $targetEditorRoot 'Editor\Unity.com'
    if (-not (Test-Path -LiteralPath $unityConsole -PathType Leaf)) {
        throw "Unity console executable not found: $unityConsole"
    }

    $safePhaseName = $PhaseName -replace '[^0-9A-Za-z._-]', '_'
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ', [System.Globalization.CultureInfo]::InvariantCulture)
    $upgradeLog = Join-Path $toolchainLogRoot "$safePhaseName-$targetVersion-$stamp.log"
    Assert-DDrivePath -Path $upgradeLog | Out-Null

    Push-Location 'D:\'
    try {
        & $unityConsole @(
            '-batchmode',
            '-nographics',
            '-accept-apiupdate',
            '-projectPath', $projectRoot,
            '-logFile', $upgradeLog,
            '-quit'
        )
        $unityExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($unityExitCode -ne 0) {
        $tail = Get-Content -LiteralPath $upgradeLog -Tail 120 -ErrorAction SilentlyContinue
        throw "Unity resolution phase '$PhaseName' failed with exit code $unityExitCode. Log: $upgradeLog`n$($tail -join [Environment]::NewLine)"
    }

    $completedStatus = Get-ToolchainStatus
    if (-not $completedStatus.ProjectAtTarget) {
        throw "Unity resolution phase '$PhaseName' exited successfully, but ProjectVersion.txt does not confirm $targetVersion ($targetChangeset). Log: $upgradeLog"
    }

    Write-Output "Unity resolution phase '$PhaseName' passed. Log: $upgradeLog"
}

function Install-Toolchain {
    Assert-ApplyRequested
    $status = Get-ToolchainStatus
    if (-not $status.EditorInstalled) {
        throw "The exact base Editor is not installed at $targetEditorRoot. Install the signed $targetVersion ($targetChangeset) Editor there before installing components."
    }

    if ($status.MissingModules.Count -gt 0) {
        Write-Output "Installing missing direct components: $($status.MissingModules -join ', ')"
        Invoke-ComponentInstaller -ComponentAction install -ApplyComponents
    }
    else {
        Write-Output 'All required component markers are present; running one read-only full artifact and receipt verification pass.'
        Invoke-ComponentInstaller -ComponentAction verify
    }

    $completedStatus = Get-ToolchainStatus
    if (-not $completedStatus.EditorInstalled -or $completedStatus.MissingModules.Count -gt 0) {
        $missing = $completedStatus.MissingModules -join ', '
        throw "Toolchain install did not pass verification. Missing modules: $missing"
    }

    Write-Output "Unity toolchain ready at $targetEditorRoot"
}

function Get-SourceTreeInventory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $fullRoot = (Assert-DDrivePath -Path $Root).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Backup source tree not found: $fullRoot"
    }

    $relativeRootPath = $fullRoot.Substring(3).Trim('\')
    $rootCursor = 'D:\'
    foreach ($segment in @($relativeRootPath.Split('\') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $rootCursor = Join-Path $rootCursor $segment
        $rootItem = Get-Item -LiteralPath $rootCursor -Force
        if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing a reparse point in the backup source path: $rootCursor"
        }
    }

    $records = New-Object System.Collections.Generic.List[string]
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($fullRoot)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to traverse a reparse point while backing up: $($item.FullName)"
            }

            $relative = $item.FullName.Substring($fullRoot.Length).TrimStart('\')
            if ($item.PSIsContainer) {
                $records.Add("D|$relative")
                $pending.Push($item.FullName)
            }
            else {
                $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
                $records.Add("F|$relative|$($item.Length)|$hash")
            }
        }
    }

    $recordArray = $records.ToArray()
    [Array]::Sort($recordArray, [System.StringComparer]::Ordinal)
    return $recordArray
}

function Backup-ProjectSourceForUpgrade {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceVersion
    )

    $safeSourceVersion = $SourceVersion -replace '[^0-9A-Za-z._-]', '_'
    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ', [System.Globalization.CultureInfo]::InvariantCulture)
    $backupPath = Join-Path $sourceBackupRoot "$timestamp-pre-$targetVersion-from-$safeSourceVersion"
    $stagingPath = "$backupPath.partial-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    Assert-DDrivePath -Path $backupPath | Out-Null
    Assert-DDrivePath -Path $stagingPath | Out-Null
    if ((Test-Path -LiteralPath $backupPath) -or (Test-Path -LiteralPath $stagingPath)) {
        throw "Unexpected source-backup path collision: $backupPath"
    }

    New-Item -ItemType Directory -Force -Path $sourceBackupRoot | Out-Null
    New-Item -ItemType Directory -Path $stagingPath | Out-Null
    foreach ($sourceName in @('Assets', 'Packages', 'ProjectSettings')) {
        $sourcePath = Join-Path $projectRoot $sourceName
        $destinationPath = Join-Path $stagingPath $sourceName
        Assert-DDrivePath -Path $sourcePath | Out-Null
        Assert-DDrivePath -Path $destinationPath | Out-Null
        $sourceInventory = @(Get-SourceTreeInventory -Root $sourcePath)
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Recurse
        $destinationInventory = @(Get-SourceTreeInventory -Root $destinationPath)
        $differences = @(Compare-Object -ReferenceObject $sourceInventory -DifferenceObject $destinationInventory)
        if ($differences.Count -gt 0) {
            throw "Source backup verification failed for $sourceName. Incomplete staging was retained: $stagingPath"
        }
    }

    $metadata = [ordered]@{
        schemaVersion = 1
        completedUtc = [DateTime]::UtcNow.ToString('o')
        sourceProject = $projectRoot
        sourceVersion = $SourceVersion
        targetVersion = $targetVersion
        targetChangeset = $targetChangeset
        manifestSha256 = (Get-FileHash -LiteralPath $packageManifestPath -Algorithm SHA256).Hash
        packagesLockSha256 = (Get-FileHash -LiteralPath $packageLockPath -Algorithm SHA256).Hash
        projectVersionSha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') -Algorithm SHA256).Hash
    }
    $metadataPath = Join-Path $stagingPath 'backup-complete.json'
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 4) + [Environment]::NewLine, $utf8NoBom)
    Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json | Out-Null

    Move-Item -LiteralPath $stagingPath -Destination $backupPath
    if (-not (Test-Path -LiteralPath (Join-Path $backupPath 'backup-complete.json') -PathType Leaf)) {
        throw "Source backup promotion did not produce its completion record: $backupPath"
    }

    Write-Output "Created and verified source backup: $backupPath"
    return $backupPath
}

function Upgrade-Project {
    Assert-ApplyRequested

    Confirm-ComponentIntegrity
    $status = Get-ToolchainStatus
    if (-not $status.EditorInstalled -or $status.MissingModules.Count -gt 0) {
        throw "The pinned Editor and modules are not ready. Run 'install -Apply' first."
    }
    Assert-UnityProjectClosed

    $currentNgoVersion = Get-PackageVersionFromJson -Path $packageManifestPath -PackageName 'com.unity.netcode.gameobjects'
    if ($currentNgoVersion -eq '2.13.1') {
        throw "Baseline phase cannot run because manifest NGO is already 2.13.1. Run 'upgrade-ngo-patch -Apply' to resume and verify the final phase without downgrading it."
    }

    Write-Output 'Baseline phase 1/4: retaining the pre-6000.3 source backup.'
    Backup-ProjectSourceForUpgrade -SourceVersion $status.ProjectIdentity.Version | Out-Null

    Write-Output 'Baseline phase 2/4: applying Unity 6000.3 package pins with NGO 2.13.0 before the first 6000.3 project open.'
    Invoke-PackageMigration -PackageAction apply -PhaseName 'baseline-manifest-apply'
    Assert-PackageManifestNgoVersion -ExpectedVersion '2.13.0' -PhaseName 'baseline-manifest-apply'

    Write-Output 'Baseline phase 3/4: resolving the baseline with Unity 6000.3.'
    Invoke-UnityProjectResolution -PhaseName 'baseline-resolution'

    Write-Output 'Baseline phase 4/4: verifying the resolved baseline before any NGO patch is allowed.'
    Invoke-PackageMigration -PackageAction verify -PhaseName 'baseline-verification'
    Assert-PackageNgoState -ExpectedVersion '2.13.0' -PhaseName 'baseline-verification'

    Write-Output "Unity $targetVersion baseline verified with NGO 2.13.0. The NGO 2.13.1 patch remains isolated."
    Write-Output "Next command: powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' upgrade-ngo-patch -Apply"
}

function Upgrade-NgoPatch {
    Assert-ApplyRequested

    Confirm-ComponentIntegrity
    $status = Get-ToolchainStatus
    if (-not $status.EditorInstalled -or $status.MissingModules.Count -gt 0) {
        throw "The pinned Editor and modules are not ready. Run 'install -Apply' first."
    }
    if (-not $status.ProjectAtTarget) {
        throw "The Unity 6000.3 baseline has not resolved the project identity. Run 'upgrade-project -Apply' first."
    }
    Assert-UnityProjectClosed

    $currentNgoVersion = Get-PackageVersionFromJson -Path $packageManifestPath -PackageName 'com.unity.netcode.gameobjects'
    if ($currentNgoVersion -eq '2.13.0') {
        Write-Output 'NGO patch phase 1/4: verifying the isolated 2.13.0 baseline.'
        Invoke-PackageMigration -PackageAction verify -PhaseName 'pre-ngo-patch-baseline-verification'
        Assert-PackageNgoState -ExpectedVersion '2.13.0' -PhaseName 'pre-ngo-patch-baseline-verification'
    }
    elseif ($currentNgoVersion -eq '2.13.1') {
        Write-Output 'NGO patch phase 1/4: manifest already has 2.13.1; resuming the final resolution without downgrading.'
    }
    else {
        throw "NGO patch phase requires manifest NGO 2.13.0 or resumable 2.13.1; observed $currentNgoVersion. Run 'upgrade-project -Apply' first."
    }

    Write-Output 'NGO patch phase 2/4: applying only the NGO 2.13.1 manifest patch.'
    Invoke-PackageMigration -PackageAction apply-ngo-patch -PhaseName 'ngo-2.13.1-manifest-apply'
    Assert-PackageManifestNgoVersion -ExpectedVersion '2.13.1' -PhaseName 'ngo-2.13.1-manifest-apply'

    Write-Output 'NGO patch phase 3/4: resolving the NGO patch with Unity 6000.3.'
    Invoke-UnityProjectResolution -PhaseName 'ngo-2.13.1-resolution'

    Write-Output 'NGO patch phase 4/4: verifying the final package and lock state.'
    Invoke-PackageMigration -PackageAction verify -PhaseName 'ngo-2.13.1-final-verification'
    Assert-PackageNgoState -ExpectedVersion '2.13.1' -PhaseName 'ngo-2.13.1-final-verification'

    Write-Output "Unity $targetVersion package migration verified at final NGO 2.13.1."
}

function Open-TargetEditor {
    Assert-ApplyRequested

    if (-not (Test-TargetEditorInstalled)) {
        throw "The exact $targetVersion ($targetChangeset) Editor is not installed at $targetEditorRoot."
    }
    Assert-UnityProjectClosed

    foreach ($path in @(
        $activationProjectRoot,
        (Join-Path $activationProjectRoot 'Assets'),
        (Join-Path $activationProjectRoot 'Packages'),
        (Join-Path $activationProjectRoot 'ProjectSettings')
    )) {
        Assert-DDrivePath -Path $path | Out-Null
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $activationManifestPath = Join-Path $activationProjectRoot 'Packages\manifest.json'
    $activationVersionPath = Join-Path $activationProjectRoot 'ProjectSettings\ProjectVersion.txt'
    [System.IO.File]::WriteAllText(
        $activationManifestPath,
        "{`n  `"dependencies`": {}`n}`n",
        $utf8NoBom)
    [System.IO.File]::WriteAllText(
        $activationVersionPath,
        "m_EditorVersion: $targetVersion`nm_EditorVersionWithRevision: $targetVersion ($targetChangeset)`n",
        $utf8NoBom)

    $editorExecutable = Join-Path $targetEditorRoot 'Editor\Unity.exe'
    Remove-Item Env:UNITY_NON_INTERACTIVE -ErrorAction SilentlyContinue
    Remove-Item Env:UNITY_NO_CONSENT_PROMPT -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $editorExecutable -ArgumentList @('-projectPath', $activationProjectRoot) -WorkingDirectory 'D:\' -PassThru
    $activationState = [ordered]@{
        schemaVersion = 1
        startedUtc = [DateTime]::UtcNow.ToString('o')
        processId = $process.Id
        editorPath = $editorExecutable
        activationProject = $activationProjectRoot
    }
    $activationStateTemp = "$activationStatePath.$([Guid]::NewGuid().ToString('N')).tmp"
    $activationStateBackup = "$activationStatePath.$([Guid]::NewGuid().ToString('N')).bak"
    try {
        [System.IO.File]::WriteAllText(
            $activationStateTemp,
            ($activationState | ConvertTo-Json -Depth 4) + [Environment]::NewLine,
            $utf8NoBom)
        Get-Content -LiteralPath $activationStateTemp -Raw | ConvertFrom-Json | Out-Null
        if (Test-Path -LiteralPath $activationStatePath -PathType Leaf) {
            [System.IO.File]::Replace($activationStateTemp, $activationStatePath, $activationStateBackup)
            Remove-Item -LiteralPath $activationStateBackup -Force
        }
        else {
            [System.IO.File]::Move($activationStateTemp, $activationStatePath)
        }
    }
    finally {
        foreach ($cleanupPath in @($activationStateTemp, $activationStateBackup)) {
            if (Test-Path -LiteralPath $cleanupPath -PathType Leaf) {
                Remove-Item -LiteralPath $cleanupPath -Force
            }
        }
    }
    Write-Output "Opened the exact Unity $targetVersion Editor on the isolated D-drive activation project (PID $($process.Id))."
    Write-Output 'Unity sign-in and Personal license activation remain manual account/security steps. Close Unity before resuming batch migration.'
}

function Verify-Toolchain {
    Confirm-ComponentIntegrity
    $status = Get-ToolchainStatus
    Write-ToolchainStatus -Status $status

    if (-not $status.EditorInstalled) {
        throw "Pinned Editor verification failed: $targetEditorRoot"
    }
    if ($status.MissingModules.Count -gt 0) {
        throw "Required module verification failed: $($status.MissingModules -join ', ')"
    }
    if (-not $status.ProjectAtTarget) {
        throw "Project has not been upgraded to $targetVersion ($targetChangeset)."
    }
    if (-not $status.SourceRunnerIdentity.Ready) {
        throw "Tools\wof-unity.ps1 still resolves an incompatible Editor: $($status.SourceRunnerIdentity.Description)"
    }

    Invoke-PackageMigration -PackageAction verify -PhaseName 'final-toolchain-package-verification'
    Assert-PackageNgoState -ExpectedVersion '2.13.1' -PhaseName 'final-toolchain-package-verification'

    Write-Output 'Unity 6.3 LTS toolchain, project version, and final NGO 2.13.1 package verification passed.'
}

foreach ($controlledPath in @(
    $projectRoot,
    $unityCliRoot,
    $unityCliExecutable,
    $unityCliLocalAppData,
    $unityCliRoamingAppData,
    $unityWindowsLocalAppData,
    $unityWindowsRoamingAppData,
    $unityCliTempRoot,
    $unityCliUserProfile,
    $editorInstallRoot,
    $targetEditorRoot,
    $toolchainStateRoot,
    $toolchainLogRoot,
    $packageCacheRoot,
    $sourceBackupRoot,
    $activationProjectRoot,
    $activationStatePath,
    $automationCheckpointPath,
    $componentReceiptPath,
    $componentInstaller,
    $portableGitInitializer,
    $packageMigrationTool,
    $packageManifestPath,
    $packageLockPath
)) {
    Assert-DDrivePath -Path $controlledPath | Out-Null
}

Initialize-DOnlyEnvironment -RequirePortableGit:($Action -in @('upgrade-project', 'upgrade-ngo-patch', 'verify'))

$toolchainRunLockPath = Join-Path $toolchainStateRoot 'toolchain.run.lock'
$toolchainRunLock = $null
try {
    $toolchainRunLock = [System.IO.File]::Open(
        $toolchainRunLockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch {
    throw "Another Unity toolchain automation run is active (exclusive lock: $toolchainRunLockPath). $($_.Exception.Message)"
}

try {
    switch ($Action) {
        'plan' {
            $status = Get-ToolchainStatus
            Write-ToolchainStatus -Status $status
            $observedCliVersion = Get-ObservedUnityCliVersion
            Write-Output "Observed Unity CLI version from D-drive log: $(if ($observedCliVersion) { $observedCliVersion } else { 'not yet recorded' })"
            Write-Output 'Plan only: no direct component installer, Unity CLI, or Editor command was run.'
            Write-Output 'All controlled project, installer, cache, temporary, and log paths are pinned to D:. Windows and Unity licensing services can still use existing operating-system-managed state outside those controlled paths.'
            Write-Output 'Package migration plan:'
            if ($status.EditorInstalled) {
                Invoke-PackageMigration -PackageAction plan -PhaseName 'package-plan'
            }
            else {
                Write-Output "Skipped package-catalog planning because the exact base Editor is absent at $targetEditorRoot. Component and package mutation remain blocked until that signed Editor is installed."
            }
            Write-Output "Install command: powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' install -Apply"
            Write-Output "Baseline upgrade command: powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' upgrade-project -Apply"
            Write-Output "NGO patch command: powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' upgrade-ngo-patch -Apply"
        }
        'install' {
            Install-Toolchain
        }
        'open-target' {
            Open-TargetEditor
        }
        'upgrade-project' {
            Upgrade-Project
        }
        'upgrade-ngo-patch' {
            Upgrade-NgoPatch
        }
        'verify' {
            Verify-Toolchain
        }
    }
}
finally {
    if ($null -ne $toolchainRunLock) {
        $toolchainRunLock.Dispose()
    }
}
