[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'install', 'verify')]
    [string]$Action = 'plan'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:ProjectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:ManifestPath = Join-Path $script:ProjectRoot 'Packages\manifest.json'
$script:CodexConfigPath = Join-Path $script:ProjectRoot '.codex\config.toml'

$script:ToolchainRoot = 'D:\UnityMCPToolchain'
$script:UvVersion = '0.12.2'
$script:PythonVersion = '3.14.7'
$script:McpVersion = '10.1.0'
$script:DependencyCutoff = '2026-07-13T21:16:05Z'

$script:UvDirectory = Join-Path $script:ToolchainRoot "uv\$($script:UvVersion)"
$script:UvParentDirectory = Split-Path -Parent $script:UvDirectory
$script:UvExe = Join-Path $script:UvDirectory 'uv.exe'
$script:UvxExe = Join-Path $script:UvDirectory 'uvx.exe'
$script:DownloadsDirectory = Join-Path $script:ToolchainRoot 'downloads'
$script:CacheDirectory = Join-Path $script:ToolchainRoot 'cache'
$script:PythonCacheDirectory = Join-Path $script:ToolchainRoot 'python-cache'
$script:PythonDirectory = Join-Path $script:ToolchainRoot 'python'
$script:PythonBinDirectory = Join-Path $script:ToolchainRoot 'python-bin'
$script:ToolsDirectory = Join-Path $script:ToolchainRoot 'tools'
$script:ToolBinDirectory = Join-Path $script:ToolchainRoot 'tool-bin'
$script:CredentialsDirectory = Join-Path $script:ToolchainRoot 'credentials'
$script:UserProfileDirectory = Join-Path $script:ToolchainRoot 'user-profile'
$script:AppDataRoamingDirectory = Join-Path $script:ToolchainRoot 'app-data\roaming'
$script:AppDataLocalDirectory = Join-Path $script:ToolchainRoot 'app-data\local'
$script:XdgCacheDirectory = Join-Path $script:ToolchainRoot 'xdg\cache'
$script:XdgConfigDirectory = Join-Path $script:ToolchainRoot 'xdg\config'
$script:XdgDataDirectory = Join-Path $script:ToolchainRoot 'xdg\data'
$script:XdgStateDirectory = Join-Path $script:ToolchainRoot 'xdg\state'
$script:LogsDirectory = Join-Path $script:ToolchainRoot 'logs'
$script:TempDirectory = Join-Path $script:ToolchainRoot 'temp'
$script:StagingDirectory = Join-Path $script:ToolchainRoot 'staging'
$script:ReceiptsDirectory = Join-Path $script:ToolchainRoot 'receipts'
$script:QuarantineDirectory = Join-Path $script:ToolchainRoot 'quarantine'

$script:UvArchiveName = 'uv-x86_64-pc-windows-msvc.zip'
$script:UvArchiveUrl = "https://releases.astral.sh/github/uv/releases/download/$($script:UvVersion)/$($script:UvArchiveName)"
$script:UvChecksumUrl = "$($script:UvArchiveUrl).sha256"
$script:UvArchiveSha256 = '01442d8ce5c7124151a73e697c836d252c6da853c18c73206d3cc4c2378a91d2'
$script:UvArchivePath = Join-Path $script:DownloadsDirectory $script:UvArchiveName
$script:UvChecksumPath = Join-Path $script:DownloadsDirectory "$($script:UvArchiveName).sha256"

$script:PyPiJsonUrl = "https://pypi.org/pypi/mcpforunityserver/$($script:McpVersion)/json"
$script:PyPiJsonPath = Join-Path $script:DownloadsDirectory "mcpforunityserver-$($script:McpVersion).json"
$script:McpWheelName = 'mcpforunityserver-10.1.0-py3-none-any.whl'
$script:McpWheelUrl = 'https://files.pythonhosted.org/packages/3e/f5/31b3ed6a114dac89f26ef0fa078e3fe0df1707febc23ba1920a726af767b/mcpforunityserver-10.1.0-py3-none-any.whl'
$script:McpWheelSha256 = '3d64a8fd2542133b619bfa1edcf9ffa80796c0618a88814569429635d72459d7'
$script:McpWheelUploadTime = '2026-07-13T21:16:03.534913Z'
$script:McpWheelPath = Join-Path $script:DownloadsDirectory $script:McpWheelName
$script:McpEntrypointPath = Join-Path $script:ToolBinDirectory 'mcp-for-unity.exe'
$script:McpSecondaryEntrypointPath = Join-Path $script:ToolBinDirectory 'unity-mcp.exe'
$script:McpReceiptPath = Join-Path $script:ReceiptsDirectory 'unity-mcp-10.1.0.json'

