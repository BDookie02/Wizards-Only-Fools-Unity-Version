[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'record-prepared-checkpoints', 'record-current-verification-checkpoints', 'prepare', 'resume', 'open-activation', 'verify')]
    [string]$Action = 'plan',

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$editorInstaller = Join-Path $projectRoot 'Tools\install-unity-editor.ps1'
$componentInstaller = Join-Path $projectRoot 'Tools\install-unity-components.ps1'
$portableGitSetup = Join-Path $projectRoot 'Tools\setup-portable-git.ps1'
$toolchainSetup = Join-Path $projectRoot 'Tools\setup-unity-toolchain.ps1'
$mcpSetup = Join-Path $projectRoot 'Tools\setup-unity-mcp.ps1'
$hubSetup = Join-Path $projectRoot 'Tools\setup-unity-hub.ps1'
$wofAutomation = Join-Path $projectRoot 'Tools\wof-unity.ps1'
$reactOracleInventory = Join-Path $projectRoot 'Tools\inventory-react-oracle.ps1'
$nunitValidator = Join-Path $projectRoot 'Tools\assert-nunit-results.ps1'
$manifestPath = Join-Path $projectRoot 'Packages\manifest.json'
$lockPath = Join-Path $projectRoot 'Packages\packages-lock.json'
$projectVersionPath = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$stateRoot = 'D:\UnityAutomationState\Wizards-Only-Fools-Unity'
$statusPath = Join-Path $stateRoot 'automation-status.json'
$checkpointPath = Join-Path $stateRoot 'automation-checkpoints.json'
$runLockPath = Join-Path $stateRoot 'automation.run.lock'

$targetEditorVersion = '6000.3.21f1'
$targetEditorChangeset = 'c02631ffc030'
$unityMcpManifestPin = 'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#c14de1e6dc01ab42d2bb358730cff954bce0ce6b'
$unityMcpExpectedCommit = 'c14de1e6dc01ab42d2bb358730cff954bce0ce6b'
$finalRegistryPins = [ordered]@{
    'com.unity.inputsystem' = '1.20.0'
    'com.unity.multiplayer.center' = '1.0.1'
    'com.unity.netcode.gameobjects' = '2.13.1'
    'com.unity.transport' = '2.7.4'
    'com.unity.render-pipelines.universal' = '17.3.0'
    'com.unity.test-framework' = '1.6.0'
    'com.unity.ugui' = '2.0.0'
}
$baselineRegistryPins = [ordered]@{
    'com.unity.inputsystem' = '1.20.0'
    'com.unity.multiplayer.center' = '1.0.1'
    'com.unity.netcode.gameobjects' = '2.13.0'
    'com.unity.transport' = '2.7.4'
    'com.unity.render-pipelines.universal' = '17.3.0'
    'com.unity.test-framework' = '1.6.0'
    'com.unity.ugui' = '2.0.0'
}

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

function Assert-ApplyRequested {
    if (-not $Apply) {
        throw "Action '$Action' changes local state. Re-run with -Apply after reviewing the plan."
    }
}

