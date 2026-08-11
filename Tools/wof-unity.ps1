param(
    [Parameter(Position = 0)]
    [ValidateSet('bootstrap', 'test', 'build-windows', 'validate-windows', 'build-webgl', 'validate-webgl', 'build-android', 'validate-android', 'verify', 'rebuild-all', 'smoke-windows', 'open')]
    [string]$Action = 'bootstrap'
)

$ErrorActionPreference = 'Stop'
$projectPath = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$requiredEditorVersion = '6000.3.21f1'
$requiredEditorChangeset = 'c02631ffc030'
$projectVersionPath = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
    throw "Unity project version file not found at $projectVersionPath"
}

$projectVersionLine = Get-Content -LiteralPath $projectVersionPath |
    Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)\s*$' } |
    Select-Object -First 1
if (-not $projectVersionLine) {
    throw "Could not read m_EditorVersion from $projectVersionPath"
}

$projectEditorVersion = ([regex]::Match($projectVersionLine, '^m_EditorVersion:\s*(\S+)\s*$')).Groups[1].Value
$projectRevisionLine = Get-Content -LiteralPath $projectVersionPath |
    Where-Object { $_ -match '^m_EditorVersionWithRevision:\s*(\S+)\s+\(([0-9A-Fa-f]+)\)\s*$' } |
    Select-Object -First 1
if (-not $projectRevisionLine) {
    throw "Could not read m_EditorVersionWithRevision from $projectVersionPath"
}

$projectRevisionMatch = [regex]::Match(
    $projectRevisionLine,
    '^m_EditorVersionWithRevision:\s*(\S+)\s+\(([0-9A-Fa-f]+)\)\s*$')
$projectRevisionVersion = $projectRevisionMatch.Groups[1].Value
$projectEditorChangeset = $projectRevisionMatch.Groups[2].Value
if ($projectEditorVersion -ne $requiredEditorVersion -or
    $projectRevisionVersion -ne $requiredEditorVersion -or
    $projectEditorChangeset -ne $requiredEditorChangeset) {
    throw "Unity project migration is incomplete. Refusing to run with $projectEditorVersion ($projectEditorChangeset); expected $requiredEditorVersion ($requiredEditorChangeset). Complete Tools\setup-unity-toolchain.ps1 upgrade-project and upgrade-ngo-patch first."
}

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$JsonPath,
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [switch]$LockFile
    )

    if (-not (Test-Path -LiteralPath $JsonPath -PathType Leaf)) {
        throw "Package file not found: $JsonPath"
    }

    try {
        $json = Get-Content -LiteralPath $JsonPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Invalid package JSON at ${JsonPath}: $($_.Exception.Message)"
    }

    $dependenciesProperty = $json.PSObject.Properties['dependencies']
    if ($null -eq $dependenciesProperty -or $null -eq $dependenciesProperty.Value) {
        throw "Package JSON has no dependencies object: $JsonPath"
    }

    $packageProperty = $dependenciesProperty.Value.PSObject.Properties[$PackageName]
    if ($null -eq $packageProperty -or $null -eq $packageProperty.Value) {
        throw "Package JSON has no $PackageName entry: $JsonPath"
    }

    if (-not $LockFile) {
        return [string]$packageProperty.Value
    }

    $versionProperty = $packageProperty.Value.PSObject.Properties['version']
    if ($null -eq $versionProperty -or [string]::IsNullOrWhiteSpace([string]$versionProperty.Value)) {
        throw "Package lock entry has no version for ${PackageName}: $JsonPath"
    }

    return [string]$versionProperty.Value
}

$manifestPath = Join-Path $projectPath 'Packages\manifest.json'
$packageLockPath = Join-Path $projectPath 'Packages\packages-lock.json'
$requiredResolvedPackages = [ordered]@{
    'com.unity.inputsystem' = '1.20.0'
    'com.unity.multiplayer.center' = '1.0.1'
    'com.unity.netcode.gameobjects' = '2.13.1'
    'com.unity.transport' = '2.7.4'
    'com.unity.render-pipelines.universal' = '17.3.0'
    'com.unity.test-framework' = '1.6.0'
    'com.unity.ugui' = '2.0.0'
}
foreach ($entry in $requiredResolvedPackages.GetEnumerator()) {
    $manifestVersion = Get-PackageVersion -JsonPath $manifestPath -PackageName $entry.Key
    $lockVersion = Get-PackageVersion -JsonPath $packageLockPath -PackageName $entry.Key -LockFile
    if ($manifestVersion -ne $entry.Value -or $lockVersion -ne $entry.Value) {
        throw "Resolved package migration is incomplete for $($entry.Key). Manifest=$manifestVersion Lock=$lockVersion Expected=$($entry.Value). Complete Tools\setup-unity-toolchain.ps1 upgrade-project and upgrade-ngo-patch first."
    }
}

$unityRootCandidates = @(
    "D:\UnityEditors\$projectEditorVersion\Editor",
    "D:\Program Files\Unity\Hub\Editor\$projectEditorVersion\Editor"
)
$unityRoot = $unityRootCandidates |
    Where-Object {
        $candidateEditor = Join-Path $_ 'Unity.exe'
        $candidateConsole = Join-Path $_ 'Unity.com'
        if (-not ((Test-Path -LiteralPath $candidateEditor -PathType Leaf) -and
            (Test-Path -LiteralPath $candidateConsole -PathType Leaf))) {
            return $false
        }

        $productVersion = (Get-Item -LiteralPath $candidateEditor).VersionInfo.ProductVersion
        return (-not [string]::IsNullOrWhiteSpace($productVersion) -and
            ($productVersion -eq $projectEditorVersion -or
            $productVersion.StartsWith("$projectEditorVersion`_", [System.StringComparison]::OrdinalIgnoreCase))
        )
    } |
    Select-Object -First 1
if (-not $unityRoot) {
    throw "A complete, identity-matching Unity $projectEditorVersion installation was not found on D:. Checked: $($unityRootCandidates -join ', ')"
}

if ([System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($unityRoot)) -ne 'D:\') {
    throw "Unity editor must run from D:. Refusing to use $unityRoot"
}

