[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'apply', 'verify', 'apply-ngo-patch')]
    [string]$Action = 'plan'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$manifestPath = Join-Path $projectRoot 'Packages\manifest.json'
$lockPath = Join-Path $projectRoot 'Packages\packages-lock.json'
$projectVersionPath = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$unityProjectLockPath = Join-Path $projectRoot 'Temp\UnityLockfile'
$targetEditorRoot = 'D:\UnityEditors\6000.3.21f1'
$targetEditorExecutable = Join-Path $targetEditorRoot 'Editor\Unity.exe'
$editorCatalogPath = Join-Path $targetEditorRoot 'Editor\Data\Resources\PackageManager\Editor\manifest.json'
$backupRoot = 'D:\UnityProjectBackups\Wizards-Only-Fools-Unity\package-migrations'
$targetEditorVersion = '6000.3.21f1'
$targetEditorChangeset = 'c02631ffc030'

$baselineTargets = [ordered]@{
    'com.unity.inputsystem' = '1.20.0'
    'com.unity.multiplayer.center' = '1.0.1'
    'com.unity.netcode.gameobjects' = '2.13.0'
    'com.unity.transport' = '2.7.4'
    'com.unity.render-pipelines.universal' = '17.3.0'
    'com.unity.test-framework' = '1.6.0'
}

$legacyOrBaselineVersions = [ordered]@{
    'com.unity.inputsystem' = @('1.12.0', '1.20.0')
    'com.unity.multiplayer.center' = @('1.0.0', '1.0.1')
    'com.unity.netcode.gameobjects' = @('2.7.0', '2.13.0')
    'com.unity.render-pipelines.universal' = @('17.2.0', '17.3.0')
    'com.unity.test-framework' = @('1.5.1', '1.6.0')
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$strictUtf8NoBom = New-Object System.Text.UTF8Encoding($false, $true)

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

function Assert-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Assert-DDrivePath -Path $Path | Out-Null
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file not found: $Path"
    }
}

function Assert-ProjectClosedForMutation {
    Assert-DDrivePath -Path $unityProjectLockPath | Out-Null
    if (Test-Path -LiteralPath $unityProjectLockPath) {
        throw "Unity appears to have this project open ($unityProjectLockPath exists). Close the Editor before changing package pins."
    }
}

function Read-Utf8Text {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Assert-RequiredFile -Path $Path
    return [System.IO.File]::ReadAllText($Path, $strictUtf8NoBom)
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $text = Read-Utf8Text -Path $Path
    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON in ${Path}: $($_.Exception.Message)"
    }
}

function Get-JsonProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return $Object.PSObject.Properties[$Name]
}

function Get-DependencyMap {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest
    )

    $dependenciesProperty = Get-JsonProperty -Object $Manifest -Name 'dependencies'
    if ($null -eq $dependenciesProperty -or $null -eq $dependenciesProperty.Value) {
        throw 'Packages\manifest.json does not contain a dependencies object.'
    }

    $map = @{}
    foreach ($property in $dependenciesProperty.Value.PSObject.Properties) {
        if ($map.ContainsKey($property.Name)) {
            throw "Duplicate manifest dependency: $($property.Name)"
        }

        $map[$property.Name] = [string]$property.Value
    }

    return $map
}

function Get-DependencyVersion {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Dependencies,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not $Dependencies.ContainsKey($Name)) {
        return $null
    }

    return [string]$Dependencies[$Name]
}