function Get-JsonDependency {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $dependencies = $Json.PSObject.Properties['dependencies']
    if ($null -eq $dependencies -or $null -eq $dependencies.Value) {
        return $null
    }
    $property = $dependencies.Value.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function Get-ProjectState {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $projectVersionText = Get-Content -LiteralPath $projectVersionPath -Raw
    $identityMatch = [regex]::Match(
        $projectVersionText,
        '(?m)^m_EditorVersionWithRevision:\s*(\S+)\s+\(([0-9A-Fa-f]+)\)\s*$')
    if (-not $identityMatch.Success) {
        throw "Could not parse exact Unity project identity from $projectVersionPath"
    }

    $manifestNgo = [string](Get-JsonDependency -Json $manifest -Name 'com.unity.netcode.gameobjects')
    $lockNgoEntry = Get-JsonDependency -Json $lock -Name 'com.unity.netcode.gameobjects'
    $lockNgo = if ($null -ne $lockNgoEntry) { [string]$lockNgoEntry.version } else { $null }

    return [PSCustomObject]@{
        Manifest = $manifest
        Lock = $lock
        EditorVersion = $identityMatch.Groups[1].Value
        EditorChangeset = $identityMatch.Groups[2].Value
        AtTargetEditor = (
            $identityMatch.Groups[1].Value -eq $targetEditorVersion -and
            $identityMatch.Groups[2].Value -eq $targetEditorChangeset)
        ManifestNgo = $manifestNgo
        LockNgo = $lockNgo
    }
}

function Get-FinalProjectProblems {
    $state = Get-ProjectState
    $problems = New-Object System.Collections.Generic.List[string]

    if (-not $state.AtTargetEditor) {
        $problems.Add("Project identity is $($state.EditorVersion) ($($state.EditorChangeset)); expected $targetEditorVersion ($targetEditorChangeset).")
    }

    foreach ($entry in $finalRegistryPins.GetEnumerator()) {
        $manifestValue = [string](Get-JsonDependency -Json $state.Manifest -Name $entry.Key)
        $lockEntry = Get-JsonDependency -Json $state.Lock -Name $entry.Key
        $lockValue = if ($null -ne $lockEntry) { [string]$lockEntry.version } else { $null }
        if ($manifestValue -ne $entry.Value) {
            $problems.Add("Manifest $($entry.Key) is '$manifestValue'; expected '$($entry.Value)'.")
        }
        if ($lockValue -ne $entry.Value) {
            $problems.Add("Lock $($entry.Key) is '$lockValue'; expected '$($entry.Value)'.")
        }
    }

    $mcpManifest = [string](Get-JsonDependency -Json $state.Manifest -Name 'com.coplaydev.unity-mcp')
    if ($mcpManifest -ne $unityMcpManifestPin) {
        $problems.Add("Unity MCP manifest pin is '$mcpManifest'; expected '$unityMcpManifestPin'.")
    }

    $mcpLock = Get-JsonDependency -Json $state.Lock -Name 'com.coplaydev.unity-mcp'
    if ($null -eq $mcpLock) {
        $problems.Add('Unity MCP is absent from packages-lock.json; Unity has not resolved the pinned Git package.')
    }
    else {
        $mcpLockVersion = [string]$mcpLock.version
        $mcpLockSource = [string]$mcpLock.source
        $mcpLockHash = [string]$mcpLock.hash
        $mcpLockDepthProperty = $mcpLock.PSObject.Properties['depth']
        if ($mcpLockVersion -ne $unityMcpManifestPin) {
            $problems.Add("Unity MCP lock version is '$mcpLockVersion'; expected the exact tagged Git URL.")
        }
        if ($mcpLockSource -ne 'git') {
            $problems.Add("Unity MCP lock source is '$mcpLockSource'; expected 'git'.")
        }
        if ($null -eq $mcpLockDepthProperty -or [int]$mcpLockDepthProperty.Value -ne 0) {
            $problems.Add('Unity MCP lock entry must be a direct depth-0 project dependency.')
        }
        if (-not [string]::Equals(
            $mcpLockHash,
            $unityMcpExpectedCommit,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            $problems.Add("Unity MCP lock hash is '$mcpLockHash'; expected v10.1.0 commit '$unityMcpExpectedCommit'.")
        }
    }

    return $problems.ToArray()
}

function Get-BaselineProjectProblems {
    $state = Get-ProjectState
    $problems = New-Object System.Collections.Generic.List[string]

    if (-not $state.AtTargetEditor) {
        $problems.Add("Project identity is $($state.EditorVersion) ($($state.EditorChangeset)); expected $targetEditorVersion ($targetEditorChangeset).")
    }
    foreach ($entry in $baselineRegistryPins.GetEnumerator()) {
        $manifestValue = [string](Get-JsonDependency -Json $state.Manifest -Name $entry.Key)
        $lockEntry = Get-JsonDependency -Json $state.Lock -Name $entry.Key
        $lockValue = if ($null -ne $lockEntry) { [string]$lockEntry.version } else { $null }
        if ($manifestValue -ne $entry.Value) {
            $problems.Add("Baseline manifest $($entry.Key) is '$manifestValue'; expected '$($entry.Value)'.")
        }
        if ($lockValue -ne $entry.Value) {
            $problems.Add("Baseline lock $($entry.Key) is '$lockValue'; expected '$($entry.Value)'.")
        }
    }

    return $problems.ToArray()
}

function Write-StatusAtomic {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Status)

    New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
    $json = ($Status | ConvertTo-Json -Depth 8) + [Environment]::NewLine
    $tempPath = Join-Path $stateRoot ("automation-status.$([Guid]::NewGuid().ToString('N')).tmp")
    $backupPath = Join-Path $stateRoot ("automation-status.$([Guid]::NewGuid().ToString('N')).bak")
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    try {
        [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        Get-Content -LiteralPath $tempPath -Raw | ConvertFrom-Json | Out-Null
        if (Test-Path -LiteralPath $statusPath -PathType Leaf) {
            [System.IO.File]::Replace($tempPath, $statusPath, $backupPath)
            Remove-Item -LiteralPath $backupPath -Force
        }
        else {
            [System.IO.File]::Move($tempPath, $statusPath)
        }
    }
    finally {
        foreach ($cleanupPath in @($tempPath, $backupPath)) {
            if (Test-Path -LiteralPath $cleanupPath -PathType Leaf) {
                Remove-Item -LiteralPath $cleanupPath -Force
            }
        }
    }
}

function Get-CheckpointSpec {
    param([Parameter(Mandatory = $true)][string]$Name)

    switch ($Name) {
        'editor' {
            return [PSCustomObject]@{
                EvidenceFiles = @('D:\UnityEditors\6000.3.21f1\Editor\Unity.exe')
                RequiredPaths = @(
                    'D:\UnityEditors\6000.3.21f1\Editor\Unity.exe',
                    'D:\UnityEditors\6000.3.21f1\Editor\Unity.com'
                )
            }
        }
        'portable-git' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\UnityTools\GitForWindows\2.55.0.3\.wof-portable-git-receipt.json',
                    'D:\UnityTools\GitForWindows\2.55.0.3\cmd\git.exe'
                )
                RequiredPaths = @('D:\UnityTools\GitForWindows\2.55.0.3\cmd\git.exe')
            }
        }
        'platform-components' {
            return [PSCustomObject]@{
                EvidenceFiles = @('D:\UnityInstallers\6000.3.21f1\component-install-receipts.json')
                RequiredPaths = @(
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\modules.asset',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\WebGLSupport\BuildTools',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_development_il2cpp',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_server_development_il2cpp',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\java.exe',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\ndk-build.cmd',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt2.exe',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-34\android.jar',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-35\android.jar',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-36\android.jar',
                    'D:\UnityEditors\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\16.0\bin\sdkmanager.bat'
                )
            }
        }
        'codex-unity-mcp' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\UnityMCPToolchain\receipts\unity-mcp-10.1.0.json',
                    'D:\UnityMCPToolchain\tool-bin\mcp-for-unity.exe',
                    'D:\UnityMCPToolchain\tool-bin\unity-mcp.exe'
                )
                RequiredPaths = @(
                    'D:\UnityMCPToolchain\tools\mcpforunityserver',
                    'D:\UnityMCPToolchain\python\cpython-3.14.7-windows-x86_64-none\python.exe'
                )
            }
        }
        'unity-hub' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\UnityAutomationState\Wizards-Only-Fools-Unity\unity-hub-receipt.json',
                    'D:\UnityHub\Unity Hub.exe'
                )
                RequiredPaths = @(
                    'D:\UnityHub\Unity Hub.exe',
                    'D:\UnityHubProfile\UserData'
                )
            }
        }
        'project-bootstrap' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\bootstrap.log',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Assets\WOF\Generated\Scenes\WofBootstrap.unity',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Assets\WOF\Generated\Prefabs\WofNetworkPlayer.prefab',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Assets\WOF\Generated\Prefabs\WofFireball.prefab'
                )
                RequiredPaths = @('D:\CodexProjects\Wizards-Only-Fools-Unity\Assets\WOF\Generated\Settings\WofNetworkPrefabs.asset')
            }
        }
        'editmode-tests' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\editmode-tests.log',
                    'D:\tmp\wof-unity\logs\editmode-results.xml'
                )
                RequiredPaths = @('D:\tmp\wof-unity\logs\editmode-results.xml')
            }
        }
        'windows-build' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\build-windows.log',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows\WizardsOnlyFools.exe',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows\WizardsOnlyFools.exe.build.json'
                )
                RequiredPaths = @('D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows\WizardsOnlyFools_Data')
            }
        }
        'windows-lan-smoke' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\smoke-host.log',
                    'D:\tmp\wof-unity\logs\smoke-client.log'
                )
                RequiredPaths = @('D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows\WizardsOnlyFools.exe')
            }
        }
        'webgl-build' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\build-webgl.log',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\WebGL\index.html',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\WebGL\WofBuildReceipt.json'
                )
                RequiredPaths = @('D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\WebGL\Build')
            }
        }
        'android-build' {
            return [PSCustomObject]@{
                EvidenceFiles = @(
                    'D:\tmp\wof-unity\logs\build-android.log',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Android\WizardsOnlyFools.apk',
                    'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Android\WizardsOnlyFools.apk.build.json'
                )
                RequiredPaths = @('D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Android\WizardsOnlyFools.apk')
            }
        }
        default {
            throw "No durable checkpoint specification exists for stage '$Name'."
        }
    }
}