$unityConsole = Join-Path $unityRoot 'Unity.com'
$unityEditor = Join-Path $unityRoot 'Unity.exe'
$taskRoot = 'D:\tmp\wof-unity'
$logRoot = Join-Path $taskRoot 'logs'
$cacheRoot = 'D:\UnityPackageCache'
$editorProfileRoot = 'D:\UnityEditorProfile'
$editorLocalAppData = Join-Path $editorProfileRoot 'LocalAppData'
$editorRoamingAppData = Join-Path $editorProfileRoot 'RoamingAppData'
$editorWindowsLocalAppData = Join-Path $editorProfileRoot 'AppData\Local'
$editorWindowsRoamingAppData = Join-Path $editorProfileRoot 'AppData\Roaming'
$editorGiCache = Join-Path $editorProfileRoot 'GICache'
$androidStateRoot = 'D:\UnityAndroidState'
$gradleUserHome = Join-Path $androidStateRoot 'Gradle'
$androidUserHome = Join-Path $androidStateRoot 'AndroidUser'
$androidPlaybackRoot = Join-Path $unityRoot 'Data\PlaybackEngines\AndroidPlayer'
$androidSdkRoot = Join-Path $androidPlaybackRoot 'SDK'
$androidNdkRoot = Join-Path $androidPlaybackRoot 'NDK'
$androidJavaHome = Join-Path $androidPlaybackRoot 'OpenJDK'
$androidAapt2 = Join-Path $androidSdkRoot 'build-tools\36.0.0\aapt2.exe'
$portableGitInitializer = Join-Path $projectPath 'Tools\initialize-unity-git-environment.ps1'
$reactProjectPath = 'D:\CodexProjects\Wizards-Only-Fools-React-Latest'
$nodeExecutable = 'D:\Program Files\nodejs\node.exe'
$tsxCli = Join-Path $reactProjectPath 'node_modules\tsx\dist\cli.mjs'
$reactVisualBaker = Join-Path $projectPath 'Tools\bake-react-visual-assets.mts'
$lilyCoilBaker = Join-Path $projectPath 'Tools\bake-lily-coil-assets.mts'
$survivalTerrainBaker = Join-Path $projectPath 'Tools\bake-survival-terrain-assets.mts'
$engineSystemCatalogBaker = Join-Path $projectPath 'Tools\bake-engine-system-catalog.mts'

New-Item -ItemType Directory -Force -Path $taskRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
New-Item -ItemType Directory -Force -Path $editorProfileRoot | Out-Null
New-Item -ItemType Directory -Force -Path $editorLocalAppData | Out-Null
New-Item -ItemType Directory -Force -Path $editorRoamingAppData | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $editorWindowsLocalAppData 'Unity\Caches') | Out-Null
New-Item -ItemType Directory -Force -Path $editorWindowsRoamingAppData | Out-Null
New-Item -ItemType Directory -Force -Path $editorGiCache | Out-Null
New-Item -ItemType Directory -Force -Path $androidStateRoot | Out-Null
New-Item -ItemType Directory -Force -Path $gradleUserHome | Out-Null
New-Item -ItemType Directory -Force -Path $androidUserHome | Out-Null

# Preserve the normal Windows identity environment so Unity can reuse the license
# activated by Hub. Heavy mutable state remains pinned to D: below.
$env:TEMP = $taskRoot
$env:TMP = $taskRoot
$env:UPM_CACHE_ROOT = $cacheRoot
$env:UPM_NPM_CACHE_PATH = Join-Path $cacheRoot 'npm'
$env:UPM_CACHE_PATH = Join-Path $cacheRoot 'packages'
$env:UPM_GIT_LFS_CACHE_PATH = Join-Path $cacheRoot 'git-lfs'
$env:GRADLE_USER_HOME = $gradleUserHome
$env:ANDROID_USER_HOME = $androidUserHome
$env:ANDROID_HOME = $androidSdkRoot
$env:ANDROID_SDK_ROOT = $androidSdkRoot
$env:ANDROID_NDK_ROOT = $androidNdkRoot
$env:JAVA_HOME = $androidJavaHome

if (-not (Test-Path -LiteralPath $portableGitInitializer -PathType Leaf)) {
    throw "Portable Git environment initializer is missing: $portableGitInitializer"
}
$additionalPathEntries = @(
    'D:\UnityRuntimeCompat',
    $unityRoot,
    (Join-Path $unityRoot 'Data\Tools'),
    (Join-Path $androidJavaHome 'bin'),
    (Join-Path $androidSdkRoot 'platform-tools'),
    (Join-Path $androidSdkRoot 'build-tools\36.0.0')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
& $portableGitInitializer -AdditionalDPath $additionalPathEntries -SkipFullPayloadVerification | Out-Null

function Remove-RunArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ([System.IO.Path]::GetPathRoot($fullPath) -ne 'D:\') {
        throw "Run artifacts must stay on D:. Refusing to remove $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Force -ErrorAction Stop
    }
    if (Test-Path -LiteralPath $fullPath) {
        throw "Could not clear stale run artifact: $fullPath"
    }
}

function Start-WindowsPlayerOnD {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlayerPath,
        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,
        [Parameter(Mandatory = $true)]
        [string]$ProfileRoot
    )

    $fullPlayerPath = [System.IO.Path]::GetFullPath($PlayerPath)
    $fullProfileRoot = [System.IO.Path]::GetFullPath($ProfileRoot)
    if ([System.IO.Path]::GetPathRoot($fullPlayerPath) -ne 'D:\' -or
        [System.IO.Path]::GetPathRoot($fullProfileRoot) -ne 'D:\') {
        throw "Windows player and profile must stay on D:. Player=$fullPlayerPath Profile=$fullProfileRoot"
    }

    $localAppData = Join-Path $fullProfileRoot 'AppData\Local'
    $roamingAppData = Join-Path $fullProfileRoot 'AppData\Roaming'
    $profileTemp = Join-Path $fullProfileRoot 'Temp'
    New-Item -ItemType Directory -Force -Path $localAppData, $roamingAppData, $profileTemp | Out-Null

    $environmentNames = @('USERPROFILE', 'LOCALAPPDATA', 'APPDATA', 'TEMP', 'TMP')
    $previousEnvironment = @{}
    foreach ($name in $environmentNames) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    try {
        $env:USERPROFILE = $fullProfileRoot
        $env:LOCALAPPDATA = $localAppData
        $env:APPDATA = $roamingAppData
        $env:TEMP = $profileTemp
        $env:TMP = $profileTemp
        return Start-Process -FilePath $fullPlayerPath -ArgumentList $ArgumentList `
            -WorkingDirectory (Split-Path $fullPlayerPath) -WindowStyle Hidden -PassThru
    }
    finally {
        foreach ($name in $environmentNames) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
    }
}

function Invoke-UnityBatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $logPath = Join-Path $logRoot "$Name.log"
    Remove-RunArtifact -Path $logPath
    $allArguments = @(
        '-batchmode',
        '-nographics',
        '-accept-apiupdate',
        '-projectPath', $projectPath,
        '-giCustomCacheLocation', $editorGiCache,
        '-logFile', $logPath
    ) + $Arguments

    Push-Location 'D:\'
    try {
        & $unityConsole @allArguments
        $unityExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($unityExitCode -ne 0) {
        $tail = Get-Content -LiteralPath $logPath -Tail 120 -ErrorAction SilentlyContinue
        throw "Unity action '$Name' failed with exit code $unityExitCode.`n$($tail -join [Environment]::NewLine)"
    }

    return $logPath
}