function Assert-EditorIdentity {
    Assert-RequiredFile -Path $targetEditorExecutable
    Assert-RequiredFile -Path $editorCatalogPath

    $productVersion = (Get-Item -LiteralPath $targetEditorExecutable).VersionInfo.ProductVersion
    $validProductVersion = $productVersion -eq $targetEditorVersion -or
        $productVersion.StartsWith("$targetEditorVersion`_$targetEditorChangeset", [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $validProductVersion) {
        throw "Unexpected target Editor identity at ${targetEditorExecutable}: $productVersion"
    }

    $catalog = Read-JsonFile -Path $editorCatalogPath
    $catalogPackagesProperty = Get-JsonProperty -Object $catalog -Name 'packages'
    if ($null -eq $catalogPackagesProperty -or $null -eq $catalogPackagesProperty.Value) {
        throw "Editor package catalog is missing its packages object: $editorCatalogPath"
    }

    $expectedCatalogVersions = [ordered]@{
        'com.unity.inputsystem' = '1.20.0'
        'com.unity.multiplayer.center' = '1.0.1'
        'com.unity.netcode.gameobjects' = '2.13.0'
        'com.unity.transport' = '2.7.4'
        'com.unity.render-pipelines.universal' = '17.3.0'
        'com.unity.test-framework' = '1.6.0'
        'com.unity.ugui' = '2.0.0'
    }

    foreach ($entry in $expectedCatalogVersions.GetEnumerator()) {
        $packageProperty = Get-JsonProperty -Object $catalogPackagesProperty.Value -Name $entry.Key
        if ($null -eq $packageProperty -or $null -eq $packageProperty.Value) {
            throw "Editor package catalog does not contain $($entry.Key)."
        }

        $minimumProperty = Get-JsonProperty -Object $packageProperty.Value -Name 'minimumVersion'
        if ($null -eq $minimumProperty -or [string]$minimumProperty.Value -ne $entry.Value) {
            $observed = if ($null -eq $minimumProperty) { '<missing>' } else { [string]$minimumProperty.Value }
            throw "Unexpected Editor minimum for $($entry.Key): $observed (expected $($entry.Value))."
        }
    }

    return $catalog
}

function Assert-PreservedUgui {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Dependencies
    )

    $uguiVersion = Get-DependencyVersion -Dependencies $Dependencies -Name 'com.unity.ugui'
    if ($uguiVersion -ne '2.0.0') {
        $observed = if ($null -eq $uguiVersion) { '<missing>' } else { $uguiVersion }
        throw "com.unity.ugui must remain exactly 2.0.0; observed $observed."
    }
}

function Assert-BaselineApplyPreconditions {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Dependencies
    )

    Assert-PreservedUgui -Dependencies $Dependencies

    foreach ($entry in $legacyOrBaselineVersions.GetEnumerator()) {
        $observed = Get-DependencyVersion -Dependencies $Dependencies -Name $entry.Key
        if ($null -eq $observed -or $entry.Value -notcontains $observed) {
            $display = if ($null -eq $observed) { '<missing>' } else { $observed }
            throw "Refusing to migrate unexpected $($entry.Key) version $display. Allowed versions: $($entry.Value -join ', ')."
        }
    }

    $transportVersion = Get-DependencyVersion -Dependencies $Dependencies -Name 'com.unity.transport'
    if ($null -ne $transportVersion -and $transportVersion -ne '2.7.4') {
        throw "Refusing to replace unexpected explicit com.unity.transport version $transportVersion."
    }
}

function Assert-ManifestTargetState {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Dependencies,
        [Parameter(Mandatory = $true)]
        [ValidateSet('2.13.0', '2.13.1')]
        [string]$ExpectedNgoVersion
    )

    Assert-PreservedUgui -Dependencies $Dependencies

    foreach ($entry in $baselineTargets.GetEnumerator()) {
        $expected = if ($entry.Key -eq 'com.unity.netcode.gameobjects') { $ExpectedNgoVersion } else { $entry.Value }
        $observed = Get-DependencyVersion -Dependencies $Dependencies -Name $entry.Key
        if ($observed -ne $expected) {
            $display = if ($null -eq $observed) { '<missing>' } else { $observed }
            throw "Manifest pin mismatch for $($entry.Key): $display (expected $expected)."
        }
    }
}

function Set-ExactDependencyVersionInText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$FromVersion,
        [Parameter(Mandatory = $true)]
        [string]$ToVersion
    )

    $pattern = '(?m)^(?<indent>[ \t]*)"' + [regex]::Escape($Name) +
        '"(?<beforeColon>[ \t]*):(?<afterColon>[ \t]*)"' + [regex]::Escape($FromVersion) +
        '"(?<comma>,?)(?<trailing>[ \t]*)(?<carriage>\r?)$'
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    $matches = $regex.Matches($Text)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one textual $Name $FromVersion manifest pin; found $($matches.Count)."
    }

    $replacement = [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return $match.Groups['indent'].Value + '"' + $Name + '"' +
            $match.Groups['beforeColon'].Value + ':' + $match.Groups['afterColon'].Value +
            '"' + $ToVersion + '"' + $match.Groups['comma'].Value + $match.Groups['trailing'].Value +
            $match.Groups['carriage'].Value
    }

    return $regex.Replace($Text, $replacement, 1)
}