function Read-CheckpointDocument {
    if (-not (Test-Path -LiteralPath $checkpointPath -PathType Leaf)) {
        return [PSCustomObject]@{
            schemaVersion = 1
            projectRoot = $projectRoot
            stages = [PSCustomObject]@{}
        }
    }
    $document = Get-Content -LiteralPath $checkpointPath -Raw | ConvertFrom-Json
    if (($document.schemaVersion -ne 1) -or
        (-not [string]::Equals([string]$document.projectRoot, $projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) -or
        $null -eq $document.PSObject.Properties['stages']) {
        throw "Automation checkpoint identity is invalid: $checkpointPath"
    }
    return $document
}

function Write-CheckpointDocumentAtomic {
    param([Parameter(Mandatory = $true)]$Document)

    New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
    $tempPath = Join-Path $stateRoot ("automation-checkpoints.$([Guid]::NewGuid().ToString('N')).tmp")
    $backupPath = Join-Path $stateRoot ("automation-checkpoints.$([Guid]::NewGuid().ToString('N')).bak")
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    try {
        [System.IO.File]::WriteAllText($tempPath, ($Document | ConvertTo-Json -Depth 10) + [Environment]::NewLine, $utf8NoBom)
        Get-Content -LiteralPath $tempPath -Raw | ConvertFrom-Json | Out-Null
        if (Test-Path -LiteralPath $checkpointPath -PathType Leaf) {
            [System.IO.File]::Replace($tempPath, $checkpointPath, $backupPath)
            Remove-Item -LiteralPath $backupPath -Force
        }
        else {
            [System.IO.File]::Move($tempPath, $checkpointPath)
        }
    }
    finally {
        foreach ($cleanupPath in @($tempPath, $backupPath)) {
            if (Test-Path -LiteralPath $cleanupPath -PathType Leaf) {
                Remove-Item -LiteralPath $cleanupPath -Force
            }
        }
    }
}

function Get-StableFileSha256 {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutSeconds = 60
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        $stream = $null
        $algorithm = $null
        try {
            $before = Get-Item -LiteralPath $Path
            $stream = [System.IO.File]::Open(
                $Path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
            $algorithm = [System.Security.Cryptography.SHA256]::Create()
            $hashBytes = $algorithm.ComputeHash($stream)
            $hash = ([System.BitConverter]::ToString($hashBytes)).Replace('-', '').ToLowerInvariant()
            $stream.Dispose()
            $stream = $null
            $after = Get-Item -LiteralPath $Path
            if ($before.Length -eq $after.Length -and $before.LastWriteTimeUtc -eq $after.LastWriteTimeUtc) {
                return $hash
            }
            $lastError = "File changed while hashing."
        }
        catch {
            $lastError = $_.Exception.Message
        }
        finally {
            if ($null -ne $algorithm) { $algorithm.Dispose() }
            if ($null -ne $stream) { $stream.Dispose() }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Could not obtain a stable SHA-256 for $Path within $TimeoutSeconds seconds. $lastError"
}

function Get-StageInputFingerprint {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($Name -notin @(
        'project-bootstrap',
        'editmode-tests',
        'windows-build',
        'windows-lan-smoke',
        'webgl-build',
        'android-build')) {
        return $null
    }

    $inputFiles = @()
    foreach ($relativeRoot in @(
        'Assets\WOF\Runtime',
        'Assets\WOF\Editor',
        'Assets\WOF\Tests',
        'Assets\WOF\Art',
        'Packages',
        'ProjectSettings')) {
        $root = Assert-DDrivePath -Path (Join-Path $projectRoot $relativeRoot)
        if (Test-Path -LiteralPath $root -PathType Container) {
            $inputFiles += Get-ChildItem -LiteralPath $root -Recurse -File
        }
    }
    foreach ($relativePath in @(
        'Tools\wof-unity.ps1',
        'Tools\assert-nunit-results.ps1')) {
        $path = Assert-DDrivePath -Path (Join-Path $projectRoot $relativePath)
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $inputFiles += Get-Item -LiteralPath $path
        }
    }

    $builder = New-Object System.Text.StringBuilder
    foreach ($file in @($inputFiles | Sort-Object FullName -Unique)) {
        $relativePath = $file.FullName.Substring($projectRoot.Length).TrimStart('\').Replace('\', '/')
        [void]$builder.Append($relativePath)
        [void]$builder.Append("`n")
        [void]$builder.Append((Get-StableFileSha256 -Path $file.FullName))
        [void]$builder.Append("`n")
    }

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Save-StageCheckpoint {
    param([Parameter(Mandatory = $true)][string]$Name)

    $spec = Get-CheckpointSpec -Name $Name
    $evidence = @()
    foreach ($path in @($spec.EvidenceFiles)) {
        $fullPath = Assert-DDrivePath -Path $path
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Cannot checkpoint '$Name'; evidence file is missing: $fullPath"
        }
        $evidence += [PSCustomObject]@{
            path = $fullPath
            sha256 = Get-StableFileSha256 -Path $fullPath
        }
    }
    foreach ($path in @($spec.RequiredPaths)) {
        $fullPath = Assert-DDrivePath -Path $path
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "Cannot checkpoint '$Name'; required path is missing: $fullPath"
        }
    }

    $document = Read-CheckpointDocument
    $record = [PSCustomObject]@{
        completedUtc = [DateTime]::UtcNow.ToString('o')
        inputFingerprint = Get-StageInputFingerprint -Name $Name
        evidence = $evidence
        requiredPaths = @($spec.RequiredPaths)
    }
    $document.stages | Add-Member -NotePropertyName $Name -NotePropertyValue $record -Force
    Write-CheckpointDocumentAtomic -Document $document
}

function Test-StageCheckpoint {
    param([Parameter(Mandatory = $true)][string]$Name)

    try {
        $document = Read-CheckpointDocument
        $stageProperty = $document.stages.PSObject.Properties[$Name]
        if ($null -eq $stageProperty -or $null -eq $stageProperty.Value) {
            return $false
        }
        $record = $stageProperty.Value
        $spec = Get-CheckpointSpec -Name $Name
        $expectedInputFingerprint = Get-StageInputFingerprint -Name $Name
        $savedInputFingerprintProperty = $record.PSObject.Properties['inputFingerprint']
        if ($null -ne $expectedInputFingerprint -and
            ($null -eq $savedInputFingerprintProperty -or
             -not [string]::Equals(
                 [string]$savedInputFingerprintProperty.Value,
                 [string]$expectedInputFingerprint,
                 [System.StringComparison]::OrdinalIgnoreCase))) {
            return $false
        }
        if (@($record.evidence).Count -ne @($spec.EvidenceFiles).Count -or
            @($record.requiredPaths).Count -ne @($spec.RequiredPaths).Count) {
            return $false
        }
        foreach ($path in @($spec.EvidenceFiles)) {
            $fullPath = Assert-DDrivePath -Path $path
            $saved = @($record.evidence | Where-Object {
                [string]::Equals([string]$_.path, $fullPath, [System.StringComparison]::OrdinalIgnoreCase)
            }) | Select-Object -First 1
            if ($null -eq $saved -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf) -or
                -not [string]::Equals([string]$saved.sha256, (Get-StableFileSha256 -Path $fullPath), [System.StringComparison]::OrdinalIgnoreCase)) {
                return $false
            }
        }
        foreach ($path in @($spec.RequiredPaths)) {
            if (-not (Test-Path -LiteralPath (Assert-DDrivePath -Path $path))) {
                return $false
            }
        }
        return $true
    }
    catch {
        return $false
    }
}

function Get-FailureBoundary {
    param([Parameter(Mandatory = $true)][string]$Message)

    if ($Message -match '(?i)(exit\s+198|no valid unity.*licen|licen[cs]e.*activat|licen[cs]ing client)') {
        return [PSCustomObject]@{
            Status = 'waiting-for-unity-activation'
            NextAction = "Run '$PSCommandPath open-activation -Apply', complete Unity Hub sign-in/Personal activation in the normal browser, then rerun '$PSCommandPath resume -Apply'."
        }
    }
    if ($Message -match '(?i)(UAC was declined|elevation was declined|operation was canceled by the user|native error\s*1223|requires.*UAC)') {
        return [PSCustomObject]@{
            Status = 'waiting-for-uac-approval'
            NextAction = "Rerun '$PSCommandPath resume -Apply' and approve the single signed Unity component elevation prompt."
        }
    }
    if ($Message -match '(?i)(Unity project.*open|UnityLockfile|project is already open)') {
        return [PSCustomObject]@{
            Status = 'waiting-for-unity-to-close'
            NextAction = "Close the Unity Editor, then rerun '$PSCommandPath resume -Apply'."
        }
    }
    return [PSCustomObject]@{
        Status = 'failed'
        NextAction = 'Inspect the recorded error and D-drive log named in it; rerunning resume is safe after the cause is corrected.'
    }
}

$script:RunStatus = [ordered]@{
    schemaVersion = 1
    projectRoot = $projectRoot
    action = $Action
    status = 'starting'
    currentStage = $null
    startedUtc = [DateTime]::UtcNow.ToString('o')
    updatedUtc = [DateTime]::UtcNow.ToString('o')
    completedStages = @()
    error = $null
    nextAction = $null
}
$script:McpRuntimeVerifiedThisRun = $false
$script:PortableGitVerifiedThisRun = $false

function Save-RunStatus {
    $script:RunStatus.updatedUtc = [DateTime]::UtcNow.ToString('o')
    Write-StatusAtomic -Status $script:RunStatus
}

function Invoke-AutomationStage {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Operation,
        [switch]$UseCheckpoint
    )

    if ($UseCheckpoint -and (Test-StageCheckpoint -Name $Name)) {
        $script:RunStatus.completedStages = @($script:RunStatus.completedStages | Where-Object { $_ -ne $Name }) + $Name
        Save-RunStatus
        Write-Output "AUTOMATION_STAGE_SKIPPED $Name (durable D-drive checkpoint matches)"
        return
    }

    $script:RunStatus.status = 'running'
    $script:RunStatus.currentStage = $Name
    $script:RunStatus.error = $null
    $script:RunStatus.nextAction = $null
    Save-RunStatus
    Write-Output "AUTOMATION_STAGE_STARTED $Name"
    try {
        & $Operation
        $script:RunStatus.completedStages = @($script:RunStatus.completedStages | Where-Object { $_ -ne $Name }) + $Name
        if ($UseCheckpoint) {
            Save-StageCheckpoint -Name $Name
        }
        Save-RunStatus
        Write-Output "AUTOMATION_STAGE_COMPLETED $Name"
    }
    catch {
        $script:RunStatus.completedStages = @($script:RunStatus.completedStages | Where-Object { $_ -ne $Name })
        $boundary = Get-FailureBoundary -Message $_.Exception.Message
        $script:RunStatus.status = $boundary.Status
        $script:RunStatus.error = $_.Exception.Message
        $script:RunStatus.nextAction = $boundary.NextAction
        Save-RunStatus
        throw "Unity recreation automation stopped at '$Name'. $($boundary.NextAction)`n$($_.Exception.Message)"
    }
}