function Invoke-ReactVisualBaker {
    foreach ($requiredPath in @($nodeExecutable, $tsxCli, $reactVisualBaker, $lilyCoilBaker, $survivalTerrainBaker, $engineSystemCatalogBaker)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "React visual baker dependency is missing: $requiredPath"
        }
        if ([System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($requiredPath)) -ne 'D:\') {
            throw "React visual baking is restricted to D:. Refusing dependency: $requiredPath"
        }
    }

    $previousNpmCache = $env:npm_config_cache
    $previousNodeOptions = $env:NODE_OPTIONS
    try {
        $env:npm_config_cache = Join-Path $taskRoot 'npm-cache'
        $env:NODE_OPTIONS = '--no-warnings'
        New-Item -ItemType Directory -Force -Path $env:npm_config_cache | Out-Null
        & $nodeExecutable $tsxCli $reactVisualBaker
        if ($LASTEXITCODE -ne 0) {
            throw "React visual baker failed with exit code $LASTEXITCODE."
        }
        & $nodeExecutable $tsxCli $lilyCoilBaker
        if ($LASTEXITCODE -ne 0) {
            throw "Lily Coil baker failed with exit code $LASTEXITCODE."
        }
        & $nodeExecutable $tsxCli $survivalTerrainBaker
        if ($LASTEXITCODE -ne 0) {
            throw "Survival terrain baker failed with exit code $LASTEXITCODE."
        }
        & $nodeExecutable $tsxCli $engineSystemCatalogBaker
        if ($LASTEXITCODE -ne 0) {
            throw "Engine system catalog baker failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        $env:npm_config_cache = $previousNpmCache
        $env:NODE_OPTIONS = $previousNodeOptions
    }
}

function Invoke-Bootstrap {
    Invoke-ReactVisualBaker
    Invoke-UnityBatch -Name 'bootstrap' -Arguments @(
        '-executeMethod', 'WOF.Editor.WofProjectAutomation.BootstrapProject',
        '-quit'
    )
}

function Invoke-Tests {
    Invoke-ReactVisualBaker
    $resultsPath = Join-Path $logRoot 'editmode-results.xml'
    $performanceResultsPath = Join-Path $logRoot 'editmode-performance-results.json'
    Remove-RunArtifact -Path $resultsPath
    Remove-RunArtifact -Path $performanceResultsPath
    Invoke-UnityBatch -Name 'editmode-tests' -Arguments @(
        '-runTests',
        '-testPlatform', 'EditMode',
        '-testResults', $resultsPath,
        '-perfTestResults', $performanceResultsPath
    )

    $validatorPath = Join-Path $PSScriptRoot 'assert-nunit-results.ps1'
    & $validatorPath -Path $resultsPath
}

function Assert-BuildLogMarker {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedTarget
    )

    $pattern = [regex]::Escape("[WOF-AUTOMATION] BUILD_COMPLETE target=$ExpectedTarget ")
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf) -or
        -not (Select-String -LiteralPath $LogPath -Pattern $pattern -Quiet)) {
        throw "Unity exited without the current WOF build-complete marker for $ExpectedTarget. Log: $LogPath"
    }

    $shaderErrors = @(Select-String -LiteralPath $LogPath -SimpleMatch 'Shader error in' -ErrorAction SilentlyContinue)
    if ($shaderErrors.Count -gt 0) {
        $details = $shaderErrors | Select-Object -First 12 | ForEach-Object { $_.Line }
        throw "Unity reported player shader compilation errors for $ExpectedTarget.`n$($details -join [Environment]::NewLine)"
    }
}