function Assert-PathOnDDrive {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($fullPath)
    if (-not [string]::Equals($root, 'D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Purpose must remain on D:. Resolved path: $fullPath"
    }

    return $fullPath
}

function Assert-DescendantPath {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Purpose,
        [switch]$AllowRoot
    )

    $candidatePath = Assert-PathOnDDrive -Path $Candidate -Purpose $Purpose
    $rootPath = (Assert-PathOnDDrive -Path $Root -Purpose "$Purpose root").TrimEnd('\')
    $prefix = "$rootPath\"
    $isRoot = [string]::Equals($candidatePath.TrimEnd('\'), $rootPath, [System.StringComparison]::OrdinalIgnoreCase)
    $isChild = $candidatePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
    if ((-not $isChild) -and (-not ($AllowRoot -and $isRoot))) {
        throw "$Purpose escaped its allowed D: root. Candidate: $candidatePath; root: $rootPath"
    }

    return $candidatePath
}

function Assert-ControlledPaths {
    $projectPaths = @($script:ProjectRoot, $script:ManifestPath, $script:CodexConfigPath)
    foreach ($path in $projectPaths) {
        [void](Assert-PathOnDDrive -Path $path -Purpose 'Project path')
    }

    $toolchainPaths = @(
        $script:ToolchainRoot, $script:UvDirectory, $script:UvParentDirectory,
        $script:UvExe, $script:UvxExe, $script:DownloadsDirectory,
        $script:CacheDirectory, $script:PythonCacheDirectory, $script:PythonDirectory,
        $script:PythonBinDirectory, $script:ToolsDirectory, $script:ToolBinDirectory,
        $script:CredentialsDirectory, $script:UserProfileDirectory, $script:AppDataRoamingDirectory,
        $script:AppDataLocalDirectory, $script:XdgCacheDirectory,
        $script:XdgConfigDirectory, $script:XdgDataDirectory, $script:XdgStateDirectory,
        $script:LogsDirectory, $script:TempDirectory,
        $script:StagingDirectory, $script:ReceiptsDirectory, $script:QuarantineDirectory,
        $script:UvArchivePath, $script:UvChecksumPath,
        $script:PyPiJsonPath, $script:McpWheelPath, $script:McpEntrypointPath,
        $script:McpSecondaryEntrypointPath,
        $script:McpReceiptPath
    )
    foreach ($path in $toolchainPaths) {
        [void](Assert-DescendantPath -Candidate $path -Root $script:ToolchainRoot -Purpose 'Toolchain path' -AllowRoot)
    }

    Assert-NoReparsePoints -Path $script:ProjectRoot -Root $script:ProjectRoot
    if (Test-Path -LiteralPath $script:ToolchainRoot) {
        Assert-NoReparsePoints -Path $script:ToolchainRoot -Root $script:ToolchainRoot
    }
}

function Assert-NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $candidate = Assert-DescendantPath -Candidate $Path -Root $Root -Purpose 'Controlled path' -AllowRoot
    $driveRoot = 'D:\'
    $relative = $candidate.Substring($driveRoot.Length).Trim('\')
    $cursor = $driveRoot
    $segments = if ([string]::IsNullOrWhiteSpace($relative)) { @() } else { @($relative.Split('\')) }
    foreach ($segment in $segments) {
        $cursor = Join-Path $cursor $segment
        if (-not (Test-Path -LiteralPath $cursor)) {
            break
        }
        $item = Get-Item -LiteralPath $cursor -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse points are not allowed in controlled D: paths: $cursor"
        }
    }
}

function Assert-ExistingControlledPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = Assert-PathOnDDrive -Path $Path -Purpose 'Existing controlled path'
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Controlled path is missing: $fullPath"
    }

    $projectPrefix = $script:ProjectRoot.TrimEnd('\') + '\'
    $toolchainPrefix = $script:ToolchainRoot.TrimEnd('\') + '\'
    if ([string]::Equals($fullPath.TrimEnd('\'), $script:ProjectRoot.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        Assert-NoReparsePoints -Path $fullPath -Root $script:ProjectRoot
        return
    }
    if ([string]::Equals($fullPath.TrimEnd('\'), $script:ToolchainRoot.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($toolchainPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        Assert-NoReparsePoints -Path $fullPath -Root $script:ToolchainRoot
        return
    }
    throw "Existing path is outside the reviewed project/toolchain roots: $fullPath"
}

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    [void](Assert-DescendantPath -Candidate $Path -Root $script:ToolchainRoot -Purpose 'Directory creation' -AllowRoot)
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        throw "A file blocks required directory: $Path"
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $Path)
    }
    Assert-NoReparsePoints -Path $Path -Root $script:ToolchainRoot
}

function Remove-UniqueStagingDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    [void](Assert-DescendantPath -Candidate $Path -Root $script:StagingDirectory -Purpose 'Staging cleanup')
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Get-CanonicalCodexConfig {
    return @'
[mcp_servers.unityMCP]
command = "D:/UnityMCPToolchain/tool-bin/mcp-for-unity.exe"
args = ["--transport", "stdio"]
startup_timeout_sec = 60
env_vars = ["SystemRoot"]

[mcp_servers.unityMCP.env]
UV_CACHE_DIR = "D:/UnityMCPToolchain/cache"
UV_PYTHON_CACHE_DIR = "D:/UnityMCPToolchain/python-cache"
UV_PYTHON_INSTALL_DIR = "D:/UnityMCPToolchain/python"
UV_PYTHON_BIN_DIR = "D:/UnityMCPToolchain/python-bin"
UV_TOOL_DIR = "D:/UnityMCPToolchain/tools"
UV_TOOL_BIN_DIR = "D:/UnityMCPToolchain/tool-bin"
UV_CREDENTIALS_DIR = "D:/UnityMCPToolchain/credentials"
UV_PYTHON = "3.14.7"
UV_PYTHON_DOWNLOADS = "never"
UV_MANAGED_PYTHON = "1"
UV_NO_CONFIG = "1"
UV_NO_SYSTEM_CONFIG = "1"
UV_NO_MODIFY_PATH = "1"
UV_NO_PROGRESS = "1"
UV_EXCLUDE_NEWER = "2026-07-13T21:16:05Z"
USERPROFILE = "D:/UnityMCPToolchain/user-profile"
HOME = "D:/UnityMCPToolchain/user-profile"
HOMEDRIVE = "D:"
HOMEPATH = "\\UnityMCPToolchain\\user-profile"
PATH = "D:/UnityMCPToolchain/tool-bin;D:/UnityMCPToolchain/tools/mcpforunityserver/Scripts;D:/UnityMCPToolchain/python-bin;D:/UnityMCPToolchain/uv/0.12.2"
APPDATA = "D:/UnityMCPToolchain/app-data/roaming"
LOCALAPPDATA = "D:/UnityMCPToolchain/app-data/local"
XDG_CACHE_HOME = "D:/UnityMCPToolchain/xdg/cache"
XDG_CONFIG_HOME = "D:/UnityMCPToolchain/xdg/config"
XDG_DATA_HOME = "D:/UnityMCPToolchain/xdg/data"
XDG_STATE_HOME = "D:/UnityMCPToolchain/xdg/state"
TEMP = "D:/UnityMCPToolchain/temp"
TMP = "D:/UnityMCPToolchain/temp"
PYTHONPYCACHEPREFIX = "D:/UnityMCPToolchain/python-cache/bytecode"
PYTHONDONTWRITEBYTECODE = "1"
PYTHONNOUSERSITE = "1"
UNITY_MCP_LOG_DIR = "D:/UnityMCPToolchain/logs"
DISABLE_TELEMETRY = "1"
UNITY_MCP_DISABLE_TELEMETRY = "1"
MCP_DISABLE_TELEMETRY = "1"
'@
}

function Normalize-Text {
    param([Parameter(Mandatory = $true)][string]$Text)
    return (($Text -replace "`r`n", "`n") -replace "`r", "`n").Trim()
}

function Assert-ProjectConfiguration {
    if (-not (Test-Path -LiteralPath $script:ManifestPath -PathType Leaf)) {
        throw "Unity manifest is missing: $($script:ManifestPath)"
    }
    Assert-ExistingControlledPath -Path $script:ManifestPath
    $manifest = Get-Content -LiteralPath $script:ManifestPath -Raw | ConvertFrom-Json
    if ($null -eq $manifest.dependencies) {
        throw 'Unity manifest has no dependencies object.'
    }
    $dependency = $manifest.dependencies.PSObject.Properties['com.coplaydev.unity-mcp']
    $expectedPackage = 'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#c14de1e6dc01ab42d2bb358730cff954bce0ce6b'
    if (($null -eq $dependency) -or ($dependency.Value -cne $expectedPackage)) {
        throw "Unity MCP package must be pinned exactly to $expectedPackage"
    }

    if (-not (Test-Path -LiteralPath $script:CodexConfigPath -PathType Leaf)) {
        throw "Project Codex configuration is missing: $($script:CodexConfigPath)"
    }
    Assert-ExistingControlledPath -Path $script:CodexConfigPath
    $actualConfig = Normalize-Text -Text (Get-Content -LiteralPath $script:CodexConfigPath -Raw)
    $expectedConfig = Normalize-Text -Text (Get-CanonicalCodexConfig)
    if ($actualConfig -cne $expectedConfig) {
        throw "Project Codex configuration differs from the reviewed D-only stdio configuration: $($script:CodexConfigPath)"
    }
}

function Set-ProcessToolchainEnvironment {
    param([ValidateSet('manual', 'never')][string]$PythonDownloads = 'never')

    $pathValues = [ordered]@{
        UV_CACHE_DIR = $script:CacheDirectory
        UV_PYTHON_CACHE_DIR = $script:PythonCacheDirectory
        UV_PYTHON_INSTALL_DIR = $script:PythonDirectory
        UV_PYTHON_BIN_DIR = $script:PythonBinDirectory
        UV_TOOL_DIR = $script:ToolsDirectory
        UV_TOOL_BIN_DIR = $script:ToolBinDirectory
        UV_CREDENTIALS_DIR = $script:CredentialsDirectory
        USERPROFILE = $script:UserProfileDirectory
        HOME = $script:UserProfileDirectory
        APPDATA = $script:AppDataRoamingDirectory
        LOCALAPPDATA = $script:AppDataLocalDirectory
        XDG_CACHE_HOME = $script:XdgCacheDirectory
        XDG_CONFIG_HOME = $script:XdgConfigDirectory
        XDG_DATA_HOME = $script:XdgDataDirectory
        XDG_STATE_HOME = $script:XdgStateDirectory
        TEMP = $script:TempDirectory
        TMP = $script:TempDirectory
        PYTHONPYCACHEPREFIX = (Join-Path $script:PythonCacheDirectory 'bytecode')
        UNITY_MCP_LOG_DIR = $script:LogsDirectory
    }
    foreach ($entry in $pathValues.GetEnumerator()) {
        [void](Assert-DescendantPath -Candidate $entry.Value -Root $script:ToolchainRoot -Purpose "Environment variable $($entry.Key)" -AllowRoot)
        [System.Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $runtimePathEntries = @(
        $script:ToolBinDirectory,
        (Join-Path $script:ToolsDirectory 'mcpforunityserver\Scripts'),
        $script:PythonBinDirectory,
        $script:UvDirectory
    )
    foreach ($runtimePathEntry in $runtimePathEntries) {
        [void](Assert-DescendantPath -Candidate $runtimePathEntry -Root $script:ToolchainRoot -Purpose 'Sanitized runtime PATH entry')
    }
    [System.Environment]::SetEnvironmentVariable('PATH', ($runtimePathEntries -join ';'), 'Process')

    $scalarValues = [ordered]@{
        UV_PYTHON = $script:PythonVersion
        UV_PYTHON_DOWNLOADS = $PythonDownloads
        UV_MANAGED_PYTHON = '1'
        UV_NO_CONFIG = '1'
        UV_NO_SYSTEM_CONFIG = '1'
        UV_NO_MODIFY_PATH = '1'
        UV_NO_PROGRESS = '1'
        UV_EXCLUDE_NEWER = $script:DependencyCutoff
        HOMEDRIVE = 'D:'
        HOMEPATH = '\UnityMCPToolchain\user-profile'
        PYTHONDONTWRITEBYTECODE = '1'
        PYTHONNOUSERSITE = '1'
        PYTHONUTF8 = '1'
        DISABLE_TELEMETRY = '1'
        UNITY_MCP_DISABLE_TELEMETRY = '1'
        MCP_DISABLE_TELEMETRY = '1'
    }
    foreach ($entry in $scalarValues.GetEnumerator()) {
        [System.Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

function Ensure-ToolchainDirectories {
    $directories = @(
        $script:ToolchainRoot, $script:UvParentDirectory, $script:DownloadsDirectory,
        $script:CacheDirectory, $script:PythonCacheDirectory, $script:PythonDirectory,
        $script:PythonBinDirectory, $script:ToolsDirectory, $script:ToolBinDirectory,
        $script:CredentialsDirectory, $script:UserProfileDirectory, $script:AppDataRoamingDirectory,
        $script:AppDataLocalDirectory, $script:XdgCacheDirectory,
        $script:XdgConfigDirectory, $script:XdgDataDirectory, $script:XdgStateDirectory,
        $script:LogsDirectory, $script:TempDirectory,
        $script:StagingDirectory, $script:ReceiptsDirectory, $script:QuarantineDirectory,
        (Join-Path $script:PythonCacheDirectory 'bytecode')
    )
    foreach ($directory in $directories) {
        Ensure-Directory -Path $directory
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Cannot hash missing file: $Path"
    }
    Assert-ExistingControlledPath -Path $Path
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-StreamSha256 {
    param([Parameter(Mandatory = $true)][System.IO.Stream]$Stream)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($Stream)
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Download-ImmutableFile {
    param(
        [Parameter(Mandatory = $true)][uri]$Uri,
        [Parameter(Mandatory = $true)][string]$ExpectedHost,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string]$ExpectedSha256
    )

    if (($Uri.Scheme -cne 'https') -or (-not [string]::Equals($Uri.Host, $ExpectedHost, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing unapproved download endpoint: $Uri"
    }
    [void](Assert-DescendantPath -Candidate $Destination -Root $script:DownloadsDirectory -Purpose 'Download destination')

    if (Test-Path -LiteralPath $Destination) {
        if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
            throw "Download destination is not a file: $Destination"
        }
        Assert-ExistingControlledPath -Path $Destination
        if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
            $existingHash = Get-FileSha256 -Path $Destination
            if ($existingHash -cne $ExpectedSha256.ToLowerInvariant()) {
                throw "Existing immutable download has an unexpected SHA256 and will not be overwritten: $Destination"
            }
        }
        return $Destination
    }

    $partialPath = "$Destination.partial.$([Guid]::NewGuid().ToString('N'))"
    [void](Assert-DescendantPath -Candidate $partialPath -Root $script:DownloadsDirectory -Purpose 'Partial download')
    try {
        $previousProtocol = [System.Net.ServicePointManager]::SecurityProtocol
        try {
            [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
            $null = Invoke-WebRequest -Uri $Uri.AbsoluteUri -OutFile $partialPath -UseBasicParsing -MaximumRedirection 0
        }
        finally {
            [System.Net.ServicePointManager]::SecurityProtocol = $previousProtocol
        }

        if (-not (Test-Path -LiteralPath $partialPath -PathType Leaf)) {
            throw "Download did not produce a file: $Uri"
        }
        if ((Get-Item -LiteralPath $partialPath).Length -le 0) {
            throw "Download was empty: $Uri"
        }
        if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
            $downloadHash = Get-FileSha256 -Path $partialPath
            if ($downloadHash -cne $ExpectedSha256.ToLowerInvariant()) {
                throw "Downloaded SHA256 mismatch for $Uri. Expected $ExpectedSha256; received $downloadHash"
            }
        }
        Assert-ExistingControlledPath -Path $partialPath
        if (Test-Path -LiteralPath $Destination) {
            throw "Immutable download destination appeared during transfer and will not be overwritten: $Destination"
        }
        Move-Item -LiteralPath $partialPath -Destination $Destination
        return $Destination
    }
    finally {
        if (Test-Path -LiteralPath $partialPath) {
            Remove-Item -LiteralPath $partialPath -Force
        }
    }
}

function Assert-UvDownloads {
    [void](Download-ImmutableFile -Uri ([uri]$script:UvChecksumUrl) -ExpectedHost 'releases.astral.sh' -Destination $script:UvChecksumPath)
    Assert-ExistingControlledPath -Path $script:UvChecksumPath
    $sidecar = Get-Content -LiteralPath $script:UvChecksumPath -Raw
    $match = [System.Text.RegularExpressions.Regex]::Match($sidecar, '(?im)^\s*([a-f0-9]{64})(?:\s+.*)?$')
    if (-not $match.Success) {
        throw "uv checksum sidecar is not a recognized SHA256 file: $($script:UvChecksumPath)"
    }
    $publisherHash = $match.Groups[1].Value.ToLowerInvariant()
    if ($publisherHash -cne $script:UvArchiveSha256) {
        throw "uv publisher sidecar disagrees with the reviewed SHA256. Expected $($script:UvArchiveSha256); received $publisherHash"
    }

    [void](Download-ImmutableFile -Uri ([uri]$script:UvArchiveUrl) -ExpectedHost 'releases.astral.sh' -Destination $script:UvArchivePath -ExpectedSha256 $script:UvArchiveSha256)
    $archiveHash = Get-FileSha256 -Path $script:UvArchivePath
    if ($archiveHash -cne $script:UvArchiveSha256) {
        throw "uv archive SHA256 mismatch. Expected $($script:UvArchiveSha256); received $archiveHash"
    }
}

function Get-ValidatedUvArchiveContents {
    if (-not (Test-Path -LiteralPath $script:UvArchivePath -PathType Leaf)) {
        throw "Verified uv archive is missing: $($script:UvArchivePath)"
    }
    if ((Get-FileSha256 -Path $script:UvArchivePath) -cne $script:UvArchiveSha256) {
        throw 'uv archive no longer matches its reviewed SHA256.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($script:UvArchivePath)
    $entryNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $requiredNames = @('uv.exe', 'uvx.exe', 'uvw.exe')
    $requiredCounts = @{}
    $binaryHashes = @{}
    foreach ($name in $requiredNames) {
        $requiredCounts[$name] = 0
    }

    try {
        foreach ($entry in $archive.Entries) {
            $normalized = $entry.FullName.Replace('\', '/')
            if ([string]::IsNullOrWhiteSpace($normalized)) {
                throw 'uv archive contains an empty entry name.'
            }
            if ($normalized.StartsWith('/') -or $normalized.StartsWith('\') -or [System.IO.Path]::IsPathRooted($normalized) -or $normalized.Contains(':')) {
                throw "uv archive contains a rooted or drive-qualified path: $normalized"
            }
            $segments = $normalized.Split('/')
            for ($index = 0; $index -lt $segments.Count; $index++) {
                if ($segments[$index] -eq '..') {
                    throw "uv archive contains path traversal: $normalized"
                }
                if (($segments[$index].Length -eq 0) -and ($index -lt ($segments.Count - 1))) {
                    throw "uv archive contains an ambiguous empty path segment: $normalized"
                }
            }
            if (-not $entryNames.Add($normalized)) {
                throw "uv archive contains a duplicate path: $normalized"
            }

            $unixType = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            $dosAttributes = ($entry.ExternalAttributes -band 0xFFFF)
            if (($unixType -eq 0xA000) -or (($dosAttributes -band [int][System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw "uv archive contains a link/reparse entry: $normalized"
            }

            $destinationProbe = [System.IO.Path]::GetFullPath((Join-Path $script:StagingDirectory ($normalized.Replace('/', '\'))))
            [void](Assert-DescendantPath -Candidate $destinationProbe -Root $script:StagingDirectory -Purpose 'uv ZIP entry' -AllowRoot)

            foreach ($requiredName in $requiredNames) {
                if ([string]::Equals($normalized, $requiredName, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $requiredCounts[$requiredName]++
                    $stream = $entry.Open()
                    try {
                        $binaryHashes[$requiredName] = Get-StreamSha256 -Stream $stream
                    }
                    finally {
                        $stream.Dispose()
                    }
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($requiredName in $requiredNames) {
        if ($requiredCounts[$requiredName] -ne 1) {
            throw "uv archive must contain exactly one root-level $requiredName; found $($requiredCounts[$requiredName])."
        }
    }
    if ($entryNames.Count -ne $requiredNames.Count) {
        throw "Reviewed uv archive must contain only uv.exe, uvx.exe, and uvw.exe; found $($entryNames.Count) entries."
    }

    return [pscustomobject]@{
        BinaryHashes = $binaryHashes
        EntryCount = $entryNames.Count
    }
}

function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    [void](Assert-PathOnDDrive -Path $FilePath -Purpose 'Executable')
    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        throw "Executable is missing: $FilePath"
    }
    Assert-ExistingControlledPath -Path $FilePath
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 maps native stderr to its error stream. Keep it
        # capturable so a nonzero probe can be inspected instead of terminating.
        $ErrorActionPreference = 'Continue'
        $output = @(& $FilePath @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $text = ($output | ForEach-Object { $_.ToString() }) -join [System.Environment]::NewLine
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $text.Trim()
    }
}

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory = $true)][string]$Description
    )

    $result = Invoke-NativeCapture -FilePath $FilePath -ArgumentList $ArgumentList
    if ($result.ExitCode -ne 0) {
        throw "$Description failed with exit code $($result.ExitCode).`n$($result.Output)"
    }
    return $result.Output
}

function Assert-UvInstallation {
    param([Parameter(Mandatory = $true)]$ArchiveContents)

    if (-not (Test-Path -LiteralPath $script:UvDirectory -PathType Container)) {
        throw "uv $($script:UvVersion) is not installed at $($script:UvDirectory)"
    }
    Assert-NoReparsePoints -Path $script:UvDirectory -Root $script:ToolchainRoot

    $installedItems = @(Get-ChildItem -LiteralPath $script:UvDirectory -Force)
    if ($installedItems.Count -ne 3) {
        throw "uv installation directory contains unexpected files or directories and will not be executed: $($script:UvDirectory)"
    }
    foreach ($item in $installedItems) {
        if (($item.PSIsContainer) -or (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) -or ($item.Name -notin @('uv.exe', 'uvx.exe', 'uvw.exe'))) {
            throw "Unexpected uv installation entry will not be executed: $($item.FullName)"
        }
    }

    foreach ($name in @('uv.exe', 'uvx.exe', 'uvw.exe')) {
        $installedPath = Join-Path $script:UvDirectory $name
        Assert-NoReparsePoints -Path $installedPath -Root $script:ToolchainRoot
        if (-not (Test-Path -LiteralPath $installedPath -PathType Leaf)) {
            throw "uv installation is incomplete: $installedPath"
        }
        $installedHash = Get-FileSha256 -Path $installedPath
        $archiveHash = $ArchiveContents.BinaryHashes[$name]
        if ($installedHash -cne $archiveHash) {
            throw "Installed $name differs from the reviewed archive. Existing files will not be overwritten."
        }
    }

    $uvOutput = Invoke-CheckedNative -FilePath $script:UvExe -ArgumentList @('--version') -Description 'uv version check'
    if ($uvOutput -notmatch '^uv\s+0\.12\.2(?:\s|$)') {
        throw "Unexpected uv version output: $uvOutput"
    }
    $uvxOutput = Invoke-CheckedNative -FilePath $script:UvxExe -ArgumentList @('--version') -Description 'uvx version check'
    if ($uvxOutput -notmatch '0\.12\.2(?:\s|$)') {
        throw "Unexpected uvx version output: $uvxOutput"
    }

    return [pscustomobject]@{
        Uv = $uvOutput
        Uvx = $uvxOutput
    }
}

function Install-Uv {
    param([Parameter(Mandatory = $true)]$ArchiveContents)

    if (Test-Path -LiteralPath $script:UvDirectory) {
        [void](Assert-UvInstallation -ArchiveContents $ArchiveContents)
        return
    }

    $uniqueStaging = Join-Path $script:StagingDirectory "uv-$($script:UvVersion)-$([Guid]::NewGuid().ToString('N'))"
    $payloadDirectory = Join-Path $uniqueStaging 'payload'
    [void](Assert-DescendantPath -Candidate $uniqueStaging -Root $script:StagingDirectory -Purpose 'uv staging')
    [void](Assert-DescendantPath -Candidate $payloadDirectory -Root $uniqueStaging -Purpose 'uv extraction')
    try {
        Ensure-Directory -Path $uniqueStaging
        Ensure-Directory -Path $payloadDirectory
        [System.IO.Compression.ZipFile]::ExtractToDirectory($script:UvArchivePath, $payloadDirectory)

        foreach ($name in @('uv.exe', 'uvx.exe', 'uvw.exe')) {
            $extractedPath = Join-Path $payloadDirectory $name
            if (-not (Test-Path -LiteralPath $extractedPath -PathType Leaf)) {
                throw "uv extraction did not produce expected file: $extractedPath"
            }
            if ((Get-FileSha256 -Path $extractedPath) -cne $ArchiveContents.BinaryHashes[$name]) {
                throw "Extracted $name differs from its verified ZIP entry."
            }
        }

        if (Test-Path -LiteralPath $script:UvDirectory) {
            throw "uv destination appeared during installation and will not be overwritten: $($script:UvDirectory)"
        }
        Move-Item -LiteralPath $payloadDirectory -Destination $script:UvDirectory
    }
    finally {
        Remove-UniqueStagingDirectory -Path $uniqueStaging
    }

    [void](Assert-UvInstallation -ArchiveContents $ArchiveContents)
}

function Install-ManagedPython {
    Set-ProcessToolchainEnvironment -PythonDownloads 'manual'
    [void](Invoke-CheckedNative -FilePath $script:UvExe -ArgumentList @('python', 'install', $script:PythonVersion, '--no-registry', '--no-progress') -Description "managed CPython $($script:PythonVersion) installation")
    Set-ProcessToolchainEnvironment -PythonDownloads 'never'
    return Find-ManagedPython
}

function Find-ManagedPython {
    Set-ProcessToolchainEnvironment -PythonDownloads 'never'
    $findOutput = Invoke-CheckedNative -FilePath $script:UvExe -ArgumentList @('python', 'find', $script:PythonVersion) -Description "managed CPython $($script:PythonVersion) lookup"
    $paths = @()
    foreach ($line in ($findOutput -split "`r?`n")) {
        $candidate = $line.Trim().Trim('"')
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $paths += [System.IO.Path]::GetFullPath($candidate)
        }
    }
    $paths = @($paths | Select-Object -Unique)
    if ($paths.Count -ne 1) {
        throw "Expected one managed CPython $($script:PythonVersion) path from uv; received: $findOutput"
    }

    $pythonPath = Assert-DescendantPath -Candidate $paths[0] -Root $script:PythonDirectory -Purpose 'Managed Python executable'
    Assert-NoReparsePoints -Path $pythonPath -Root $script:ToolchainRoot
    $versionOutput = Invoke-CheckedNative -FilePath $pythonPath -ArgumentList @('-I', '-c', "import sys; print('.'.join(map(str, sys.version_info[:3])))") -Description 'managed Python version check'
    if ($versionOutput -cne $script:PythonVersion) {
        throw "Expected managed CPython $($script:PythonVersion); received $versionOutput from $pythonPath"
    }
    return $pythonPath
}

function Assert-StoredPyPiMetadata {
    if (-not (Test-Path -LiteralPath $script:PyPiJsonPath -PathType Leaf)) {
        throw "Stored PyPI metadata is missing: $($script:PyPiJsonPath)"
    }
    Assert-ExistingControlledPath -Path $script:PyPiJsonPath
    $metadata = Get-Content -LiteralPath $script:PyPiJsonPath -Raw | ConvertFrom-Json
    if (($metadata.info.name -cne 'mcpforunityserver') -or ($metadata.info.version -cne $script:McpVersion)) {
        throw 'PyPI metadata name/version does not match the reviewed MCP server release.'
    }
    if ($metadata.info.requires_python -cne '>=3.10') {
        throw "Unexpected MCP server Python requirement: $($metadata.info.requires_python)"
    }

    $wheelRecords = @($metadata.urls | Where-Object { $_.filename -ceq $script:McpWheelName })
    if ($wheelRecords.Count -ne 1) {
        throw "PyPI metadata must contain exactly one $($script:McpWheelName) record."
    }
    $wheel = $wheelRecords[0]
    if (($wheel.packagetype -cne 'bdist_wheel') -or [bool]$wheel.yanked) {
        throw 'Reviewed MCP wheel is not an active wheel release on PyPI.'
    }
    if (($wheel.url -cne $script:McpWheelUrl) -or ($wheel.digests.sha256 -cne $script:McpWheelSha256)) {
        throw 'PyPI metadata URL/SHA256 disagrees with the reviewed MCP wheel identity.'
    }
    if ($wheel.upload_time_iso_8601 -cne $script:McpWheelUploadTime) {
        throw "PyPI MCP wheel upload timestamp changed unexpectedly: $($wheel.upload_time_iso_8601)"
    }

    return $wheel
}

function Assert-PyPiMetadataAndWheel {
    [void](Download-ImmutableFile -Uri ([uri]$script:PyPiJsonUrl) -ExpectedHost 'pypi.org' -Destination $script:PyPiJsonPath)
    [void](Assert-StoredPyPiMetadata)

    [void](Download-ImmutableFile -Uri ([uri]$script:McpWheelUrl) -ExpectedHost 'files.pythonhosted.org' -Destination $script:McpWheelPath -ExpectedSha256 $script:McpWheelSha256)
    if ((Get-FileSha256 -Path $script:McpWheelPath) -cne $script:McpWheelSha256) {
        throw 'Downloaded MCP wheel no longer matches the reviewed SHA256.'
    }
}

function Get-SafeFileInventory {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Boundary
    )

    [void](Assert-DescendantPath -Candidate $Root -Root $Boundary -Purpose 'Inventory root' -AllowRoot)
    Assert-NoReparsePoints -Path $Root -Root $Boundary
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($Root)
    $files = @()
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point found during controlled inventory: $($item.FullName)"
            }
            if ($item.PSIsContainer) {
                $pending.Push($item.FullName)
            }
            else {
                $files += $item.FullName
            }
        }
    }
    return $files
}

function Remove-GeneratedPythonBytecodeCaches {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Boundary
    )

    $cacheDirectories = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($filePath in @(Get-SafeFileInventory -Root $Root -Boundary $Boundary)) {
        $file = Get-Item -LiteralPath $filePath -Force
        $parent = $file.Directory
        if ($null -eq $parent -or $parent.Name -cne '__pycache__') {
            continue
        }
        if ($file.Extension -cne '.pyc') {
            throw "Unexpected non-bytecode file exists in a generated Python cache directory: $filePath"
        }

        $match = [System.Text.RegularExpressions.Regex]::Match(
            $file.Name,
            '^(?<module>.+)\.cpython-\d+[a-z0-9_]*\.pyc$',
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
        if (-not $match.Success) {
            throw "Unrecognized Python bytecode-cache filename: $filePath"
        }
        $sourcePath = Join-Path $parent.Parent.FullName ($match.Groups['module'].Value + '.py')
        [void](Assert-DescendantPath -Candidate $sourcePath -Root $Boundary -Purpose 'Python bytecode source')
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Python bytecode cache has no reviewed source file: $filePath"
        }

        Remove-Item -LiteralPath $filePath -Force
        [void]$cacheDirectories.Add($parent.FullName)
    }

    foreach ($cacheDirectory in @($cacheDirectories | Sort-Object { $_.Length } -Descending)) {
        [void](Assert-DescendantPath -Candidate $cacheDirectory -Root $Boundary -Purpose 'Python bytecode cache directory')
        $remaining = @(Get-ChildItem -LiteralPath $cacheDirectory -Force)
        if ($remaining.Count -ne 0) {
            throw "Generated Python cache directory was not empty after bytecode cleanup: $cacheDirectory"
        }
        Remove-Item -LiteralPath $cacheDirectory -Force
        Write-Verbose "Removed generated Python bytecode cache: $cacheDirectory"
    }
}

function Assert-McpFilesMatchVerifiedWheel {
    param([Parameter(Mandatory = $true)][string]$EnvironmentDirectory)

    if ((Get-FileSha256 -Path $script:McpWheelPath) -cne $script:McpWheelSha256) {
        throw 'Cannot verify MCP environment because its reviewed wheel changed.'
    }
    $sitePackages = Join-Path $EnvironmentDirectory 'Lib\site-packages'
    if (-not (Test-Path -LiteralPath $sitePackages -PathType Container)) {
        throw "MCP site-packages directory is missing: $sitePackages"
    }
    Assert-NoReparsePoints -Path $sitePackages -Root $script:ToolchainRoot
    Remove-GeneratedPythonBytecodeCaches -Root $sitePackages -Boundary $sitePackages

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($script:McpWheelPath)
    $expectedFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $packageRoots = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $distInfo = "mcpforunityserver-$($script:McpVersion).dist-info"
    try {
        foreach ($entry in $archive.Entries) {
            $relative = $entry.FullName.Replace('\', '/')
            if ([string]::IsNullOrWhiteSpace($relative) -or $relative.StartsWith('/') -or $relative.Contains(':')) {
                throw "MCP wheel contains an unsafe path: $relative"
            }
            $segments = $relative.Split('/')
            if ($segments -contains '..') {
                throw "MCP wheel contains path traversal: $relative"
            }
            if ($segments[0].EndsWith('.data', [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "MCP wheel uses an unreviewed .data install layout: $relative"
            }
            $unixType = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            $dosAttributes = ($entry.ExternalAttributes -band 0xFFFF)
            if (($unixType -eq 0xA000) -or (($dosAttributes -band [int][System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw "MCP wheel contains a link/reparse entry: $relative"
            }
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }
            if (-not $expectedFiles.Add($relative)) {
                throw "MCP wheel contains a duplicate file path: $relative"
            }
            $installedPath = [System.IO.Path]::GetFullPath((Join-Path $sitePackages ($relative.Replace('/', '\'))))
            [void](Assert-DescendantPath -Candidate $installedPath -Root $sitePackages -Purpose 'Installed MCP wheel file')
            if (-not (Test-Path -LiteralPath $installedPath -PathType Leaf)) {
                throw "Installed MCP wheel file is missing: $installedPath"
            }
            # Wheel installers regenerate RECORD to describe the installed
            # layout. All executable/importable package files remain subject
            # to byte-for-byte comparison and the complete environment tree
            # (including regenerated RECORD) is bound by the final receipt.
            if (-not $relative.EndsWith('.dist-info/RECORD', [System.StringComparison]::OrdinalIgnoreCase)) {
                $stream = $entry.Open()
                try {
                    $wheelFileHash = Get-StreamSha256 -Stream $stream
                }
                finally {
                    $stream.Dispose()
                }
                if ((Get-FileSha256 -Path $installedPath) -cne $wheelFileHash) {
                    throw "Installed MCP file differs from the reviewed wheel: $installedPath"
                }
            }
            if (-not [string]::Equals($segments[0], $distInfo, [System.StringComparison]::OrdinalIgnoreCase)) {
                [void]$packageRoots.Add($segments[0])
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($rootName in $packageRoots) {
        $rootPath = Join-Path $sitePackages $rootName
        if (Test-Path -LiteralPath $rootPath -PathType Container) {
            foreach ($filePath in @(Get-SafeFileInventory -Root $rootPath -Boundary $sitePackages)) {
                $relative = $filePath.Substring($sitePackages.TrimEnd('\').Length + 1).Replace('\', '/')
                if (-not $expectedFiles.Contains($relative)) {
                    throw "Unexpected executable/importable file exists beside the reviewed MCP package: $filePath"
                }
            }
        }
    }

    $metadataPath = Join-Path $sitePackages "$distInfo\METADATA"
    $entryPointsPath = Join-Path $sitePackages "$distInfo\entry_points.txt"
    Assert-ExistingControlledPath -Path $metadataPath
    Assert-ExistingControlledPath -Path $entryPointsPath
    $metadataText = Get-Content -LiteralPath $metadataPath -Raw
    if (($metadataText -notmatch '(?m)^Name:\s*mcpforunityserver\s*$') -or ($metadataText -notmatch '(?m)^Version:\s*10\.1\.0\s*$')) {
        throw 'Installed MCP METADATA identity is not exactly mcpforunityserver 10.1.0.'
    }
    $entryPointsText = Get-Content -LiteralPath $entryPointsPath -Raw
    $entryMatch = [System.Text.RegularExpressions.Regex]::Match($entryPointsText, '(?m)^mcp-for-unity\s*=\s*(\S[^\r\n]*)$')
    if (-not $entryMatch.Success) {
        throw 'Reviewed MCP wheel has no mcp-for-unity console entry point.'
    }
    return $entryMatch.Groups[1].Value.Trim()
}

function Get-McpToolEnvironment {
    param([Parameter(Mandatory = $true)][string]$ManagedPythonPath)

    $environmentDirectory = Join-Path $script:ToolsDirectory 'mcpforunityserver'
    if (-not (Test-Path -LiteralPath $environmentDirectory)) {
        if (@(Get-ChildItem -LiteralPath $script:ToolBinDirectory -Force).Count -gt 0) {
            throw "Orphaned files exist in the MCP launcher directory: $($script:ToolBinDirectory)"
        }
        return $null
    }
    if (-not (Test-Path -LiteralPath $environmentDirectory -PathType Container)) {
        throw "MCP tool environment path is not a directory: $environmentDirectory"
    }
    Assert-NoReparsePoints -Path $environmentDirectory -Root $script:ToolchainRoot

    $pythonPath = Join-Path $environmentDirectory 'Scripts\python.exe'
    if (-not (Test-Path -LiteralPath $pythonPath -PathType Leaf)) {
        throw "MCP tool Python is missing: $pythonPath"
    }
    Assert-NoReparsePoints -Path $pythonPath -Root $script:ToolchainRoot

    $venvConfigPath = Join-Path $environmentDirectory 'pyvenv.cfg'
    if (-not (Test-Path -LiteralPath $venvConfigPath -PathType Leaf)) {
        throw "MCP tool virtual-environment configuration is missing: $venvConfigPath"
    }
    Assert-NoReparsePoints -Path $venvConfigPath -Root $script:ToolchainRoot
    $venvValues = @{}
    foreach ($line in @(Get-Content -LiteralPath $venvConfigPath)) {
        if ($line -match '^\s*([^=]+?)\s*=\s*(.*?)\s*$') {
            $venvValues[$matches[1].Trim()] = $matches[2].Trim()
        }
    }
    $expectedPythonHome = [System.IO.Path]::GetDirectoryName($ManagedPythonPath)
    if (-not [string]::Equals($venvValues['home'], $expectedPythonHome, [System.StringComparison]::OrdinalIgnoreCase) -or
        $venvValues['implementation'] -cne 'CPython' -or
        $venvValues['uv'] -cne $script:UvVersion -or
        $venvValues['version_info'] -cne $script:PythonVersion -or
        $venvValues['include-system-site-packages'] -cne 'false') {
        throw 'MCP tool pyvenv.cfg does not bind the exact reviewed uv-managed CPython runtime.'
    }

    $launcherItems = @(Get-ChildItem -LiteralPath $script:ToolBinDirectory -Force)
    $launcherNames = @($launcherItems | ForEach-Object { $_.Name } | Sort-Object)
    if (($launcherItems.Count -ne 2) -or
        @($launcherItems | Where-Object { $_.PSIsContainer -or (($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) }).Count -gt 0 -or
        ($launcherNames -join '|') -cne 'mcp-for-unity.exe|unity-mcp.exe') {
        throw "MCP launcher directory must contain only the two reviewed wheel entry points: $($script:ToolBinDirectory)"
    }
    Assert-NoReparsePoints -Path $script:McpEntrypointPath -Root $script:ToolchainRoot
    Assert-NoReparsePoints -Path $script:McpSecondaryEntrypointPath -Root $script:ToolchainRoot
    $entrypoint = Assert-McpFilesMatchVerifiedWheel -EnvironmentDirectory $environmentDirectory

    return [pscustomobject]@{
        Directory = $environmentDirectory
        Python = $pythonPath
        Version = $script:McpVersion
        Entrypoint = $entrypoint
    }
}

function Get-DirectoryTreeIdentity {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $records = @()
    foreach ($filePath in @(Get-SafeFileInventory -Root $root -Boundary $script:ToolchainRoot)) {
        $relative = $filePath.Substring($root.Length + 1).Replace('\', '/')
        $records += [pscustomobject]@{
            Relative = $relative
            Length = (Get-Item -LiteralPath $filePath -Force).Length
            Sha256 = Get-FileSha256 -Path $filePath
        }
    }
    $records = @($records | Sort-Object -Property Relative)
    $builder = New-Object System.Text.StringBuilder
    foreach ($record in $records) {
        [void]$builder.Append($record.Relative)
        [void]$builder.Append('|')
        [void]$builder.Append($record.Length)
        [void]$builder.Append('|')
        [void]$builder.Append($record.Sha256)
        [void]$builder.Append("`n")
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digest = ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
    return [pscustomobject]@{
        FileCount = $records.Count
        Sha256 = $digest
    }
}

function New-McpReceipt {
    param([Parameter(Mandatory = $true)]$McpTool)

    if (Test-Path -LiteralPath $script:McpReceiptPath) {
        throw "MCP receipt already exists and will not be overwritten: $($script:McpReceiptPath)"
    }
    $environmentTree = Get-DirectoryTreeIdentity -Directory $McpTool.Directory
    $receipt = [ordered]@{
        schema = 1
        mcpVersion = $script:McpVersion
        pythonVersion = $script:PythonVersion
        wheelSha256 = $script:McpWheelSha256
        launcherPath = $script:McpEntrypointPath
        launcherSha256 = Get-FileSha256 -Path $script:McpEntrypointPath
        secondaryLauncherPath = $script:McpSecondaryEntrypointPath
        secondaryLauncherSha256 = Get-FileSha256 -Path $script:McpSecondaryEntrypointPath
        environmentPath = $McpTool.Directory
        environmentPythonSha256 = Get-FileSha256 -Path $McpTool.Python
        environmentFileCount = $environmentTree.FileCount
        environmentTreeSha256 = $environmentTree.Sha256
        entrypoint = $McpTool.Entrypoint
    }
    $partialPath = "$($script:McpReceiptPath).partial.$([Guid]::NewGuid().ToString('N'))"
    [void](Assert-DescendantPath -Candidate $partialPath -Root $script:ReceiptsDirectory -Purpose 'MCP receipt staging')
    try {
        $receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $partialPath -Encoding UTF8
        Assert-ExistingControlledPath -Path $partialPath
        if (Test-Path -LiteralPath $script:McpReceiptPath) {
            throw "MCP receipt destination appeared during creation and will not be overwritten: $($script:McpReceiptPath)"
        }
        Move-Item -LiteralPath $partialPath -Destination $script:McpReceiptPath
    }
    finally {
        if (Test-Path -LiteralPath $partialPath) {
            Remove-Item -LiteralPath $partialPath -Force
        }
    }
}

function Assert-McpReceipt {
    param([Parameter(Mandatory = $true)]$McpTool)

    Assert-ExistingControlledPath -Path $script:McpReceiptPath
    $receipt = Get-Content -LiteralPath $script:McpReceiptPath -Raw | ConvertFrom-Json
    if (($receipt.schema -ne 1) -or ($receipt.mcpVersion -cne $script:McpVersion) -or
        ($receipt.pythonVersion -cne $script:PythonVersion) -or ($receipt.wheelSha256 -cne $script:McpWheelSha256) -or
        ($receipt.launcherPath -cne $script:McpEntrypointPath) -or
        ($receipt.secondaryLauncherPath -cne $script:McpSecondaryEntrypointPath) -or
        ($receipt.environmentPath -cne $McpTool.Directory) -or
        ($receipt.entrypoint -cne $McpTool.Entrypoint)) {
        throw 'MCP installation receipt identity does not match the reviewed toolchain.'
    }
    $environmentTree = Get-DirectoryTreeIdentity -Directory $McpTool.Directory
    if (($receipt.launcherSha256 -cne (Get-FileSha256 -Path $script:McpEntrypointPath)) -or
        ($receipt.secondaryLauncherSha256 -cne (Get-FileSha256 -Path $script:McpSecondaryEntrypointPath)) -or
        ($receipt.environmentPythonSha256 -cne (Get-FileSha256 -Path $McpTool.Python)) -or
        ([int]$receipt.environmentFileCount -ne $environmentTree.FileCount) -or
        ($receipt.environmentTreeSha256 -cne $environmentTree.Sha256)) {
        throw 'MCP launcher or installed environment tree changed after verified installation.'
    }
}

function Move-IncompleteMcpInstallToQuarantine {
    $environmentDirectory = Join-Path $script:ToolsDirectory 'mcpforunityserver'
    $hasEnvironment = Test-Path -LiteralPath $environmentDirectory
    $launcherItems = @(Get-ChildItem -LiteralPath $script:ToolBinDirectory -Force -ErrorAction SilentlyContinue)
    if (-not ($hasEnvironment -or $launcherItems.Count -gt 0)) {
        return
    }

    Ensure-Directory -Path $script:QuarantineDirectory
    $quarantinePath = Join-Path $script:QuarantineDirectory (
        ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')) + '-incomplete-mcp-' +
        [Guid]::NewGuid().ToString('N').Substring(0, 8))
    [void](Assert-DescendantPath -Candidate $quarantinePath -Root $script:QuarantineDirectory -Purpose 'MCP quarantine')
    New-Item -ItemType Directory -Path $quarantinePath | Out-Null

    if ($hasEnvironment) {
        Assert-ExistingControlledPath -Path $environmentDirectory
        Move-Item -LiteralPath $environmentDirectory -Destination (Join-Path $quarantinePath 'mcpforunityserver-environment')
    }
    foreach ($launcherItem in $launcherItems) {
        if ($launcherItem.PSIsContainer -or (($launcherItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw "Refusing an unsafe interrupted MCP launcher entry: $($launcherItem.FullName)"
        }
        Assert-ExistingControlledPath -Path $launcherItem.FullName
        Move-Item -LiteralPath $launcherItem.FullName -Destination (Join-Path $quarantinePath $launcherItem.Name)
    }
    Write-Information "Moved an interrupted receipt-less MCP install to recoverable D-drive quarantine: $quarantinePath" -InformationAction Continue
}

function Install-McpTool {
    param([Parameter(Mandatory = $true)][string]$PythonPath)

    $environmentDirectory = Join-Path $script:ToolsDirectory 'mcpforunityserver'
    $hasEnvironment = Test-Path -LiteralPath $environmentDirectory
    $hasPrimaryLauncher = Test-Path -LiteralPath $script:McpEntrypointPath
    $hasSecondaryLauncher = Test-Path -LiteralPath $script:McpSecondaryEntrypointPath
    $hasAnyLauncher = @(Get-ChildItem -LiteralPath $script:ToolBinDirectory -Force -ErrorAction SilentlyContinue).Count -gt 0
    $hasReceipt = Test-Path -LiteralPath $script:McpReceiptPath
    if ($hasReceipt -and -not ($hasEnvironment -and $hasPrimaryLauncher -and $hasSecondaryLauncher)) {
        throw 'The MCP receipt exists but its environment or launcher is missing. Refusing to overwrite possible post-install tampering.'
    }
    if (-not $hasReceipt -and ($hasEnvironment -or $hasAnyLauncher)) {
        Move-IncompleteMcpInstallToQuarantine
    }

    $existing = Get-McpToolEnvironment -ManagedPythonPath $PythonPath
    if ($null -ne $existing) {
        Assert-McpReceipt -McpTool $existing
        return $existing
    }

    Set-ProcessToolchainEnvironment -PythonDownloads 'never'
    $arguments = @(
        'tool', 'install', $script:McpWheelPath,
        '--python', $PythonPath,
        '--no-build',
        '--exclude-newer', $script:DependencyCutoff
    )
    [void](Invoke-CheckedNative -FilePath $script:UvExe -ArgumentList $arguments -Description "mcpforunityserver $($script:McpVersion) tool installation")
    $installed = Get-McpToolEnvironment -ManagedPythonPath $PythonPath
    if ($null -eq $installed) {
        throw 'uv reported success but the MCP tool environment could not be verified.'
    }
    New-McpReceipt -McpTool $installed
    Assert-McpReceipt -McpTool $installed
    return $installed
}

function Get-Plan {
    return [ordered]@{
        action = 'plan'
        mutatesDisk = $false
        projectRoot = $script:ProjectRoot
        unityPackage = [ordered]@{
            name = 'com.coplaydev.unity-mcp'
            version = $script:McpVersion
            source = 'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#c14de1e6dc01ab42d2bb358730cff954bce0ce6b'
        }
        codexTransport = 'stdio'
        toolchainRoot = $script:ToolchainRoot
        uv = [ordered]@{
            version = $script:UvVersion
            archive = $script:UvArchiveUrl
            sha256 = $script:UvArchiveSha256
            installPath = $script:UvDirectory
        }
        python = [ordered]@{
            version = $script:PythonVersion
            managedOnly = $true
            installRoot = $script:PythonDirectory
            registryDisabled = $true
        }
        mcpServer = [ordered]@{
            version = $script:McpVersion
            wheel = $script:McpWheelUrl
            sha256 = $script:McpWheelSha256
            transport = 'stdio'
            telemetryDisabled = $true
        }
        prohibitedActions = @('Unity launch', 'MCP server launch', 'UAC elevation', 'shell-profile edits', 'system PATH edits', 'HTTP transport')
        nextAction = "Run this script with 'install' after reviewing this plan."
    }
}

function Invoke-Install {
    Assert-ProjectConfiguration
    Ensure-ToolchainDirectories
    Set-ProcessToolchainEnvironment -PythonDownloads 'never'

    Assert-UvDownloads
    $archiveContents = Get-ValidatedUvArchiveContents
    Install-Uv -ArchiveContents $archiveContents
    $uvStatus = Assert-UvInstallation -ArchiveContents $archiveContents

    $pythonPath = Install-ManagedPython
    Assert-PyPiMetadataAndWheel
    $mcpTool = Install-McpTool -PythonPath $pythonPath

    return [ordered]@{
        action = 'install'
        status = 'complete'
        projectRoot = $script:ProjectRoot
        unityPackagePin = 'v10.1.0'
        transport = 'stdio'
        uv = $uvStatus.Uv
        uvx = $uvStatus.Uvx
        managedPython = $pythonPath
        mcpServerVersion = $mcpTool.Version
        mcpToolEnvironment = $mcpTool.Directory
        mcpLauncher = $script:McpEntrypointPath
        toolchainRoot = $script:ToolchainRoot
        serverStarted = $false
        unityStarted = $false
        nextAction = 'Start the licensed Unity Editor once to resolve the pinned package, then restart/open a trusted Codex task from this D: project.'
    }
}

function Invoke-Verify {
    Assert-ProjectConfiguration
    if (-not (Test-Path -LiteralPath $script:ToolchainRoot -PathType Container)) {
        throw "D-only MCP toolchain is not installed: $($script:ToolchainRoot)"
    }
    Assert-NoReparsePoints -Path $script:ToolchainRoot -Root $script:ToolchainRoot
    Set-ProcessToolchainEnvironment -PythonDownloads 'never'

    if (-not (Test-Path -LiteralPath $script:UvChecksumPath -PathType Leaf)) {
        throw "uv checksum sidecar is missing: $($script:UvChecksumPath)"
    }
    Assert-ExistingControlledPath -Path $script:UvChecksumPath
    $sidecar = Get-Content -LiteralPath $script:UvChecksumPath -Raw
    $match = [System.Text.RegularExpressions.Regex]::Match($sidecar, '(?im)^\s*([a-f0-9]{64})(?:\s+.*)?$')
    if ((-not $match.Success) -or ($match.Groups[1].Value.ToLowerInvariant() -cne $script:UvArchiveSha256)) {
        throw 'Stored uv checksum sidecar does not match the reviewed SHA256.'
    }
    $archiveContents = Get-ValidatedUvArchiveContents
    $uvStatus = Assert-UvInstallation -ArchiveContents $archiveContents
    $pythonPath = Find-ManagedPython

    [void](Assert-StoredPyPiMetadata)
    if (-not (Test-Path -LiteralPath $script:McpWheelPath -PathType Leaf)) {
        throw "Stored MCP wheel is missing: $($script:McpWheelPath)"
    }
    if ((Get-FileSha256 -Path $script:McpWheelPath) -cne $script:McpWheelSha256) {
        throw 'Stored MCP wheel does not match the reviewed SHA256.'
    }
    $mcpTool = Get-McpToolEnvironment -ManagedPythonPath $pythonPath
    if ($null -eq $mcpTool) {
        throw "mcpforunityserver $($script:McpVersion) is not installed."
    }
    Assert-McpReceipt -McpTool $mcpTool

    return [ordered]@{
        action = 'verify'
        status = 'verified'
        projectConfiguration = 'exact'
        unityPackagePin = 'v10.1.0'
        transport = 'stdio'
        uv = $uvStatus.Uv
        uvx = $uvStatus.Uvx
        managedPython = $pythonPath
        mcpServerVersion = $mcpTool.Version
        mcpToolEnvironment = $mcpTool.Directory
        mcpLauncher = $script:McpEntrypointPath
        allControlledStateRoot = $script:ToolchainRoot
        serverStarted = $false
        unityStarted = $false
    }
}

Assert-ControlledPaths
$locationWasChanged = $false
try {
    Push-Location -LiteralPath $script:ProjectRoot
    $locationWasChanged = $true
    switch ($Action) {
        'plan' {
            Assert-ProjectConfiguration
            Get-Plan | ConvertTo-Json -Depth 8
        }
        'install' {
            Invoke-Install | ConvertTo-Json -Depth 8
        }
        'verify' {
            Invoke-Verify | ConvertTo-Json -Depth 8
        }
    }
}
finally {
    if ($locationWasChanged) {
        Pop-Location
    }
}