function Invoke-Prepare {
    Invoke-AutomationStage -Name 'react-oracle-verification' -Operation {
        & $reactOracleInventory verify
    }
    Invoke-AutomationStage -Name 'editor' -UseCheckpoint -Operation {
        & $editorInstaller install -Apply
    }
    Invoke-AutomationStage -Name 'portable-git' -UseCheckpoint -Operation {
        & $portableGitSetup install -Apply
    }
    $script:PortableGitVerifiedThisRun = $true
    Invoke-AutomationStage -Name 'platform-components' -UseCheckpoint -Operation {
        & $toolchainSetup install -Apply
    }
    Invoke-AutomationStage -Name 'codex-unity-mcp' -UseCheckpoint -Operation {
        try {
            & $mcpSetup verify
        }
        catch {
            Write-Output 'The D-drive MCP runtime is absent or incomplete; installing the pinned toolchain now.'
            & $mcpSetup install
        }
    }
    $script:McpRuntimeVerifiedThisRun = $true
    Invoke-AutomationStage -Name 'unity-hub' -UseCheckpoint -Operation {
        & $hubSetup verify
    }
}

function Invoke-PackageMigration {
    $state = Get-ProjectState
    if ($state.ManifestNgo -eq '2.13.0' -and @(Get-BaselineProjectProblems).Count -gt 0) {
        Invoke-AutomationStage -Name 'unity-6000.3-baseline' -Operation {
            & $toolchainSetup upgrade-project -Apply -UseVerifiedComponentCheckpoint
        }
        $state = Get-ProjectState
    }
    elseif ($state.ManifestNgo -eq '2.13.1' -and -not $state.AtTargetEditor) {
        throw "Inconsistent migration state: project is still $($state.EditorVersion), but manifest NGO is already 2.13.1. Refusing an automatic downgrade."
    }
    elseif ($state.ManifestNgo -notin @('2.13.0', '2.13.1')) {
        Invoke-AutomationStage -Name 'unity-6000.3-baseline' -Operation {
            & $toolchainSetup upgrade-project -Apply -UseVerifiedComponentCheckpoint
        }
        $state = Get-ProjectState
    }
    else {
        Write-Output 'AUTOMATION_STAGE_SKIPPED unity-6000.3-baseline (resolved baseline or final package state already matches)'
    }

    $prePatchProblems = @(Get-FinalProjectProblems)
    if ($prePatchProblems.Count -gt 0) {
        Invoke-AutomationStage -Name 'ngo-2.13.1-isolated-patch' -Operation {
            & $toolchainSetup upgrade-ngo-patch -Apply -UseVerifiedComponentCheckpoint
        }
    }
    else {
        Write-Output 'AUTOMATION_STAGE_SKIPPED ngo-2.13.1-isolated-patch (final project state already matches)'
    }
}