function Add-TransportDependencyInText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $pattern = '(?m)^(?<indent>[ \t]*)"com\.unity\.netcode\.gameobjects"[ \t]*:[ \t]*"2\.13\.0",(?<trailing>[ \t]*)(?<carriage>\r?)$'
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    $matches = $regex.Matches($Text)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one NGO 2.13.0 line before adding Unity Transport; found $($matches.Count)."
    }

    $replacement = [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $matchedLine = $match.Value
        if ($matchedLine.EndsWith("`r", [System.StringComparison]::Ordinal)) {
            $matchedLine = $matchedLine.Substring(0, $matchedLine.Length - 1)
        }
        return $matchedLine + $newline + $match.Groups['indent'].Value + '"com.unity.transport": "2.7.4",'
    }

    return $regex.Replace($Text, $replacement, 1)
}

function Assert-DependencyMutationScope {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Before,
        [Parameter(Mandatory = $true)]
        [hashtable]$After,
        [Parameter(Mandatory = $true)]
        [hashtable]$AllowedTargets,
        [Parameter(Mandatory = $true)]
        [bool]$AllowTransportAddition
    )

    $expectedCount = $Before.Count
    if ($AllowTransportAddition -and -not $Before.ContainsKey('com.unity.transport')) {
        $expectedCount++
    }

    if ($After.Count -ne $expectedCount) {
        throw "Manifest dependency count changed outside the allowed scope: $($Before.Count) -> $($After.Count)."
    }

    foreach ($name in $Before.Keys) {
        if (-not $After.ContainsKey($name)) {
            throw "Manifest dependency was removed unexpectedly: $name"
        }

        $expected = if ($AllowedTargets.ContainsKey($name)) { [string]$AllowedTargets[$name] } else { [string]$Before[$name] }
        if ([string]$After[$name] -ne $expected) {
            throw "Manifest dependency changed outside the allowed scope: $name $($Before[$name]) -> $($After[$name])."
        }
    }

    foreach ($name in $After.Keys) {
        if (-not $Before.ContainsKey($name) -and ($name -ne 'com.unity.transport' -or -not $AllowTransportAddition)) {
            throw "Manifest dependency was added unexpectedly: $name"
        }
    }

    if ($AllowTransportAddition -and [string]$After['com.unity.transport'] -ne '2.7.4') {
        throw 'The only permitted new dependency is com.unity.transport 2.7.4.'
    }
}

function Get-TreeInventory {
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

            $relativePath = $item.FullName.Substring($fullRoot.Length).TrimStart('\')
            if ($item.PSIsContainer) {
                $records.Add("D|$relativePath")
                $pending.Push($item.FullName)
            }
            else {
                $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
                $records.Add("F|$relativePath|$($item.Length)|$hash")
            }
        }
    }

    $recordArray = $records.ToArray()
    [Array]::Sort($recordArray, [System.StringComparer]::Ordinal)
    return $recordArray
}

