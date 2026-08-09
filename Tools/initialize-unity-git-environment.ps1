[CmdletBinding()]
param(
    [Parameter()]
    [Alias('AdditionalPath')]
    [string[]]$AdditionalDPath = @(),

    [switch]$SkipFullPayloadVerification
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProjectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:SetupScript = Join-Path $PSScriptRoot 'setup-portable-git.ps1'
$script:GitRoot = 'D:\UnityTools\GitForWindows\2.55.0.3'
$script:GitExe = Join-Path $script:GitRoot 'cmd\git.exe'
$script:GitCommandDirectory = Join-Path $script:GitRoot 'cmd'
$script:GitExecDirectory = Join-Path $script:GitRoot 'mingw64\libexec\git-core'
$script:GitSsh = Join-Path $script:GitRoot 'usr\bin\ssh.exe'
$script:FalseExe = Join-Path $script:GitRoot 'usr\bin\false.exe'
$script:ProfileRoot = 'D:\UnityEditorProfile\Git'
$script:XdgConfigRoot = Join-Path $script:ProfileRoot 'xdg\config'
$script:XdgCacheRoot = Join-Path $script:ProfileRoot 'xdg\cache'
$script:XdgDataRoot = Join-Path $script:ProfileRoot 'xdg\data'
$script:XdgStateRoot = Join-Path $script:ProfileRoot 'xdg\state'
$script:GlobalConfig = Join-Path $script:ProfileRoot '.gitconfig'
$script:SystemConfig = Join-Path $script:ProfileRoot 'system.gitconfig'
$script:ExpectedVersionOutput = 'git version 2.55.0.windows.3'

function Assert-DDrivePath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals(
        [System.IO.Path]::GetPathRoot($fullPath),
        'D:\',
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Purpose must remain on D:. Resolved path: $fullPath"
    }
    if ($fullPath.Contains(';')) {
        throw "$Purpose cannot contain a semicolon because it will be used as one PATH entry: $fullPath"
    }
    return $fullPath
}

function Assert-NoReparsePointsInDPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = Assert-DDrivePath -Path $Path -Purpose $Purpose
    $relativePath = $fullPath.Substring(3).Trim('\')
    $cursor = 'D:\'
    $segments = if ([string]::IsNullOrWhiteSpace($relativePath)) {
        @()
    }
    else {
        @($relativePath.Split('\'))
    }
    foreach ($segment in $segments) {
        $cursor = Join-Path $cursor $segment
        if (-not (Test-Path -LiteralPath $cursor)) {
            break
        }
        $item = Get-Item -LiteralPath $cursor -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Purpose contains a reparse point, which is not allowed: $cursor"
        }
    }
    return $fullPath
}

function Ensure-DDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = Assert-NoReparsePointsInDPath -Path $Path -Purpose $Purpose
    if (-not (Test-Path -LiteralPath $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "$Purpose is not a directory: $fullPath"
    }
    [void](Assert-NoReparsePointsInDPath -Path $fullPath -Purpose $Purpose)
    return $fullPath
}

function Assert-RequiredDFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = Assert-NoReparsePointsInDPath -Path $Path -Purpose $Purpose
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Purpose is missing: $fullPath"
    }
    return $fullPath
}