function Assert-BuildReceipt {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReceiptPath,
        [Parameter(Mandatory = $true)]
        [string]$PrimaryArtifact,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedTarget,
        [Parameter(Mandatory = $true)]
        [DateTime]$BuildStartedUtc
    )

    if (-not (Test-Path -LiteralPath $ReceiptPath -PathType Leaf)) {
        throw "Build identity receipt is missing: $ReceiptPath"
    }
    try {
        $receipt = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json
        $completedUtc = [DateTimeOffset]::Parse(
            [string]$receipt.completedUtc,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind).UtcDateTime
    }
    catch {
        throw "Build identity receipt is invalid: $ReceiptPath. $($_.Exception.Message)"
    }

    if ($completedUtc -lt $BuildStartedUtc.AddSeconds(-2)) {
        throw "Build identity receipt is stale: $ReceiptPath"
    }
    if (-not (Test-Path -LiteralPath $PrimaryArtifact -PathType Leaf)) {
        throw "Build receipt primary artifact is missing: $PrimaryArtifact"
    }

    $artifactInfo = Get-Item -LiteralPath $PrimaryArtifact
    $artifactHash = (Get-FileHash -LiteralPath $PrimaryArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
    if (([int]$receipt.schemaVersion -ne 1 -and [int]$receipt.schemaVersion -ne 2) -or
        [string]$receipt.target -ne $ExpectedTarget -or
        [uint64]$receipt.reportedTotalSize -eq 0 -or
        -not [string]::Equals([string]$receipt.primaryArtifact, $PrimaryArtifact, [System.StringComparison]::OrdinalIgnoreCase) -or
        [long]$receipt.primaryLength -ne $artifactInfo.Length -or
        -not [string]::Equals([string]$receipt.primarySha256, $artifactHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Build receipt does not match the current $ExpectedTarget artifact: $ReceiptPath"
    }


    if ($ExpectedTarget -eq 'StandaloneWindows64') {
        if ([int]$receipt.schemaVersion -ne 2) {
            throw "Windows build receipt must use scene-payload schema 2: $ReceiptPath"
        }
        $scenePayloads = @($receipt.scenePayloads)
        if ($scenePayloads.Count -ne 6) {
            throw "Windows build receipt must identify all 6 current scene payloads: $ReceiptPath"
        }
        for ($sceneIndex = 0; $sceneIndex -lt $scenePayloads.Count; $sceneIndex++) {
            $scenePayload = $scenePayloads[$sceneIndex]
            $sceneArtifact = [string]$scenePayload.artifact
            $expectedName = 'level' + $sceneIndex
            if ([string]::IsNullOrWhiteSpace($sceneArtifact) -or
                [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($sceneArtifact)) -ne 'D:\' -or
                [System.IO.Path]::GetFileName($sceneArtifact) -ne $expectedName -or
                -not (Test-Path -LiteralPath $sceneArtifact -PathType Leaf)) {
                throw "Windows build receipt scene payload $sceneIndex is missing, misordered, or outside D: $ReceiptPath"
            }
            $sceneInfo = Get-Item -LiteralPath $sceneArtifact
            $sceneHash = (Get-FileHash -LiteralPath $sceneArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
            if ([long]$scenePayload.length -ne $sceneInfo.Length -or
                $sceneInfo.Length -eq 0 -or
                -not [string]::Equals([string]$scenePayload.sha256, $sceneHash, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Windows build receipt does not match scene payload: $sceneArtifact"
            }
        }

        $payloadArtifact = [string]$receipt.payloadArtifact
        if ([string]::IsNullOrWhiteSpace($payloadArtifact) -or
            [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($payloadArtifact)) -ne 'D:\' -or
            -not (Test-Path -LiteralPath $payloadArtifact -PathType Leaf)) {
            throw "Windows build receipt scene payload is missing or outside D: $ReceiptPath"
        }

        $payloadInfo = Get-Item -LiteralPath $payloadArtifact
        $payloadHash = (Get-FileHash -LiteralPath $payloadArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
        if ([long]$receipt.payloadLength -ne $payloadInfo.Length -or
            $payloadInfo.Length -eq 0 -or
            -not [string]::Equals(
                [string]$receipt.payloadSha256,
                $payloadHash,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Windows build receipt does not match its current scene payload: $payloadArtifact"
        }

        $additivePayloadArtifact = [string]$receipt.additivePayloadArtifact
        if ([string]::IsNullOrWhiteSpace($additivePayloadArtifact) -or
            [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($additivePayloadArtifact)) -ne 'D:\' -or
            -not (Test-Path -LiteralPath $additivePayloadArtifact -PathType Leaf)) {
            throw "Windows build receipt additive scene payload is missing or outside D: $ReceiptPath"
        }

        $additivePayloadInfo = Get-Item -LiteralPath $additivePayloadArtifact
        $additivePayloadHash = (Get-FileHash -LiteralPath $additivePayloadArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
        if ([long]$receipt.additivePayloadLength -ne $additivePayloadInfo.Length -or
            $additivePayloadInfo.Length -eq 0 -or
            -not [string]::Equals(
                [string]$receipt.additivePayloadSha256,
                $additivePayloadHash,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Windows build receipt does not match its current additive scene payload: $additivePayloadArtifact"
        }
    }
}

function Assert-WindowsPlayerLaunch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlayerPath
    )

    $launchLog = Join-Path $logRoot 'validate-windows-player-launch.log'
    $profileRoot = Join-Path $taskRoot ('validate-windows-profile-' + [Guid]::NewGuid().ToString('N'))
    Remove-RunArtifact -Path $launchLog
    New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
    $process = $null
    try {
        $process = Start-WindowsPlayerOnD -PlayerPath $PlayerPath -ProfileRoot $profileRoot -ArgumentList @(
            '--wof-solo',
            '--wof-auto-exit=120',
            "--wof-profile-root=$profileRoot",
            '-batchmode',
            '-nographics',
            '-logFile',
            $launchLog
        )

        $deadline = [DateTime]::UtcNow.AddSeconds(90)
        $ready = $false
        do {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            $ready = (Test-Path -LiteralPath $launchLog -PathType Leaf) -and
                (Select-String -LiteralPath $launchLog -Pattern 'SESSION_READY mode=Solo' -Quiet)
        } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

        if (-not $ready) {
            $tail = if (Test-Path -LiteralPath $launchLog -PathType Leaf) {
                (Get-Content -LiteralPath $launchLog -Tail 80) -join [Environment]::NewLine
            }
            else {
                '<player launch log was not created>'
            }
            throw "Windows player failed its executable launch gate before SESSION_READY.`n$tail"
        }

        Write-Output "Windows player executable launch verified: SESSION_READY mode=Solo log=$launchLog"
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        if ($profileRoot.StartsWith($taskRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $profileRoot -PathType Container)) {
            Remove-Item -LiteralPath $profileRoot -Recurse -Force
        }
    }
}

function Invoke-BuildWindows {
    Invoke-ReactVisualBaker
    $buildStartedUtc = [DateTime]::UtcNow
    $buildLog = @(Invoke-UnityBatch -Name 'build-windows' -Arguments @(
        '-executeMethod', 'WOF.Editor.WofProjectAutomation.BuildWindowsBatch',
        '-quit'
    ))[-1]

    $playerPath = Join-Path $projectPath 'Builds\Windows\WizardsOnlyFools.exe'
    if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf) -or (Get-Item -LiteralPath $playerPath).Length -eq 0) {
        throw "Windows build completed without a non-empty player artifact at $playerPath"
    }
    Assert-BuildLogMarker -LogPath $buildLog -ExpectedTarget 'StandaloneWindows64'
    Assert-BuildReceipt -ReceiptPath ($playerPath + '.build.json') -PrimaryArtifact $playerPath -ExpectedTarget 'StandaloneWindows64' -BuildStartedUtc $buildStartedUtc
    Assert-WindowsPlayerLaunch -PlayerPath $playerPath
}

function Assert-ExistingWindowsBuild {
    $playerPath = Join-Path $projectPath 'Builds\Windows\WizardsOnlyFools.exe'
    $receiptPath = $playerPath + '.build.json'
    $buildLog = Join-Path $logRoot 'build-windows.log'
    if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf) -or (Get-Item -LiteralPath $playerPath).Length -eq 0) {
        throw "A non-empty Windows player is not available for receipt validation: $playerPath"
    }
    Assert-BuildLogMarker -LogPath $buildLog -ExpectedTarget 'StandaloneWindows64'
    Assert-BuildReceipt -ReceiptPath $receiptPath -PrimaryArtifact $playerPath -ExpectedTarget 'StandaloneWindows64' -BuildStartedUtc ([DateTime]'2000-01-01T00:00:00Z')
    Assert-WindowsPlayerLaunch -PlayerPath $playerPath
    Write-Output "Existing Windows build receipt, primary artifact, and scene payload verified: $playerPath"
}

function Invoke-BuildWebGl {
    Invoke-ReactVisualBaker
    $buildStartedUtc = [DateTime]::UtcNow
    $buildLog = @(Invoke-UnityBatch -Name 'build-webgl' -Arguments @(
        '-executeMethod', 'WOF.Editor.WofProjectAutomation.BuildWebGlBatch',
        '-quit'
    ))[-1]

    $indexPath = Join-Path $projectPath 'Builds\WebGL\index.html'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf) -or (Get-Item -LiteralPath $indexPath).Length -eq 0) {
        throw "WebGL build completed without a non-empty entry point at $indexPath"
    }

    $webGlBuildRoot = Join-Path $projectPath 'Builds\WebGL\Build'
    foreach ($pattern in @('*.loader.js', '*.data*', '*.framework.js*', '*.wasm*')) {
        $matches = @(Get-ChildItem -LiteralPath $webGlBuildRoot -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like $pattern -and $_.Length -gt 0 })
        if ($matches.Count -eq 0) {
            throw "WebGL build is missing a non-empty $pattern payload under $webGlBuildRoot"
        }
    }

    Assert-BuildLogMarker -LogPath $buildLog -ExpectedTarget 'WebGL'
    Assert-BuildReceipt -ReceiptPath (Join-Path $projectPath 'Builds\WebGL\WofBuildReceipt.json') -PrimaryArtifact $indexPath -ExpectedTarget 'WebGL' -BuildStartedUtc $buildStartedUtc
}