function New-VerifiedSourceBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    Assert-DDrivePath -Path $backupRoot | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
    $safeLabel = $Label -replace '[^0-9A-Za-z._-]', '_'
    $backupPath = Join-Path $backupRoot "$timestamp-$safeLabel"
    if (Test-Path -LiteralPath $backupPath) {
        $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $backupPath = Join-Path $backupRoot "$timestamp-$safeLabel-$suffix"
    }

    Assert-DDrivePath -Path $backupPath | Out-Null
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    New-Item -ItemType Directory -Path $backupPath | Out-Null

    foreach ($sourceName in @('Assets', 'Packages', 'ProjectSettings')) {
        $sourcePath = Join-Path $projectRoot $sourceName
        $destinationPath = Join-Path $backupPath $sourceName
        Assert-DDrivePath -Path $sourcePath | Out-Null
        Assert-DDrivePath -Path $destinationPath | Out-Null

        $sourceInventory = @(Get-TreeInventory -Root $sourcePath)
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Recurse -Force
        $destinationInventory = @(Get-TreeInventory -Root $destinationPath)
        $differences = @(Compare-Object -ReferenceObject $sourceInventory -DifferenceObject $destinationInventory)
        if ($differences.Count -gt 0) {
            throw "Backup verification failed for $sourceName at $destinationPath. The partial backup was retained for inspection."
        }
    }

    $metadata = [ordered]@{
        schemaVersion = 1
        createdUtc = [DateTime]::UtcNow.ToString('o')
        label = $Label
        sourceProject = $projectRoot
        manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
        packagesLockSha256 = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
        projectVersionSha256 = (Get-FileHash -LiteralPath $projectVersionPath -Algorithm SHA256).Hash
    }
    $metadataPath = Join-Path $backupPath 'migration-metadata.json'
    Assert-DDrivePath -Path $metadataPath | Out-Null
    [System.IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 4), $utf8NoBom)

    return $backupPath
}