function Get-SystemPathAllowlist {
    $systemRoot = [System.Environment]::GetEnvironmentVariable('SystemRoot', 'Process')
    if ([string]::IsNullOrWhiteSpace($systemRoot)) {
        throw 'SystemRoot is unavailable; the explicit Windows PATH allowlist cannot be derived safely.'
    }
    $root = [System.IO.Path]::GetFullPath($systemRoot).TrimEnd('\')
    if (-not [System.IO.Path]::IsPathRooted($root)) {
        throw "SystemRoot did not resolve to an absolute path: $root"
    }
    return @(
        (Join-Path $root 'System32'),
        (Join-Path $root 'System32\Wbem'),
        (Join-Path $root 'System32\WindowsPowerShell\v1.0'),
        $root
    )
}

function Get-UniquePathEntries {
    param([Parameter(Mandatory = $true)][string[]]$Entries)

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $Entries) {
        $normalized = [System.IO.Path]::GetFullPath($entry).TrimEnd('\')
        if ($seen.Add($normalized)) {
            $result.Add($normalized)
        }
    }
    return $result.ToArray()
}

function Remove-InheritedGitConfigurationOverrides {
    $removed = [System.Collections.Generic.List[string]]::new()
    $environment = [System.Environment]::GetEnvironmentVariables('Process')
    foreach ($nameValue in @($environment.Keys)) {
        $name = [string]$nameValue
        if ($name.StartsWith('GIT_CONFIG', [System.StringComparison]::OrdinalIgnoreCase)) {
            [System.Environment]::SetEnvironmentVariable($name, $null, 'Process')
            $removed.Add($name)
        }
    }
    return $removed.ToArray()
}

function Remove-InheritedGitExecutionAndPromptOverrides {
    foreach ($name in @(
        'GIT_EXEC_PATH',
        'GIT_SSH',
        'GIT_SSH_COMMAND',
        'GIT_SSH_VARIANT',
        'GIT_ASKPASS',
        'GIT_TERMINAL_PROMPT',
        'GIT_TEMPLATE_DIR',
        'GIT_SSL_CAINFO',
        'GCM_INTERACTIVE',
        'GCM_GUI_PROMPT',
        'SSH_ASKPASS',
        'SSH_ASKPASS_REQUIRE'
    )) {
        [System.Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}

function Invoke-ExactGit {
    param([Parameter(Mandatory = $true)][string[]]$ArgumentList)

    $output = @(& $script:GitExe @ArgumentList 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "D-drive Portable Git command failed with exit code $exitCode`: $($output -join ' ')"
    }
    return $output
}

function Assert-OnlyExpectedGitResolves {
    $commands = @(Get-Command -Name git -All -ErrorAction Stop)
    if ($commands.Count -ne 1) {
        $resolved = @($commands | ForEach-Object {
            if ($_ -is [System.Management.Automation.ApplicationInfo]) {
                $_.Path
            }
            else {
                "$($_.CommandType):$($_.Name)"
            }
        }) -join ', '
        throw "Get-Command must resolve only one controlled Git application. Found: $resolved"
    }
    $command = $commands[0]
    if ($command.CommandType -ne [System.Management.Automation.CommandTypes]::Application) {
        throw "Get-Command resolved a non-application Git command: $($command.CommandType):$($command.Name)"
    }
    $resolvedPath = [System.IO.Path]::GetFullPath($command.Path)
    if (-not [string]::Equals($resolvedPath, $script:GitExe, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Get-Command resolved Git outside the pinned D-drive installation: $resolvedPath"
    }
    return $resolvedPath
}

function Convert-GitOriginToPath {
    param([Parameter(Mandatory = $true)][string]$Origin)

    if (-not $Origin.StartsWith('file:', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Git reported a non-file configuration origin, which is not allowed in this environment: $Origin"
    }
    $originPath = $Origin.Substring(5)
    if ($originPath.Length -ge 2 -and $originPath[0] -eq '"' -and $originPath[$originPath.Length - 1] -eq '"') {
        $originPath = $originPath.Substring(1, $originPath.Length - 2)
        $originPath = $originPath.Replace('\\', '\').Replace('\"', '"')
    }
    $originPath = $originPath.Replace('/', '\')
    if (-not [System.IO.Path]::IsPathRooted($originPath)) {
        $originPath = Join-Path $script:ProjectRoot $originPath
    }
    return [System.IO.Path]::GetFullPath($originPath)
}

function Assert-AllGitConfigurationOriginsOnD {
    $lines = @(Invoke-ExactGit -ArgumentList @('-C', $script:ProjectRoot, 'config', '--includes', '--show-origin', '--list'))
    $origins = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $separator = $line.IndexOf("`t", [System.StringComparison]::Ordinal)
        if ($separator -lt 1) {
            throw "Git configuration origin output was not parseable: $line"
        }
        $origin = $line.Substring(0, $separator)
        $originPath = Convert-GitOriginToPath -Origin $origin
        [void](Assert-DDrivePath -Path $originPath -Purpose 'Git configuration origin')
        [void](Assert-NoReparsePointsInDPath -Path $originPath -Purpose 'Git configuration origin')
        if (-not $origins.Contains($originPath)) {
            $origins.Add($originPath)
        }
    }
    return $origins.ToArray()
}

foreach ($path in @(
    $script:ProjectRoot,
    $script:SetupScript,
    $script:GitRoot,
    $script:GitExe,
    $script:GitCommandDirectory,
    $script:GitExecDirectory,
    $script:GitSsh,
    $script:FalseExe,
    $script:ProfileRoot,
    $script:XdgConfigRoot,
    $script:XdgCacheRoot,
    $script:XdgDataRoot,
    $script:XdgStateRoot,
    $script:GlobalConfig,
    $script:SystemConfig
)) {
    [void](Assert-DDrivePath -Path $path -Purpose 'Unity Git environment path')
}

[void](Assert-RequiredDFile -Path $script:SetupScript -Purpose 'Portable Git setup verifier')
if ($SkipFullPayloadVerification) {
    [void](Assert-RequiredDFile -Path (Join-Path $script:GitRoot '.wof-portable-git-receipt.json') -Purpose 'Portable Git verified-install receipt')
}
else {
    & $script:SetupScript verify | Out-Null
}

foreach ($path in @($script:GitExe, $script:GitSsh, $script:FalseExe)) {
    [void](Assert-RequiredDFile -Path $path -Purpose 'Portable Git runtime')
}
foreach ($path in @($script:GitCommandDirectory, $script:GitExecDirectory)) {
    $directory = Assert-NoReparsePointsInDPath -Path $path -Purpose 'Portable Git runtime directory'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Portable Git runtime directory is missing: $directory"
    }
}

$validatedAdditionalPaths = [System.Collections.Generic.List[string]]::new()
foreach ($path in $AdditionalDPath) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw 'Caller-supplied PATH entries cannot be empty.'
    }
    $fullPath = Assert-NoReparsePointsInDPath -Path $path -Purpose 'Caller-supplied PATH entry'
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Caller-supplied PATH entry is not an existing D-drive directory: $fullPath"
    }
    $validatedAdditionalPaths.Add($fullPath)
}

foreach ($directory in @(
    $script:ProfileRoot,
    $script:XdgConfigRoot,
    $script:XdgCacheRoot,
    $script:XdgDataRoot,
    $script:XdgStateRoot
)) {
    [void](Ensure-DDirectory -Path $directory -Purpose 'Isolated Git profile directory')
}

$removedConfigurationOverrides = @(Remove-InheritedGitConfigurationOverrides)
Remove-InheritedGitExecutionAndPromptOverrides

$env:HOME = $script:ProfileRoot
$env:XDG_CONFIG_HOME = $script:XdgConfigRoot
$env:XDG_CACHE_HOME = $script:XdgCacheRoot
$env:XDG_DATA_HOME = $script:XdgDataRoot
$env:XDG_STATE_HOME = $script:XdgStateRoot

$env:GIT_CONFIG_NOSYSTEM = '1'
$env:GIT_CONFIG_GLOBAL = $script:GlobalConfig
$env:GIT_CONFIG_SYSTEM = $script:SystemConfig
$env:GIT_EXEC_PATH = $script:GitExecDirectory
$env:GIT_SSH = $script:GitSsh
$env:GIT_SSH_VARIANT = 'ssh'
$env:GIT_ASKPASS = $script:FalseExe
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$env:GCM_GUI_PROMPT = '0'
$env:SSH_ASKPASS = $script:FalseExe
$env:SSH_ASKPASS_REQUIRE = 'never'

$systemPaths = @(Get-SystemPathAllowlist)
$allowlistedPaths = @($script:GitCommandDirectory) + @($validatedAdditionalPaths) + $systemPaths
$allowlistedPaths = @(Get-UniquePathEntries -Entries $allowlistedPaths)
$env:PATH = [string]::Join([System.IO.Path]::PathSeparator, $allowlistedPaths)

$resolvedGit = Assert-OnlyExpectedGitResolves
$versionOutput = @(Invoke-ExactGit -ArgumentList @('--version')) -join "`n"
$versionOutput = $versionOutput.Trim()
if ($versionOutput -cne $script:ExpectedVersionOutput) {
    throw "D-drive Portable Git identity mismatch. Expected '$($script:ExpectedVersionOutput)'; found '$versionOutput'."
}
$configOrigins = @(Assert-AllGitConfigurationOriginsOnD)

[pscustomobject]@{
    Status = 'initialized-and-verified'
    GitExe = $resolvedGit
    VersionOutput = $versionOutput
    Home = $env:HOME
    XdgConfigHome = $env:XDG_CONFIG_HOME
    XdgCacheHome = $env:XDG_CACHE_HOME
    XdgDataHome = $env:XDG_DATA_HOME
    XdgStateHome = $env:XDG_STATE_HOME
    PathEntries = $allowlistedPaths
    RemovedInheritedGitConfigOverrides = $removedConfigurationOverrides
    GitConfigNoSystem = $env:GIT_CONFIG_NOSYSTEM
    GitConfigGlobal = $env:GIT_CONFIG_GLOBAL
    GitConfigSystem = $env:GIT_CONFIG_SYSTEM
    CredentialInteraction = 'disabled'
    ConfigurationOrigins = $configOrigins
    AllConfigurationOriginsOnD = $true
}