function Assert-ExistingWebGlBuild {
    $indexPath = Join-Path $projectPath 'Builds\WebGL\index.html'
    $receiptPath = Join-Path $projectPath 'Builds\WebGL\WofBuildReceipt.json'
    $buildLog = Join-Path $logRoot 'build-webgl.log'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf) -or (Get-Item -LiteralPath $indexPath).Length -eq 0) {
        throw "A non-empty WebGL entry point is not available for receipt validation: $indexPath"
    }
    $webGlBuildRoot = Join-Path $projectPath 'Builds\WebGL\Build'
    foreach ($pattern in @('*.loader.js', '*.data*', '*.framework.js*', '*.wasm*')) {
        $matches = @(Get-ChildItem -LiteralPath $webGlBuildRoot -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like $pattern -and $_.Length -gt 0 })
        if ($matches.Count -eq 0) {
            throw "Existing WebGL build is missing a non-empty $pattern payload under $webGlBuildRoot"
        }
    }
    Assert-BuildLogMarker -LogPath $buildLog -ExpectedTarget 'WebGL'
    Assert-BuildReceipt -ReceiptPath $receiptPath -PrimaryArtifact $indexPath -ExpectedTarget 'WebGL' -BuildStartedUtc ([DateTime]'2000-01-01T00:00:00Z')
    Write-Output "Existing WebGL build receipt and required payloads verified: $indexPath"
}

function Invoke-BuildAndroid {
    Invoke-ReactVisualBaker
    $buildStartedUtc = [DateTime]::UtcNow
    [void](Invoke-UnityBatch -Name 'build-android' -Arguments @(
        '-executeMethod', 'WOF.Editor.WofProjectAutomation.BuildAndroidBatch',
        '-quit'
    ))

    Assert-ExistingAndroidBuild -MinimumCompletedUtc $buildStartedUtc
}

function Assert-ExistingAndroidBuild {
    param([DateTime]$MinimumCompletedUtc = [DateTime]::MinValue)

    $apkPath = Join-Path $projectPath 'Builds\Android\WizardsOnlyFools.apk'
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf) -or (Get-Item -LiteralPath $apkPath).Length -eq 0) {
        throw "Android build completed without a non-empty APK artifact at $apkPath"
    }

    $receiptPath = $apkPath + '.build.json'
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw "Android build completed without its identity receipt at $receiptPath"
    }

    $buildLog = Join-Path $logRoot 'build-android.log'
    Assert-BuildLogMarker -LogPath $buildLog -ExpectedTarget 'Android'

    $receiptInfo = Get-Item -LiteralPath $receiptPath
    if ($receiptInfo.Length -eq 0 -or
        ($MinimumCompletedUtc -ne [DateTime]::MinValue -and
         $receiptInfo.LastWriteTimeUtc -lt $MinimumCompletedUtc.AddSeconds(-2))) {
        throw "Android build identity receipt is empty or stale: $receiptPath"
    }

    try {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $receiptCompletedUtc = [DateTimeOffset]::Parse(
            [string]$receipt.completedUtc,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind).UtcDateTime
    }
    catch {
        throw "Android build identity receipt is invalid JSON: $($_.Exception.Message)"
    }

    $apkInfo = Get-Item -LiteralPath $apkPath
    $apkSha256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (($MinimumCompletedUtc -ne [DateTime]::MinValue -and
         $receiptCompletedUtc -lt $MinimumCompletedUtc.AddSeconds(-2)) -or
        [int]$receipt.schemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$receipt.packageName) -or
        [string]::IsNullOrWhiteSpace([string]$receipt.versionName) -or
        [int]$receipt.versionCode -le 0 -or
        [long]$receipt.apkLength -ne $apkInfo.Length -or
        -not [string]::Equals([string]$receipt.apkSha256, $apkSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Android build receipt does not match the APK artifact: $receiptPath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = $null
    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($apkPath)
        if ($archive.Entries.Count -eq 0 -or
            $null -eq ($archive.Entries | Where-Object { $_.FullName -eq 'AndroidManifest.xml' } | Select-Object -First 1)) {
            throw 'APK archive is empty or has no AndroidManifest.xml.'
        }

        $buffer = New-Object byte[] 81920
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $entryStream = $null
            try {
                $entryStream = $entry.Open()
                while ($entryStream.Read($buffer, 0, $buffer.Length) -gt 0) { }
            }
            finally {
                if ($null -ne $entryStream) {
                    $entryStream.Dispose()
                }
            }
        }
    }
    catch {
        throw "APK archive integrity validation failed: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }
    }

    if (-not (Test-Path -LiteralPath $androidAapt2 -PathType Leaf)) {
        throw "Android manifest validator is missing from the pinned D-drive SDK: $androidAapt2"
    }

    $badging = @(& $androidAapt2 dump badging $apkPath)
    if ($LASTEXITCODE -ne 0) {
        throw "aapt2 could not inspect the APK manifest (exit code $LASTEXITCODE)."
    }

    $packageLine = $badging | Where-Object { $_ -match "^package: name='" } | Select-Object -First 1
    $packageMatch = [regex]::Match(
        [string]$packageLine,
        "^package: name='(?<name>[^']+)' versionCode='(?<code>[^']+)' versionName='(?<version>[^']*)'")
    if (-not $packageMatch.Success) {
        throw 'aapt2 did not return a parseable package identity from the APK manifest.'
    }

    $manifestPackageName = $packageMatch.Groups['name'].Value
    $manifestVersionCode = $packageMatch.Groups['code'].Value
    $manifestVersionName = $packageMatch.Groups['version'].Value
    if ($manifestPackageName -ne [string]$receipt.packageName -or
        $manifestVersionCode -ne ([string][int]$receipt.versionCode) -or
        $manifestVersionName -ne [string]$receipt.versionName) {
        throw "APK manifest identity does not match its build receipt. Manifest=$manifestPackageName/$manifestVersionName/$manifestVersionCode Receipt=$($receipt.packageName)/$($receipt.versionName)/$($receipt.versionCode)"
    }

    Write-Output "Android APK identity verified from its internal manifest: $manifestPackageName versionName=$manifestVersionName versionCode=$manifestVersionCode sha256=$apkSha256"
}