function Write-ManifestText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    Assert-DDrivePath -Path $manifestPath | Out-Null
    $temporaryPath = Join-Path (Split-Path -Parent $manifestPath) ('.manifest.wof-migration-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    Assert-DDrivePath -Path $temporaryPath | Out-Null

    try {
        [System.IO.File]::WriteAllText($temporaryPath, $Text, $utf8NoBom)
        Read-JsonFile -Path $temporaryPath | Out-Null
        Move-Item -LiteralPath $temporaryPath -Destination $manifestPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Compare-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Left,
        [Parameter(Mandatory = $true)]
        [string]$Right
    )

    $versionPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>-[0-9A-Za-z.-]+)?$'
    $leftMatch = [regex]::Match($Left, $versionPattern)
    $rightMatch = [regex]::Match($Right, $versionPattern)
    if (-not $leftMatch.Success -or -not $rightMatch.Success) {
        throw "Cannot compare non-semantic package versions '$Left' and '$Right'."
    }

    $leftCore = New-Object System.Version(
        [int]$leftMatch.Groups['major'].Value,
        [int]$leftMatch.Groups['minor'].Value,
        [int]$leftMatch.Groups['patch'].Value)
    $rightCore = New-Object System.Version(
        [int]$rightMatch.Groups['major'].Value,
        [int]$rightMatch.Groups['minor'].Value,
        [int]$rightMatch.Groups['patch'].Value)
    $coreComparison = $leftCore.CompareTo($rightCore)
    if ($coreComparison -ne 0) {
        return $coreComparison
    }

    $leftSuffix = $leftMatch.Groups['suffix'].Value
    $rightSuffix = $rightMatch.Groups['suffix'].Value
    if ([string]::IsNullOrWhiteSpace($leftSuffix) -and -not [string]::IsNullOrWhiteSpace($rightSuffix)) {
        return 1
    }
    if (-not [string]::IsNullOrWhiteSpace($leftSuffix) -and [string]::IsNullOrWhiteSpace($rightSuffix)) {
        return -1
    }

    return [string]::Compare($leftSuffix, $rightSuffix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-LockMinimumViolations {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Lock,
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    $lockDependenciesProperty = Get-JsonProperty -Object $Lock -Name 'dependencies'
    $catalogPackagesProperty = Get-JsonProperty -Object $Catalog -Name 'packages'
    if ($null -eq $lockDependenciesProperty -or $null -eq $lockDependenciesProperty.Value) {
        throw 'Packages\packages-lock.json does not contain a dependencies object.'
    }
    if ($null -eq $catalogPackagesProperty -or $null -eq $catalogPackagesProperty.Value) {
        throw 'The Editor package catalog does not contain a packages object.'
    }

    $violations = New-Object System.Collections.Generic.List[string]
    foreach ($lockProperty in $lockDependenciesProperty.Value.PSObject.Properties) {
        $catalogProperty = Get-JsonProperty -Object $catalogPackagesProperty.Value -Name $lockProperty.Name
        if ($null -eq $catalogProperty -or $null -eq $catalogProperty.Value) {
            continue
        }

        $minimumProperty = Get-JsonProperty -Object $catalogProperty.Value -Name 'minimumVersion'
        $versionProperty = Get-JsonProperty -Object $lockProperty.Value -Name 'version'
        if ($null -eq $minimumProperty -or [string]::IsNullOrWhiteSpace([string]$minimumProperty.Value) -or
            $null -eq $versionProperty -or [string]::IsNullOrWhiteSpace([string]$versionProperty.Value)) {
            continue
        }

        $observed = [string]$versionProperty.Value
        $minimum = [string]$minimumProperty.Value
        if ((Compare-PackageVersion -Left $observed -Right $minimum) -lt 0) {
            $violations.Add("$($lockProperty.Name) $observed is below Editor minimum $minimum")
        }
    }

    return $violations.ToArray()
}

function Get-ProjectIdentity {
    $text = Read-Utf8Text -Path $projectVersionPath
    $versionMatch = [regex]::Match($text, '(?m)^m_EditorVersion:\s*(?<value>\S+)\s*$')
    $revisionMatch = [regex]::Match($text, '(?m)^m_EditorVersionWithRevision:\s*(?<version>\S+)\s*\((?<revision>[0-9A-Fa-f]+)\)\s*$')
    if (-not $versionMatch.Success -or -not $revisionMatch.Success) {
        throw "Could not parse Unity project identity from $projectVersionPath"
    }

    return [pscustomobject]@{
        Version = $versionMatch.Groups['value'].Value
        RevisionVersion = $revisionMatch.Groups['version'].Value
        Revision = $revisionMatch.Groups['revision'].Value
    }
}

function Get-VerificationProblems {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('2.13.0', '2.13.1')]
        [string]$ExpectedNgoVersion,
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    $problems = New-Object System.Collections.Generic.List[string]
    $manifest = Read-JsonFile -Path $manifestPath
    $dependencies = Get-DependencyMap -Manifest $manifest
    try {
        Assert-ManifestTargetState -Dependencies $dependencies -ExpectedNgoVersion $ExpectedNgoVersion
    }
    catch {
        $problems.Add($_.Exception.Message)
    }

    $projectIdentity = Get-ProjectIdentity
    if ($projectIdentity.Version -ne $targetEditorVersion -or
        $projectIdentity.RevisionVersion -ne $targetEditorVersion -or
        $projectIdentity.Revision -ne $targetEditorChangeset) {
        $problems.Add(
            "Project identity is $($projectIdentity.Version) ($($projectIdentity.Revision)); expected $targetEditorVersion ($targetEditorChangeset).")
    }

    $lock = Read-JsonFile -Path $lockPath
    $lockDependenciesProperty = Get-JsonProperty -Object $lock -Name 'dependencies'
    if ($null -eq $lockDependenciesProperty -or $null -eq $lockDependenciesProperty.Value) {
        $problems.Add('Packages\packages-lock.json does not contain a dependencies object.')
    }
    else {
        foreach ($entry in $baselineTargets.GetEnumerator()) {
            $expected = if ($entry.Key -eq 'com.unity.netcode.gameobjects') { $ExpectedNgoVersion } else { $entry.Value }
            $lockProperty = Get-JsonProperty -Object $lockDependenciesProperty.Value -Name $entry.Key
            if ($null -eq $lockProperty -or $null -eq $lockProperty.Value) {
                $problems.Add("Lock file is missing $($entry.Key) $expected.")
                continue
            }

            $versionProperty = Get-JsonProperty -Object $lockProperty.Value -Name 'version'
            $observed = if ($null -eq $versionProperty) { '<missing>' } else { [string]$versionProperty.Value }
            if ($observed -ne $expected) {
                $problems.Add("Lock pin mismatch for $($entry.Key): $observed (expected $expected).")
            }
        }

        $uguiLockProperty = Get-JsonProperty -Object $lockDependenciesProperty.Value -Name 'com.unity.ugui'
        $uguiLockVersionProperty = if ($null -eq $uguiLockProperty) { $null } else { Get-JsonProperty -Object $uguiLockProperty.Value -Name 'version' }
        $uguiLockVersion = if ($null -eq $uguiLockVersionProperty) { '<missing>' } else { [string]$uguiLockVersionProperty.Value }
        if ($uguiLockVersion -ne '2.0.0') {
            $problems.Add("Lock pin mismatch for com.unity.ugui: $uguiLockVersion (expected 2.0.0).")
        }
    }

    foreach ($violation in @(Get-LockMinimumViolations -Lock $lock -Catalog $Catalog)) {
        $problems.Add($violation)
    }

    return $problems.ToArray()
}