function Invoke-FinalVerification {
    if (Test-StageCheckpoint -Name 'editor') {
        Write-Output 'AUTOMATION_STAGE_SKIPPED editor-integrity-verification (durable Editor checkpoint matches)'
    }
    else {
        Invoke-AutomationStage -Name 'editor-integrity-verification' -Operation {
            & $editorInstaller verify
        }
    }
    if ($script:PortableGitVerifiedThisRun) {
        Write-Output 'AUTOMATION_STAGE_SKIPPED portable-git-verification (verified earlier in this run)'
    }
    else {
        Invoke-AutomationStage -Name 'portable-git-verification' -Operation {
            & $portableGitSetup verify
            $script:PortableGitVerifiedThisRun = $true
        }
    }
    if (Test-StageCheckpoint -Name 'platform-components') {
        Write-Output 'AUTOMATION_STAGE_SKIPPED toolchain-verification (durable component checkpoint matches)'
    }
    else {
        Invoke-AutomationStage -Name 'toolchain-verification' -Operation {
            & $toolchainSetup verify
        }
    }
    if ($script:McpRuntimeVerifiedThisRun) {
        Write-Output 'AUTOMATION_STAGE_SKIPPED mcp-runtime-verification (verified earlier in this run)'
    }
    else {
        Invoke-AutomationStage -Name 'mcp-runtime-verification' -Operation {
            & $mcpSetup verify
            $script:McpRuntimeVerifiedThisRun = $true
        }
    }
    Invoke-AutomationStage -Name 'resolved-project-verification' -Operation {
        $problems = @(Get-FinalProjectProblems)
        if ($problems.Count -gt 0) {
            throw "Final Unity project verification failed:`n - $($problems -join "`n - ")"
        }
        Write-Output 'Exact Unity project identity, registry package locks, and immutable Unity MCP Git lock verification passed.'
    }
    Invoke-AutomationStage -Name 'unity-hub-license-refresh' -Operation {
        & $hubSetup refresh-license -Apply
    }
    Invoke-AutomationStage -Name 'project-bootstrap' -UseCheckpoint -Operation {
        & $wofAutomation bootstrap
    }
    Invoke-AutomationStage -Name 'editmode-tests' -UseCheckpoint -Operation {
        & $wofAutomation test
    }
    Invoke-AutomationStage -Name 'windows-build' -UseCheckpoint -Operation {
        & $wofAutomation build-windows
    }
    Invoke-AutomationStage -Name 'windows-lan-smoke' -UseCheckpoint -Operation {
        & $wofAutomation smoke-windows
    }
    Invoke-AutomationStage -Name 'webgl-build' -UseCheckpoint -Operation {
        & $wofAutomation build-webgl
    }
    Invoke-AutomationStage -Name 'android-build' -UseCheckpoint -Operation {
        & $wofAutomation build-android
    }
}

foreach ($controlledPath in @(
    $projectRoot,
    $editorInstaller,
    $componentInstaller,
    $portableGitSetup,
    $toolchainSetup,
    $mcpSetup,
    $hubSetup,
    $wofAutomation,
    $reactOracleInventory,
    $nunitValidator,
    $manifestPath,
    $lockPath,
    $projectVersionPath,
    $stateRoot,
    $statusPath,
    $checkpointPath,
    $runLockPath
)) {
    Assert-DDrivePath -Path $controlledPath | Out-Null
}

foreach ($requiredScript in @($editorInstaller, $componentInstaller, $portableGitSetup, $toolchainSetup, $mcpSetup, $hubSetup, $wofAutomation, $reactOracleInventory, $nunitValidator)) {
    if (-not (Test-Path -LiteralPath $requiredScript -PathType Leaf)) {
        throw "Required automation script is missing: $requiredScript"
    }
}

if ($Action -eq 'plan') {
    $problems = @(Get-FinalProjectProblems)
    [ordered]@{
        action = 'plan'
        mutationPerformed = $false
        projectRoot = $projectRoot
        controlledStateRoot = $stateRoot
        pipeline = @(
            'verify the complete hash-backed React/Node playable oracle and clean GitHub history oracle on D:',
            'verify/install signed Unity 6000.3.21f1 Editor on D:',
            'verify/install official PortableGit on D: and isolate Unity Package Manager from C-installed Git',
            'verify/install Unity Windows, WebGL, server, and Android modules on D: (one UAC approval if missing)',
            'verify/install pinned stdio Unity MCP runtime on D:',
            'verify the signed Unity Hub installation and D-profile browser callback on D:',
            'resolve the Unity 6000.3 package baseline',
            'apply and resolve the isolated NGO 2.13.1 patch',
            'verify exact toolchain/package/Git lock state',
            'bootstrap, compile, test, build Windows/WebGL/Android, and run the two-process LAN smoke probe'
        )
        currentFinalStateReady = ($problems.Count -eq 0)
        currentProblems = $problems
        manualBoundaries = @(
            'Approve the signed Unity platform-module installers if Windows displays UAC.',
            'Sign in and activate Unity Personal through Unity Hub if no valid license exists.',
            'Physically inspect and interact with desktop and mobile/WebGL builds before parity is declared complete.'
        )
        runCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' resume -Apply"
    } | ConvertTo-Json -Depth 8
    return
}

Assert-ApplyRequested
New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
$runLock = $null
try {
    $runLock = [System.IO.File]::Open(
        $runLockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch {
    throw "Another Unity recreation automation run is active (exclusive lock: $runLockPath). $($_.Exception.Message)"
}

try {
    Save-RunStatus
    switch ($Action) {
        'record-prepared-checkpoints' {
            foreach ($stageName in @('editor', 'portable-git', 'platform-components', 'codex-unity-mcp')) {
                Save-StageCheckpoint -Name $stageName
                $script:RunStatus.completedStages = @($script:RunStatus.completedStages | Where-Object { $_ -ne $stageName }) + $stageName
                Write-Output "AUTOMATION_CHECKPOINT_RECORDED $stageName"
            }
            $script:RunStatus.status = 'prepared-checkpoints-recorded'
            $script:RunStatus.currentStage = $null
            $script:RunStatus.nextAction = "Run '$PSCommandPath resume -Apply'."
        }
        'record-current-verification-checkpoints' {
            $bootstrapLog = 'D:\tmp\wof-unity\logs\bootstrap.log'
            if (-not (Test-Path -LiteralPath $bootstrapLog -PathType Leaf) -or
                -not (Select-String -LiteralPath $bootstrapLog -Pattern '\[WOF-AUTOMATION\] BOOTSTRAP_COMPLETE' -Quiet)) {
                throw "The current bootstrap log does not contain its completion marker: $bootstrapLog"
            }
            & $nunitValidator -Path 'D:\tmp\wof-unity\logs\editmode-results.xml'
            & $wofAutomation validate-windows
            $stageNames = @('project-bootstrap', 'editmode-tests', 'windows-build')
            $smokeHostLog = 'D:\tmp\wof-unity\logs\smoke-host.log'
            $smokeClientLog = 'D:\tmp\wof-unity\logs\smoke-client.log'
            if ((Test-Path -LiteralPath $smokeHostLog -PathType Leaf) -and
                (Test-Path -LiteralPath $smokeClientLog -PathType Leaf) -and
                (Select-String -LiteralPath $smokeHostLog -Pattern '\[WOF-AUTOMATION\] CLIENT_RPC_SERVER_PATH_PASSED' -Quiet) -and
                (Select-String -LiteralPath $smokeClientLog -Pattern '\[WOF-AUTOMATION\] CLIENT_REPLICATION_PROBE_PASSED' -Quiet) -and
                -not (Select-String -LiteralPath @($smokeHostLog, $smokeClientLog) -Pattern '\[WOF-AUTOMATION\].*PROBE_FAILED' -Quiet)) {
                $stageNames += 'windows-lan-smoke'
            }
            if (Test-Path -LiteralPath 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\WebGL\index.html' -PathType Leaf) {
                & $wofAutomation validate-webgl
                $stageNames += 'webgl-build'
            }
            if (Test-Path -LiteralPath 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Android\WizardsOnlyFools.apk' -PathType Leaf) {
                & $wofAutomation validate-android
                $stageNames += 'android-build'
            }
            foreach ($stageName in $stageNames) {
                Save-StageCheckpoint -Name $stageName
                $script:RunStatus.completedStages = @($script:RunStatus.completedStages | Where-Object { $_ -ne $stageName }) + $stageName
                Write-Output "AUTOMATION_CHECKPOINT_RECORDED $stageName"
            }
            $script:RunStatus.status = 'verification-checkpoints-recorded'
            $script:RunStatus.currentStage = $null
            $script:RunStatus.nextAction = "Run '$PSCommandPath resume -Apply'."
        }
        'prepare' {
            Invoke-Prepare
            $script:RunStatus.status = 'prepared'
            $script:RunStatus.currentStage = $null
            $script:RunStatus.nextAction = "Run '$PSCommandPath resume -Apply'."
        }
        'resume' {
            Invoke-Prepare
            Invoke-PackageMigration
            Invoke-FinalVerification
            $script:RunStatus.status = 'complete'
            $script:RunStatus.currentStage = $null
            $script:RunStatus.nextAction = 'Perform the required physical desktop and mobile/WebGL interaction pass before declaring this recreation slice complete.'
        }
        'open-activation' {
            Invoke-AutomationStage -Name 'unity-hub-activation' -Operation {
                & $hubSetup open -Apply
            }
            $script:RunStatus.status = 'waiting-for-unity-activation'
            $script:RunStatus.currentStage = 'unity-hub-activation'
            $script:RunStatus.nextAction = "Complete sign-in and Personal activation in Unity Hub using the normal browser, then run '$PSCommandPath resume -Apply'."
        }
        'verify' {
            Invoke-FinalVerification
            $script:RunStatus.status = 'verified'
            $script:RunStatus.currentStage = $null
            $script:RunStatus.nextAction = 'Perform the required physical desktop and mobile/WebGL interaction pass.'
        }
    }
    Save-RunStatus
    Write-Output "Unity recreation automation action '$Action' finished with status '$($script:RunStatus.status)'. Status: $statusPath"
}
catch {
    if ($script:RunStatus.status -in @('starting', 'running')) {
        $boundary = Get-FailureBoundary -Message $_.Exception.Message
        $script:RunStatus.status = $boundary.Status
        $script:RunStatus.error = $_.Exception.Message
        $script:RunStatus.nextAction = $boundary.NextAction
        Save-RunStatus
    }
    throw
}
finally {
    if ($null -ne $runLock) {
        $runLock.Dispose()
    }
}