function Invoke-WindowsSmoke {
    $playerPath = Join-Path $projectPath 'Builds\Windows\WizardsOnlyFools.exe'
    if (-not (Test-Path -LiteralPath $playerPath)) {
        throw "Windows build not found at $playerPath. Run build-windows first."
    }

    $hostLog = Join-Path $logRoot 'smoke-host.log'
    $clientLog = Join-Path $logRoot 'smoke-client.log'
    $villagerYelpLog = Join-Path $logRoot 'smoke-villager-yelp.log'
    $smokeRunId = [Guid]::NewGuid().ToString('N')
    $hostProfileRoot = Join-Path $taskRoot "smoke-host-profile-$smokeRunId"
    $clientProfileRoot = Join-Path $taskRoot "smoke-client-profile-$smokeRunId"
    $villagerYelpProfileRoot = Join-Path $taskRoot "smoke-villager-yelp-profile-$smokeRunId"
    $hostStartupTimeoutSeconds = 120
    $probeTimeoutSeconds = 120
    $autoExitSeconds = 300
    Remove-RunArtifact -Path $hostLog
    Remove-RunArtifact -Path $clientLog
    Remove-RunArtifact -Path $villagerYelpLog

    $hostProcess = $null
    $clientProcess = $null
    try {
        $hostProcess = Start-WindowsPlayerOnD -PlayerPath $playerPath -ProfileRoot $hostProfileRoot -ArgumentList @(
            '--wof-host', '--wof-combat-probe', "--wof-auto-exit=$autoExitSeconds",
            "--wof-profile-root=$hostProfileRoot", '-batchmode', '-nographics', '-logFile', $hostLog
        )

        $deadline = [DateTime]::UtcNow.AddSeconds($hostStartupTimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ((Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'SERVER_STARTED' -Quiet)) {
                break
            }
            Start-Sleep -Milliseconds 250
        }

        if (-not ((Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'SERVER_STARTED' -Quiet))) {
            throw "LAN smoke host did not start within $hostStartupTimeoutSeconds seconds."
        }

        $clientProcess = Start-WindowsPlayerOnD -PlayerPath $playerPath -ProfileRoot $clientProfileRoot -ArgumentList @(
            '--wof-client=127.0.0.1', '--wof-combat-probe', "--wof-auto-exit=$autoExitSeconds",
            "--wof-profile-root=$clientProfileRoot", '-batchmode', '-nographics', '-logFile', $clientLog
        )

        $probeDeadline = [DateTime]::UtcNow.AddSeconds($probeTimeoutSeconds)
        while ([DateTime]::UtcNow -lt $probeDeadline) {
            $hostProbeFailed = (Test-Path -LiteralPath $hostLog) -and
                (Select-String -LiteralPath $hostLog -Pattern 'COMBAT_PROBE_FAILED' -Quiet)
            $clientProbeFailed = (Test-Path -LiteralPath $clientLog) -and
                (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATION_PROBE_FAILED|CLIENT_TRAINING_DUMMY_REPLICATION_FAILED' -Quiet)
            if ($hostProbeFailed -or $clientProbeFailed) {
                break
            }

            $serverPathPassed = (Test-Path -LiteralPath $hostLog) -and
                (Select-String -LiteralPath $hostLog -Pattern 'CLIENT_RPC_SERVER_PATH_PASSED' -Quiet)
            $trainingDummyServerPathPassed = (Test-Path -LiteralPath $hostLog) -and
                (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_TWO_PEER_SERVER_PATH_PASSED' -Quiet)
            $clientReplicationPassed = (Test-Path -LiteralPath $clientLog) -and
                (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATION_PROBE_PASSED' -Quiet)
            $clientTrainingDummyReplicationPassed = (Test-Path -LiteralPath $clientLog) -and
                (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_REPLICATION_PASSED' -Quiet)
            if ($serverPathPassed -and $trainingDummyServerPathPassed -and
                $clientReplicationPassed -and $clientTrainingDummyReplicationPassed) {
                break
            }

            if ($hostProcess.HasExited -or $clientProcess.HasExited) {
                break
            }

            Start-Sleep -Milliseconds 250
        }
    }
    finally {
        if ($null -ne $clientProcess) {
            Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $hostProcess) {
            Stop-Process -Id $hostProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }

    $hostConnected = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'CLIENT_CONNECTED id=1' -Quiet)
    $clientReady = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'SESSION_READY mode=Client' -Quiet)
    $probeStarted = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'COMBAT_PROBE_STARTED attacker=0 target=1' -Quiet)
    $probePositioned = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'COMBAT_PROBE_POSITIONED attacker=0 target=1' -Quiet)
    $serverFireballCasts = @((Select-String -LiteralPath $hostLog -Pattern 'SPELL_CAST owner=0 hand=Right spell=Fireball' -ErrorAction SilentlyContinue)).Count
    $serverDamageEvents = @((Select-String -LiteralPath $hostLog -Pattern 'DAMAGE target=1 source=0 amount=20' -ErrorAction SilentlyContinue)).Count
    $serverTargetDied = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'PLAYER_DIED id=1' -Quiet)
    $serverTargetRespawned = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'PLAYER_RESPAWNED id=1' -Quiet)
    $serverRespawnConfirmed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'COMBAT_PROBE_RESPAWN_CONFIRMED target=1 elapsedSeconds=' -Quiet)
    $serverCombatPassed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'SERVER_COMBAT_PROBE_PASSED attacker=0 target=1 casts=5' -Quiet)
    $campfireDamagePassed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'CAMPFIRE_DAMAGE_PROBE_PASSED target=1 health=99.9 armor=0.0 tick=0.2' -Quiet)

    $clientRpcFireballCasts = @((Select-String -LiteralPath $hostLog -Pattern 'SPELL_CAST owner=1 hand=Right spell=Fireball' -ErrorAction SilentlyContinue)).Count
    $clientRpcDamageEvents = @((Select-String -LiteralPath $hostLog -Pattern 'DAMAGE target=0 source=1 amount=20' -ErrorAction SilentlyContinue)).Count
    $clientRpcTargetDied = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'PLAYER_DIED id=0' -Quiet)
    $clientRpcTargetRespawned = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'PLAYER_RESPAWNED id=0' -Quiet)
    $clientRpcServerRespawnConfirmed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'CLIENT_RPC_SERVER_RESPAWN_CONFIRMED target=0 elapsedSeconds=' -Quiet)
    $clientRpcServerPathPassed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'CLIENT_RPC_SERVER_PATH_PASSED attacker=1 target=0 casts=5' -Quiet)

    $clientCastRpcRequests = @((Select-String -LiteralPath $clientLog -Pattern 'CLIENT_CAST_RPC_SENT owner=1 target=0' -ErrorAction SilentlyContinue)).Count
    $clientReplicatedDamageEvents = @((Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATED_DAMAGE observer=1 target=0' -ErrorAction SilentlyContinue)).Count
    $clientReplicatedDeath = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATED_DEATH observer=1 target=0' -Quiet)
    $clientReplicatedRespawnHealth = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATED_RESPAWN_HEALTH observer=1 target=0' -Quiet)
    $clientReplicatedRespawnAlive = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATED_RESPAWN_ALIVE observer=1 target=0' -Quiet)
    $clientReplicationPassed = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATION_PROBE_PASSED observer=1 target=0 casts=5' -Quiet)

    $trainingDummyProbeStarted = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_TWO_PEER_PROBE_STARTED owner=1 source=0 instance=automation-client-training-dummy' -Quiet)
    $trainingDummyPlacementAcknowledged = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'CLIENT_TRAINING_DUMMY_PLACEMENT_ACKNOWLEDGED owner=1 instance=automation-client-training-dummy' -Quiet)
    $trainingDummyServerPlacementConfirmed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_SERVER_PLACEMENT_CONFIRMED owner=1 instance=automation-client-training-dummy health=120' -Quiet)
    $trainingDummyServerHits = @((Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_HIT owner=1 instance=automation-client-training-dummy source=0 spell=Fireball damage=24' -ErrorAction SilentlyContinue)).Count
    $trainingDummyServerDamageConfirmed = @((Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_SERVER_DAMAGE_CONFIRMED owner=1 instance=automation-client-training-dummy' -ErrorAction SilentlyContinue)).Count
    $trainingDummyServerDownConfirmed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_SERVER_DOWN_CONFIRMED owner=1 instance=automation-client-training-dummy sequence=5' -Quiet)
    $trainingDummyServerRespawnConfirmed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_SERVER_RESPAWN_CONFIRMED owner=1 instance=automation-client-training-dummy elapsedSeconds=' -Quiet)
    $trainingDummyServerPathPassed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'TRAINING_DUMMY_TWO_PEER_SERVER_PATH_PASSED owner=1 source=0 instance=automation-client-training-dummy hits=5' -Quiet)

    $clientTrainingDummyUpsertSent = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_UPSERT_SENT owner=1 instance=automation-client-training-dummy' -Quiet)
    $clientTrainingDummyPlacementReplicated = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_PLACEMENT_REPLICATED observer=1 owner=1 instance=automation-client-training-dummy health=120' -Quiet)
    $clientTrainingDummyDamageEvents = @((Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_DAMAGE_REPLICATED observer=1 owner=1 instance=automation-client-training-dummy' -ErrorAction SilentlyContinue)).Count
    $clientTrainingDummyDownReplicated = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_DOWN_REPLICATED observer=1 owner=1 instance=automation-client-training-dummy sequence=5' -Quiet)
    $clientTrainingDummyRespawnReplicated = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_RESPAWN_REPLICATED observer=1 owner=1 instance=automation-client-training-dummy health=120' -Quiet)
    $clientTrainingDummyReplicationPassed = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_REPLICATION_PASSED observer=1 owner=1 instance=automation-client-training-dummy hits=5' -Quiet)

    $probeFailed = (Test-Path -LiteralPath $hostLog) -and (Select-String -LiteralPath $hostLog -Pattern 'COMBAT_PROBE_FAILED' -Quiet)
    $clientProbeFailed = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_REPLICATION_PROBE_FAILED' -Quiet)
    $clientTrainingDummyProbeFailed = (Test-Path -LiteralPath $clientLog) -and (Select-String -LiteralPath $clientLog -Pattern 'CLIENT_TRAINING_DUMMY_REPLICATION_FAILED' -Quiet)
    $hostVillagerVisibilityReady = (Test-Path -LiteralPath $hostLog) -and
        (Select-String -LiteralPath $hostLog -Pattern 'VILLAGER_VISIBILITY visible=\d+ total=307' -Quiet)
    $clientVillagerVisibilityReady = (Test-Path -LiteralPath $clientLog) -and
        (Select-String -LiteralPath $clientLog -Pattern 'VILLAGER_VISIBILITY visible=\d+ total=307' -Quiet)
    $hostVillagerArchiveReady = (Test-Path -LiteralPath $hostLog) -and
        (Select-String -LiteralPath $hostLog -Pattern 'VILLAGER_ARCHIVE_READY id=.+ entries=52' -Quiet)
    $clientVillagerArchiveReady = (Test-Path -LiteralPath $clientLog) -and
        (Select-String -LiteralPath $clientLog -Pattern 'VILLAGER_ARCHIVE_READY id=.+ entries=52' -Quiet)
    $hostVillagerMultiplayerFacingReady = (Test-Path -LiteralPath $hostLog) -and
        (Select-String -LiteralPath $hostLog -Pattern 'VILLAGER_FACING_TARGETS active=2 local=1 remotes=1' -Quiet)
    $clientVillagerMultiplayerFacingReady = (Test-Path -LiteralPath $clientLog) -and
        (Select-String -LiteralPath $clientLog -Pattern 'VILLAGER_FACING_TARGETS active=2 local=1 remotes=1' -Quiet)
    $runtimeFailurePattern = 'Villager archive (load|parse) failed|InvalidOperationException|NullReferenceException|Unhandled Exception'
    $hostRuntimeFailure = (Test-Path -LiteralPath $hostLog) -and
        (Select-String -LiteralPath $hostLog -Pattern $runtimeFailurePattern -Quiet)
    $clientRuntimeFailure = (Test-Path -LiteralPath $clientLog) -and
        (Select-String -LiteralPath $clientLog -Pattern $runtimeFailurePattern -Quiet)
    if (-not ($hostConnected -and $clientReady -and $probeStarted -and $probePositioned -and
        $serverFireballCasts -eq 5 -and $serverDamageEvents -eq 5 -and $serverTargetDied -and
        $serverTargetRespawned -and $serverRespawnConfirmed -and $serverCombatPassed -and $campfireDamagePassed -and
        $clientRpcFireballCasts -eq 5 -and $clientRpcDamageEvents -eq 5 -and $clientRpcTargetDied -and
        $clientRpcTargetRespawned -and $clientRpcServerRespawnConfirmed -and $clientRpcServerPathPassed -and
        $clientCastRpcRequests -eq 5 -and $clientReplicatedDamageEvents -eq 5 -and $clientReplicatedDeath -and
        $clientReplicatedRespawnHealth -and $clientReplicatedRespawnAlive -and $clientReplicationPassed -and
        $trainingDummyProbeStarted -and $trainingDummyPlacementAcknowledged -and
        $trainingDummyServerPlacementConfirmed -and $trainingDummyServerHits -eq 5 -and
        $trainingDummyServerDamageConfirmed -eq 5 -and $trainingDummyServerDownConfirmed -and
        $trainingDummyServerRespawnConfirmed -and $trainingDummyServerPathPassed -and
        $clientTrainingDummyUpsertSent -and $clientTrainingDummyPlacementReplicated -and
        $clientTrainingDummyDamageEvents -eq 5 -and $clientTrainingDummyDownReplicated -and
        $clientTrainingDummyRespawnReplicated -and $clientTrainingDummyReplicationPassed -and
        $hostVillagerVisibilityReady -and $clientVillagerVisibilityReady -and
        $hostVillagerArchiveReady -and $clientVillagerArchiveReady -and
        $hostVillagerMultiplayerFacingReady -and $clientVillagerMultiplayerFacingReady -and
        -not $probeFailed -and -not $clientProbeFailed -and -not $clientTrainingDummyProbeFailed -and
        -not $hostRuntimeFailure -and -not $clientRuntimeFailure)) {
        throw "Two-process LAN combat smoke failed. hostConnected=$hostConnected clientReady=$clientReady campfireDamagePassed=$campfireDamagePassed serverCombatPassed=$serverCombatPassed serverFireballCasts=$serverFireballCasts serverDamageEvents=$serverDamageEvents clientRpcServerPathPassed=$clientRpcServerPathPassed clientRpcFireballCasts=$clientRpcFireballCasts clientRpcDamageEvents=$clientRpcDamageEvents clientCastRpcRequests=$clientCastRpcRequests clientReplicatedDamageEvents=$clientReplicatedDamageEvents clientReplicatedDeath=$clientReplicatedDeath clientReplicatedRespawnHealth=$clientReplicatedRespawnHealth clientReplicatedRespawnAlive=$clientReplicatedRespawnAlive clientReplicationPassed=$clientReplicationPassed trainingDummyProbeStarted=$trainingDummyProbeStarted trainingDummyPlacementAcknowledged=$trainingDummyPlacementAcknowledged trainingDummyServerPlacementConfirmed=$trainingDummyServerPlacementConfirmed trainingDummyServerHits=$trainingDummyServerHits trainingDummyServerDamageConfirmed=$trainingDummyServerDamageConfirmed trainingDummyServerDownConfirmed=$trainingDummyServerDownConfirmed trainingDummyServerRespawnConfirmed=$trainingDummyServerRespawnConfirmed trainingDummyServerPathPassed=$trainingDummyServerPathPassed clientTrainingDummyUpsertSent=$clientTrainingDummyUpsertSent clientTrainingDummyPlacementReplicated=$clientTrainingDummyPlacementReplicated clientTrainingDummyDamageEvents=$clientTrainingDummyDamageEvents clientTrainingDummyDownReplicated=$clientTrainingDummyDownReplicated clientTrainingDummyRespawnReplicated=$clientTrainingDummyRespawnReplicated clientTrainingDummyReplicationPassed=$clientTrainingDummyReplicationPassed hostVillagerVisibilityReady=$hostVillagerVisibilityReady clientVillagerVisibilityReady=$clientVillagerVisibilityReady hostVillagerArchiveReady=$hostVillagerArchiveReady clientVillagerArchiveReady=$clientVillagerArchiveReady hostVillagerMultiplayerFacingReady=$hostVillagerMultiplayerFacingReady clientVillagerMultiplayerFacingReady=$clientVillagerMultiplayerFacingReady probeFailed=$probeFailed clientProbeFailed=$clientProbeFailed clientTrainingDummyProbeFailed=$clientTrainingDummyProbeFailed hostRuntimeFailure=$hostRuntimeFailure clientRuntimeFailure=$clientRuntimeFailure"
    }

    $villagerYelpProcess = Start-WindowsPlayerOnD -PlayerPath $playerPath -ProfileRoot $villagerYelpProfileRoot -ArgumentList @(
        '--wof-host', '--wof-villager-view-probe', '--wof-auto-exit=10',
        "--wof-profile-root=$villagerYelpProfileRoot", '-batchmode', '-nographics', '-logFile', $villagerYelpLog
    )
    try {
        if (-not $villagerYelpProcess.WaitForExit(30000)) {
            throw 'Villager yelp runtime probe did not exit within 30 seconds.'
        }
    }
    finally {
        Stop-Process -Id $villagerYelpProcess.Id -Force -ErrorAction SilentlyContinue
    }

    $villagerYelpReady = (Test-Path -LiteralPath $villagerYelpLog) -and
        (Select-String -LiteralPath $villagerYelpLog -Pattern 'VILLAGER_YELP id=48-64 volume=0\.500 duration=0\.31' -Quiet)
    $villagerYelpArchiveReady = (Test-Path -LiteralPath $villagerYelpLog) -and
        (Select-String -LiteralPath $villagerYelpLog -Pattern 'VILLAGER_ARCHIVE_READY id=48-64 entries=52' -Quiet)
    $villagerYelpRuntimeFailure = (Test-Path -LiteralPath $villagerYelpLog) -and
        (Select-String -LiteralPath $villagerYelpLog -Pattern $runtimeFailurePattern -Quiet)
    if (-not $villagerYelpReady -or -not $villagerYelpArchiveReady -or $villagerYelpRuntimeFailure) {
        throw "Villager yelp runtime probe failed. yelpReady=$villagerYelpReady archiveReady=$villagerYelpArchiveReady runtimeFailure=$villagerYelpRuntimeFailure log=$villagerYelpLog"
    }

    Write-Output "Two-process LAN combat smoke passed: player and client-owned training-dummy server authority/replication, exact 307-villager runtime archives, and the exact procedural villager yelp verified with no runtime exceptions. Host log: $hostLog Client log: $clientLog Yelp log: $villagerYelpLog"
}

$runLockPath = Join-Path $taskRoot 'wof-unity.run.lock'
$runLock = $null
try {
    $runLock = [System.IO.File]::Open(
        $runLockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch {
    throw "Another WOF Unity automation run is active (exclusive lock: $runLockPath). $($_.Exception.Message)"
}

try {
    switch ($Action) {
        'bootstrap' { Invoke-Bootstrap }
        'test' { Invoke-Tests }
        'build-windows' { Invoke-BuildWindows }
        'validate-windows' { Assert-ExistingWindowsBuild }
        'build-webgl' { Invoke-BuildWebGl }
        'validate-webgl' { Assert-ExistingWebGlBuild }
        'build-android' { Invoke-BuildAndroid }
        'validate-android' { Assert-ExistingAndroidBuild }
        'verify' {
            Assert-ExistingWindowsBuild
            Assert-ExistingWebGlBuild
            Assert-ExistingAndroidBuild
        }
        'rebuild-all' {
            Invoke-Bootstrap
            Invoke-Tests
            Invoke-BuildWindows
            Invoke-WindowsSmoke
            Invoke-BuildWebGl
            Invoke-BuildAndroid
        }
        'smoke-windows' { Invoke-WindowsSmoke }
        'open' {
            Start-Process -FilePath $unityEditor -ArgumentList @('-projectPath', $projectPath) -WorkingDirectory 'D:\'
        }
    }
}
finally {
    if ($null -ne $runLock) {
        $runLock.Dispose()
    }
}