function Assert-ResolvedPackageState {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('2.13.0', '2.13.1')]
        [string]$ExpectedNgoVersion,
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    $problems = @(Get-VerificationProblems -ExpectedNgoVersion $ExpectedNgoVersion -Catalog $Catalog)
    if ($problems.Count -gt 0) {
        throw "Package verification failed:`n - $($problems -join "`n - ")"
    }
}

function Invoke-Plan {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    $manifest = Read-JsonFile -Path $manifestPath
    $dependencies = Get-DependencyMap -Manifest $manifest
    $lock = Read-JsonFile -Path $lockPath
    $projectIdentity = Get-ProjectIdentity
    $minimumViolations = @(Get-LockMinimumViolations -Lock $lock -Catalog $Catalog)
    $baselineProblems = @(Get-VerificationProblems -ExpectedNgoVersion '2.13.0' -Catalog $Catalog)
    $finalProblems = @(Get-VerificationProblems -ExpectedNgoVersion '2.13.1' -Catalog $Catalog)

    $manifestPins = [ordered]@{}
    foreach ($name in @(
        'com.unity.inputsystem',
        'com.unity.multiplayer.center',
        'com.unity.netcode.gameobjects',
        'com.unity.transport',
        'com.unity.render-pipelines.universal',
        'com.unity.test-framework',
        'com.unity.ugui')) {
        $value = Get-DependencyVersion -Dependencies $dependencies -Name $name
        $manifestPins[$name] = if ($null -eq $value) { '<absent>' } else { $value }
    }

    $stage = if ($finalProblems.Count -eq 0) {
        'ngo-2.13.1-verified'
    }
    elseif ($baselineProblems.Count -eq 0) {
        'baseline-verified'
    }
    elseif ($manifestPins['com.unity.netcode.gameobjects'] -eq '2.13.1') {
        'ngo-2.13.1-pending-editor-resolution'
    }
    elseif ($manifestPins['com.unity.netcode.gameobjects'] -eq '2.13.0') {
        'baseline-pending-editor-resolution'
    }
    else {
        'legacy-or-unrecognized'
    }

    [pscustomobject]@{
        action = 'plan'
        mutationPerformed = $false
        stage = $stage
        projectVersion = $projectIdentity.Version
        projectRevision = $projectIdentity.Revision
        manifestPins = $manifestPins
        lockBelowEditorMinimum = $minimumViolations
        baselineVerificationProblems = $baselineProblems
        finalVerificationProblems = $finalProblems
        nextAction = switch ($stage) {
            'baseline-verified' { "Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' apply-ngo-patch" }
            'ngo-2.13.1-verified' { 'No package migration action is required.' }
            'baseline-pending-editor-resolution' { 'Open the project with the pinned 6000.3.21f1 Editor, let UPM regenerate packages-lock.json, then run verify.' }
            'ngo-2.13.1-pending-editor-resolution' { 'Open the project with the pinned 6000.3.21f1 Editor, let UPM regenerate packages-lock.json, then run verify.' }
            default { "Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' apply" }
        }
    } | ConvertTo-Json -Depth 8
}

function Invoke-BaselineApply {
    Assert-ProjectClosedForMutation

    $originalText = Read-Utf8Text -Path $manifestPath
    $originalManifest = $originalText | ConvertFrom-Json
    $beforeDependencies = Get-DependencyMap -Manifest $originalManifest
    Assert-BaselineApplyPreconditions -Dependencies $beforeDependencies

    $alreadyAtBaseline = $true
    foreach ($entry in $baselineTargets.GetEnumerator()) {
        if ((Get-DependencyVersion -Dependencies $beforeDependencies -Name $entry.Key) -ne $entry.Value) {
            $alreadyAtBaseline = $false
            break
        }
    }

    if ($alreadyAtBaseline) {
        Write-Output 'Manifest already has the complete Unity 6000.3 baseline pins; no backup or mutation was needed.'
        Write-Output 'Open the project with the pinned Editor so Unity can regenerate packages-lock.json, then run verify.'
        return
    }

    $updatedText = $originalText
    foreach ($entry in $legacyOrBaselineVersions.GetEnumerator()) {
        $name = $entry.Key
        $observed = Get-DependencyVersion -Dependencies $beforeDependencies -Name $name
        $target = [string]$baselineTargets[$name]
        if ($observed -ne $target) {
            $updatedText = Set-ExactDependencyVersionInText -Text $updatedText -Name $name -FromVersion $observed -ToVersion $target
        }
    }

    if (-not $beforeDependencies.ContainsKey('com.unity.transport')) {
        $updatedText = Add-TransportDependencyInText -Text $updatedText
    }

    $updatedManifest = $updatedText | ConvertFrom-Json
    $afterDependencies = Get-DependencyMap -Manifest $updatedManifest
    $allowedTargets = @{}
    foreach ($entry in $baselineTargets.GetEnumerator()) {
        $allowedTargets[$entry.Key] = $entry.Value
    }
    $scopeArguments = @{
        Before = $beforeDependencies
        After = $afterDependencies
        AllowedTargets = $allowedTargets
        AllowTransportAddition = $true
    }
    Assert-DependencyMutationScope @scopeArguments
    Assert-ManifestTargetState -Dependencies $afterDependencies -ExpectedNgoVersion '2.13.0'

    $lockHashBefore = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
    $manifestHashBefore = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $backupPath = New-VerifiedSourceBackup -Label 'pre-unity-6000.3-package-baseline'
    try {
        Assert-ProjectClosedForMutation
        if ((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $manifestHashBefore -or
            (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash -ne $lockHashBefore) {
            throw 'Package files changed while the source backup was being created; no migration was applied.'
        }

        Write-ManifestText -Text $updatedText
        $writtenDependencies = Get-DependencyMap -Manifest (Read-JsonFile -Path $manifestPath)
        $writtenScopeArguments = @{
            Before = $beforeDependencies
            After = $writtenDependencies
            AllowedTargets = $allowedTargets
            AllowTransportAddition = $true
        }
        Assert-DependencyMutationScope @writtenScopeArguments
        Assert-ManifestTargetState -Dependencies $writtenDependencies -ExpectedNgoVersion '2.13.0'

        $lockHashAfter = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
        if ($lockHashAfter -ne $lockHashBefore) {
            throw 'packages-lock.json changed during manifest migration; this tool is not allowed to modify it.'
        }
    }
    catch {
        Write-ManifestText -Text $originalText
        throw "Baseline package migration failed and manifest.json was restored. Backup: $backupPath. $($_.Exception.Message)"
    }

    Write-Output "Unity 6000.3 baseline manifest pins applied. Verified backup: $backupPath"
    Write-Output 'packages-lock.json was not edited. Open the project with the pinned Editor, then run verify.'
}

function Invoke-NgoPatchApply {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    Assert-ProjectClosedForMutation

    $originalText = Read-Utf8Text -Path $manifestPath
    $originalManifest = $originalText | ConvertFrom-Json
    $beforeDependencies = Get-DependencyMap -Manifest $originalManifest
    $currentNgoVersion = Get-DependencyVersion -Dependencies $beforeDependencies -Name 'com.unity.netcode.gameobjects'

    if ($currentNgoVersion -eq '2.13.1') {
        Assert-ManifestTargetState -Dependencies $beforeDependencies -ExpectedNgoVersion '2.13.1'
        Write-Output 'Manifest already pins NGO 2.13.1; no backup or mutation was needed.'
        Write-Output 'Open the project with the pinned Editor so Unity can regenerate packages-lock.json, then run verify.'
        return
    }

    Assert-ResolvedPackageState -ExpectedNgoVersion '2.13.0' -Catalog $Catalog

    $ngoPatchArguments = @{
        Text = $originalText
        Name = 'com.unity.netcode.gameobjects'
        FromVersion = '2.13.0'
        ToVersion = '2.13.1'
    }
    $updatedText = Set-ExactDependencyVersionInText @ngoPatchArguments
    $updatedManifest = $updatedText | ConvertFrom-Json
    $afterDependencies = Get-DependencyMap -Manifest $updatedManifest
    $allowedTargets = @{ 'com.unity.netcode.gameobjects' = '2.13.1' }
    $scopeArguments = @{
        Before = $beforeDependencies
        After = $afterDependencies
        AllowedTargets = $allowedTargets
        AllowTransportAddition = $false
    }
    Assert-DependencyMutationScope @scopeArguments
    Assert-ManifestTargetState -Dependencies $afterDependencies -ExpectedNgoVersion '2.13.1'

    $lockHashBefore = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
    $manifestHashBefore = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $backupPath = New-VerifiedSourceBackup -Label 'pre-ngo-2.13.1-patch'
    try {
        Assert-ProjectClosedForMutation
        if ((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $manifestHashBefore -or
            (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash -ne $lockHashBefore) {
            throw 'Package files changed while the source backup was being created; no migration was applied.'
        }

        Write-ManifestText -Text $updatedText
        $writtenDependencies = Get-DependencyMap -Manifest (Read-JsonFile -Path $manifestPath)
        $writtenScopeArguments = @{
            Before = $beforeDependencies
            After = $writtenDependencies
            AllowedTargets = $allowedTargets
            AllowTransportAddition = $false
        }
        Assert-DependencyMutationScope @writtenScopeArguments
        Assert-ManifestTargetState -Dependencies $writtenDependencies -ExpectedNgoVersion '2.13.1'

        $lockHashAfter = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
        if ($lockHashAfter -ne $lockHashBefore) {
            throw 'packages-lock.json changed during NGO patching; this tool is not allowed to modify it.'
        }
    }
    catch {
        Write-ManifestText -Text $originalText
        throw "NGO patch migration failed and manifest.json was restored. Backup: $backupPath. $($_.Exception.Message)"
    }

    Write-Output "NGO manifest pin updated from 2.13.0 to 2.13.1 only. Verified backup: $backupPath"
    Write-Output 'packages-lock.json was not edited. Open the project with the pinned Editor, then run verify.'
}

foreach ($controlledPath in @(
    $projectRoot,
    $manifestPath,
    $lockPath,
    $projectVersionPath,
    $unityProjectLockPath,
    $targetEditorRoot,
    $targetEditorExecutable,
    $editorCatalogPath,
    $backupRoot
)) {
    Assert-DDrivePath -Path $controlledPath | Out-Null
}

Assert-RequiredFile -Path $manifestPath
Assert-RequiredFile -Path $lockPath
Assert-RequiredFile -Path $projectVersionPath
$editorCatalog = Assert-EditorIdentity

switch ($Action) {
    'plan' {
        Invoke-Plan -Catalog $editorCatalog
    }
    'apply' {
        Invoke-BaselineApply
    }
    'verify' {
        $manifestDependencies = Get-DependencyMap -Manifest (Read-JsonFile -Path $manifestPath)
        $ngoVersion = Get-DependencyVersion -Dependencies $manifestDependencies -Name 'com.unity.netcode.gameobjects'
        if ($ngoVersion -notin @('2.13.0', '2.13.1')) {
            throw "Manifest NGO pin is $ngoVersion; expected migrated baseline 2.13.0 or final 2.13.1."
        }

        Assert-ResolvedPackageState -ExpectedNgoVersion $ngoVersion -Catalog $editorCatalog
        Write-Output "Package verification passed for Unity $targetEditorVersion with NGO $ngoVersion."
    }
    'apply-ngo-patch' {
        Invoke-NgoPatchApply -Catalog $editorCatalog
    }
}
