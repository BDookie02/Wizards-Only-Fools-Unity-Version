[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'install', 'verify')]
    [string]$Action = 'plan',

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:ReleaseTag = 'v2.55.0.windows.3'
$script:GitVersion = '2.55.0.windows.3'
$script:DistributionVersion = '2.55.0.3'
$script:ArchiveName = 'PortableGit-2.55.0.3-64-bit.7z.exe'
$script:ArchiveUrl = 'https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.3/PortableGit-2.55.0.3-64-bit.7z.exe'
$script:ArchiveBytes = 58919776L
$script:ArchiveSha256 = 'ab00566336b5472120f9a52d34f2e79c5406535792acb0548001ffd0bd090e5d'

$script:InstallParent = 'D:\UnityTools\GitForWindows'
$script:InstallRoot = 'D:\UnityTools\GitForWindows\2.55.0.3'
$script:InstallerRoot = 'D:\UnityInstallers\GitForWindows\2.55.0.3'
$script:ArchivePath = Join-Path $script:InstallerRoot $script:ArchiveName
$script:PartialPath = "$($script:ArchivePath).partial"
$script:DownloadQuarantineRoot = Join-Path $script:InstallerRoot 'quarantine'
$script:StagingRoot = Join-Path $script:InstallParent '.staging'
$script:PayloadQuarantineRoot = Join-Path $script:InstallParent '.quarantine'
$script:ReceiptName = '.wof-portable-git-receipt.json'
$script:ProfileRoot = 'D:\UnityEditorProfile\Git'
$script:RunLockPath = Join-Path $script:InstallerRoot '.setup-portable-git.lock'

$script:ExpectedFiles = @(
    'cmd\git.exe',
    'bin\git.exe',
    'mingw64\bin\git.exe',
    'usr\bin\bash.exe',
    'usr\bin\false.exe',
    'usr\bin\ssh.exe',
    'mingw64\libexec\git-core\git-remote-https.exe',
    'git-bash.exe',
    'git-cmd.exe',
    'etc\gitconfig'
)

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
    return $fullPath
}

function Assert-DescendantPath {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Purpose,
        [switch]$AllowRoot
    )

    $candidatePath = Assert-DDrivePath -Path $Candidate -Purpose $Purpose
    $rootPath = (Assert-DDrivePath -Path $Root -Purpose "$Purpose root").TrimEnd('\')
    $isRoot = [string]::Equals(
        $candidatePath.TrimEnd('\'),
        $rootPath,
        [System.StringComparison]::OrdinalIgnoreCase)
    $isChild = $candidatePath.StartsWith(
        "$rootPath\",
        [System.StringComparison]::OrdinalIgnoreCase)
    if ((-not $isChild) -and (-not ($AllowRoot -and $isRoot))) {
        throw "$Purpose escaped its controlled D: root. Candidate: $candidatePath; root: $rootPath"
    }
    return $candidatePath
}

function Assert-NoReparsePointsInPath {
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

function Ensure-ControlledDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Purpose
    )

    $fullPath = Assert-NoReparsePointsInPath -Path $Path -Purpose $Purpose
    if (-not (Test-Path -LiteralPath $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "$Purpose is not a directory: $fullPath"
    }
    [void](Assert-NoReparsePointsInPath -Path $fullPath -Purpose $Purpose)
    return $fullPath
}

function Assert-ControlledPaths {
    foreach ($path in @(
        $script:InstallParent,
        $script:InstallRoot,
        $script:InstallerRoot,
        $script:ArchivePath,
        $script:PartialPath,
        $script:DownloadQuarantineRoot,
        $script:StagingRoot,
        $script:PayloadQuarantineRoot,
        $script:ProfileRoot,
        $script:RunLockPath
    )) {
        [void](Assert-DDrivePath -Path $path -Purpose 'Portable Git controlled path')
    }

    [void](Assert-DescendantPath -Candidate $script:InstallRoot -Root $script:InstallParent -Purpose 'Portable Git installation')
    [void](Assert-DescendantPath -Candidate $script:StagingRoot -Root $script:InstallParent -Purpose 'Portable Git staging')
    [void](Assert-DescendantPath -Candidate $script:PayloadQuarantineRoot -Root $script:InstallParent -Purpose 'Portable Git payload quarantine')
    foreach ($path in @(
        $script:ArchivePath,
        $script:PartialPath,
        $script:DownloadQuarantineRoot,
        $script:RunLockPath
    )) {
        [void](Assert-DescendantPath -Candidate $path -Root $script:InstallerRoot -Purpose 'Portable Git installer state')
    }

    foreach ($path in @($script:InstallParent, $script:InstallerRoot, $script:ProfileRoot)) {
        [void](Assert-NoReparsePointsInPath -Path $path -Purpose 'Portable Git controlled path')
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-StringSha256 {
    param([Parameter(Mandatory = $true)][string]$Value)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-SafePayloadIdentity {
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootPath = Assert-NoReparsePointsInPath -Path $Root -Purpose 'Portable Git payload root'
    if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
        throw "Portable Git payload root is missing: $rootPath"
    }

    $records = [System.Collections.Generic.List[string]]::new()
    $directories = [System.Collections.Generic.Stack[string]]::new()
    $directories.Push($rootPath)
    $fileCount = 0
    $directoryCount = 0
    $totalBytes = 0L

    while ($directories.Count -gt 0) {
        $directoryPath = $directories.Pop()
        $directoryItem = Get-Item -LiteralPath $directoryPath -Force
        if (($directoryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Portable Git payload contains a reparse directory: $directoryPath"
        }

        foreach ($item in @(Get-ChildItem -LiteralPath $directoryPath -Force)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Portable Git payload contains a reparse entry: $($item.FullName)"
            }
            $fullName = [System.IO.Path]::GetFullPath($item.FullName)
            [void](Assert-DescendantPath -Candidate $fullName -Root $rootPath -Purpose 'Portable Git payload entry')
            $relative = $fullName.Substring($rootPath.TrimEnd('\').Length + 1).Replace('\', '/')
            if ($item.PSIsContainer) {
                $records.Add("D|$relative")
                $directoryCount++
                $directories.Push($fullName)
                continue
            }
            if (-not [string]::Equals($relative, $script:ReceiptName, [System.StringComparison]::OrdinalIgnoreCase)) {
                $length = [long]$item.Length
                $hash = Get-FileSha256 -Path $fullName
                $records.Add("F|$relative|$length|$hash")
                $fileCount++
                $totalBytes += $length
            }
        }
    }

    $orderedRecords = $records.ToArray()
    [System.Array]::Sort($orderedRecords, [System.StringComparer]::Ordinal)
    $identityText = if ($orderedRecords.Count -eq 0) {
        ''
    }
    else {
        ([string]::Join("`n", $orderedRecords) + "`n")
    }
    return [pscustomobject]@{
        FileCount = $fileCount
        DirectoryCount = $directoryCount
        TotalBytes = $totalBytes
        TreeSha256 = Get-StringSha256 -Value $identityText
    }
}

function Invoke-GitVersion {
    param([Parameter(Mandatory = $true)][string]$GitExe)

    $gitPath = Assert-DDrivePath -Path $GitExe -Purpose 'Portable Git executable'
    if (-not (Test-Path -LiteralPath $gitPath -PathType Leaf)) {
        throw "Portable Git executable is missing: $gitPath"
    }
    [void](Assert-NoReparsePointsInPath -Path $gitPath -Purpose 'Portable Git executable')

    $saved = @{}
    foreach ($name in @(
        'HOME', 'XDG_CONFIG_HOME', 'GIT_CONFIG_NOSYSTEM', 'GIT_CONFIG_GLOBAL',
        'GIT_CONFIG_SYSTEM', 'GIT_TERMINAL_PROMPT', 'GCM_INTERACTIVE', 'GCM_GUI_PROMPT'
    )) {
        $saved[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process')
    }
    try {
        $env:HOME = $script:ProfileRoot
        $env:XDG_CONFIG_HOME = Join-Path $script:ProfileRoot 'xdg\config'
        $env:GIT_CONFIG_NOSYSTEM = '1'
        $env:GIT_CONFIG_GLOBAL = Join-Path $script:ProfileRoot '.gitconfig'
        $env:GIT_CONFIG_SYSTEM = Join-Path $script:ProfileRoot 'system.gitconfig'
        $env:GIT_TERMINAL_PROMPT = '0'
        $env:GCM_INTERACTIVE = 'Never'
        $env:GCM_GUI_PROMPT = '0'
        $output = @(& $gitPath --version 2>&1 | ForEach-Object { [string]$_ })
        $exitCode = $LASTEXITCODE
    }
    finally {
        foreach ($name in $saved.Keys) {
            [System.Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }

    if ($exitCode -ne 0) {
        throw "Portable Git version command failed with exit code $exitCode`: $($output -join ' ')"
    }
    $versionText = ($output -join "`n").Trim()
    $expected = "git version $($script:GitVersion)"
    if ($versionText -cne $expected) {
        throw "Portable Git identity mismatch. Expected '$expected'; found '$versionText'."
    }
    return $versionText
}

function Assert-ExpectedPayloadFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootPath = Assert-NoReparsePointsInPath -Path $Root -Purpose 'Portable Git payload root'
    foreach ($relativePath in $script:ExpectedFiles) {
        $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $rootPath $relativePath))
        [void](Assert-DescendantPath -Candidate $expectedPath -Root $rootPath -Purpose 'Portable Git expected file')
        if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
            throw "Portable Git expected file is missing: $expectedPath"
        }
        [void](Assert-NoReparsePointsInPath -Path $expectedPath -Purpose 'Portable Git expected file')
    }
    return Invoke-GitVersion -GitExe (Join-Path $rootPath 'cmd\git.exe')
}

function Write-ReceiptInsideStaging {
    param([Parameter(Mandatory = $true)][string]$StagingDirectory)

    $stagingPath = Assert-DescendantPath -Candidate $StagingDirectory -Root $script:StagingRoot -Purpose 'Portable Git receipt staging'
    $receiptPath = Join-Path $stagingPath $script:ReceiptName
    if (Test-Path -LiteralPath $receiptPath) {
        throw "Portable Git staging unexpectedly already contains a receipt: $receiptPath"
    }

    $versionText = Assert-ExpectedPayloadFiles -Root $stagingPath
    $payload = Get-SafePayloadIdentity -Root $stagingPath
    $receipt = [ordered]@{
        schema = 1
        releaseTag = $script:ReleaseTag
        distributionVersion = $script:DistributionVersion
        gitVersionOutput = $versionText
        artifactUrl = $script:ArchiveUrl
        artifactBytes = $script:ArchiveBytes
        artifactSha256 = $script:ArchiveSha256
        installRoot = $script:InstallRoot
        primaryGitRelativePath = 'cmd\git.exe'
        payloadFileCount = $payload.FileCount
        payloadDirectoryCount = $payload.DirectoryCount
        payloadBytes = $payload.TotalBytes
        payloadTreeSha256 = $payload.TreeSha256
        createdUtc = [DateTime]::UtcNow.ToString('o')
    }
    $temporaryReceipt = "$receiptPath.partial.$([Guid]::NewGuid().ToString('N'))"
    [void](Assert-DescendantPath -Candidate $temporaryReceipt -Root $stagingPath -Purpose 'Portable Git temporary receipt')
    try {
        $json = $receipt | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($temporaryReceipt, $json, [System.Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $receiptPath) {
            throw "Portable Git receipt destination appeared during creation: $receiptPath"
        }
        Move-Item -LiteralPath $temporaryReceipt -Destination $receiptPath
    }
    finally {
        if (Test-Path -LiteralPath $temporaryReceipt) {
            Remove-Item -LiteralPath $temporaryReceipt -Force
        }
    }
}

function Assert-InstalledPayload {
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootPath = Assert-NoReparsePointsInPath -Path $Root -Purpose 'Portable Git installation'
    if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
        throw "Portable Git is not installed: $rootPath"
    }
    $receiptPath = Join-Path $rootPath $script:ReceiptName
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw "Portable Git installation receipt is missing: $receiptPath"
    }
    [void](Assert-NoReparsePointsInPath -Path $receiptPath -Purpose 'Portable Git installation receipt')

    try {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Portable Git installation receipt is invalid JSON: $($_.Exception.Message)"
    }
    $expectedVersionOutput = "git version $($script:GitVersion)"
    if (($receipt.schema -ne 1) -or
        ($receipt.releaseTag -cne $script:ReleaseTag) -or
        ($receipt.distributionVersion -cne $script:DistributionVersion) -or
        ($receipt.gitVersionOutput -cne $expectedVersionOutput) -or
        ($receipt.artifactUrl -cne $script:ArchiveUrl) -or
        ([long]$receipt.artifactBytes -ne $script:ArchiveBytes) -or
        ($receipt.artifactSha256 -cne $script:ArchiveSha256) -or
        ($receipt.installRoot -cne $script:InstallRoot) -or
        ($receipt.primaryGitRelativePath -cne 'cmd\git.exe')) {
        throw 'Portable Git installation receipt does not match the pinned release identity.'
    }

    $versionText = Assert-ExpectedPayloadFiles -Root $rootPath
    if ($versionText -cne $receipt.gitVersionOutput) {
        throw 'Portable Git executable identity changed after installation.'
    }
    $payload = Get-SafePayloadIdentity -Root $rootPath
    if (([int]$receipt.payloadFileCount -ne $payload.FileCount) -or
        ([int]$receipt.payloadDirectoryCount -ne $payload.DirectoryCount) -or
        ([long]$receipt.payloadBytes -ne $payload.TotalBytes) -or
        ($receipt.payloadTreeSha256 -cne $payload.TreeSha256)) {
        throw 'Portable Git payload tree differs from its verified staging receipt.'
    }
    return [pscustomobject]@{
        GitExe = Join-Path $rootPath 'cmd\git.exe'
        Version = $script:GitVersion
        VersionOutput = $versionText
        PayloadFileCount = $payload.FileCount
        PayloadDirectoryCount = $payload.DirectoryCount
        PayloadBytes = $payload.TotalBytes
        PayloadTreeSha256 = $payload.TreeSha256
        Receipt = $receiptPath
    }
}

function Assert-Archive {
    if (-not (Test-Path -LiteralPath $script:ArchivePath -PathType Leaf)) {
        throw "Portable Git artifact is missing: $($script:ArchivePath)"
    }
    [void](Assert-NoReparsePointsInPath -Path $script:ArchivePath -Purpose 'Portable Git artifact')
    $length = (Get-Item -LiteralPath $script:ArchivePath -Force).Length
    if ($length -ne $script:ArchiveBytes) {
        throw "Portable Git artifact byte length mismatch. Expected $($script:ArchiveBytes); found $length."
    }
    $sha256 = Get-FileSha256 -Path $script:ArchivePath
    if ($sha256 -cne $script:ArchiveSha256) {
        throw "Portable Git artifact SHA-256 mismatch. Expected $($script:ArchiveSha256); found $sha256."
    }
    return $sha256
}

function Move-ToQuarantine {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$QuarantineRoot,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $fullPath = Assert-DDrivePath -Path $Path -Purpose 'Portable Git quarantine source'
    [void](Assert-NoReparsePointsInPath -Path $fullPath -Purpose 'Portable Git quarantine source')
    $quarantinePath = Ensure-ControlledDirectory -Path $QuarantineRoot -Purpose 'Portable Git quarantine'
    $name = '{0}-{1}-{2}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N').Substring(0, 8), [System.IO.Path]::GetFileName($fullPath)
    $destination = Join-Path $quarantinePath $name
    [void](Assert-DescendantPath -Candidate $destination -Root $quarantinePath -Purpose 'Portable Git quarantine destination')
    Move-Item -LiteralPath $fullPath -Destination $destination
    Write-Information "Moved invalid Portable Git state to recoverable D-drive quarantine: $destination ($Reason)" -InformationAction Continue
}

function Receive-ArtifactResponse {
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)][long]$StartAt
    )

    $mode = if ($StartAt -gt 0) { [System.IO.FileMode]::Append } else { [System.IO.FileMode]::Create }
    $output = [System.IO.FileStream]::new(
        $script:PartialPath,
        $mode,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None,
        1048576,
        [System.IO.FileOptions]::SequentialScan)
    $input = $Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    try {
        $buffer = New-Object byte[] 1048576
        $received = $StartAt
        while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $output.Write($buffer, 0, $read)
            $received += $read
            if ($received -gt $script:ArchiveBytes) {
                throw "Portable Git download exceeded the pinned byte length $($script:ArchiveBytes)."
            }
        }
        $output.Flush($true)
    }
    finally {
        $input.Dispose()
        $output.Dispose()
    }
}

function Invoke-Download {
    if (Test-Path -LiteralPath $script:ArchivePath) {
        try {
            [void](Assert-Archive)
            return
        }
        catch {
            Move-ToQuarantine -Path $script:ArchivePath -QuarantineRoot $script:DownloadQuarantineRoot -Reason $_.Exception.Message
        }
    }

    if (Test-Path -LiteralPath $script:PartialPath) {
        [void](Assert-NoReparsePointsInPath -Path $script:PartialPath -Purpose 'Portable Git partial artifact')
        $partialLength = (Get-Item -LiteralPath $script:PartialPath -Force).Length
        if ($partialLength -gt $script:ArchiveBytes) {
            Move-ToQuarantine -Path $script:PartialPath -QuarantineRoot $script:DownloadQuarantineRoot -Reason 'partial artifact exceeds pinned byte length'
        }
        elseif ($partialLength -eq $script:ArchiveBytes) {
            $partialHash = Get-FileSha256 -Path $script:PartialPath
            if ($partialHash -eq $script:ArchiveSha256) {
                Move-Item -LiteralPath $script:PartialPath -Destination $script:ArchivePath
                [void](Assert-Archive)
                return
            }
            Move-ToQuarantine -Path $script:PartialPath -QuarantineRoot $script:DownloadQuarantineRoot -Reason 'complete partial artifact failed pinned SHA-256 validation'
        }
    }

    $startAt = if (Test-Path -LiteralPath $script:PartialPath -PathType Leaf) {
        (Get-Item -LiteralPath $script:PartialPath -Force).Length
    }
    else {
        0L
    }

    Add-Type -AssemblyName System.Net.Http
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, [Uri]$script:ArchiveUrl)
    if ($startAt -gt 0) {
        $request.Headers.Range = [System.Net.Http.Headers.RangeHeaderValue]::new($startAt, $null)
    }
    try {
        $response = $client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        try {
            if ($startAt -gt 0 -and $response.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
                $startAt = 0L
            }
            elseif ($startAt -gt 0 -and $response.StatusCode -ne [System.Net.HttpStatusCode]::PartialContent) {
                throw "Portable Git server refused a safe resume: HTTP $([int]$response.StatusCode)."
            }
            elseif ($startAt -eq 0 -and -not $response.IsSuccessStatusCode) {
                throw "Portable Git download failed: HTTP $([int]$response.StatusCode)."
            }
            if ($startAt -gt 0) {
                $range = $response.Content.Headers.ContentRange
                if ($null -eq $range -or -not $range.HasRange -or $range.From -ne $startAt) {
                    throw 'Portable Git resume response did not begin at the requested immutable byte offset.'
                }
            }
            Receive-ArtifactResponse -Response $response -StartAt $startAt
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
        $handler.Dispose()
    }

    $completedLength = (Get-Item -LiteralPath $script:PartialPath -Force).Length
    if ($completedLength -ne $script:ArchiveBytes) {
        throw "Portable Git download is incomplete. Expected $($script:ArchiveBytes); found $completedLength. The D-drive partial was retained for resume."
    }
    $completedHash = Get-FileSha256 -Path $script:PartialPath
    if ($completedHash -ne $script:ArchiveSha256) {
        Move-ToQuarantine -Path $script:PartialPath -QuarantineRoot $script:DownloadQuarantineRoot -Reason 'downloaded artifact failed pinned SHA-256 validation'
        throw 'Portable Git download completed but failed the pinned SHA-256 validation.'
    }
    if (Test-Path -LiteralPath $script:ArchivePath) {
        [void](Assert-Archive)
        throw "Verified Portable Git artifact appeared concurrently; retained complete partial for review: $($script:PartialPath)"
    }
    Move-Item -LiteralPath $script:PartialPath -Destination $script:ArchivePath
    [void](Assert-Archive)
}

function Invoke-Install {
    if (Test-Path -LiteralPath $script:InstallRoot) {
        return Assert-InstalledPayload -Root $script:InstallRoot
    }

    Invoke-Download
    [void](Assert-Archive)
    $stagingDirectory = Join-Path $script:StagingRoot ("$($script:DistributionVersion)-" + [Guid]::NewGuid().ToString('N'))
    [void](Assert-DescendantPath -Candidate $stagingDirectory -Root $script:StagingRoot -Purpose 'Portable Git unique staging')
    if (Test-Path -LiteralPath $stagingDirectory) {
        throw "Portable Git unique staging collision: $stagingDirectory"
    }
    New-Item -ItemType Directory -Path $stagingDirectory | Out-Null

    $promoted = $false
    try {
        $process = Start-Process -FilePath $script:ArchivePath -ArgumentList @('-y', "-o$stagingDirectory") -WorkingDirectory $script:InstallerRoot -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "Portable Git SFX extraction failed with exit code $($process.ExitCode)."
        }
        [void](Assert-ExpectedPayloadFiles -Root $stagingDirectory)
        Write-ReceiptInsideStaging -StagingDirectory $stagingDirectory
        [void](Assert-InstalledPayload -Root $stagingDirectory)

        if (Test-Path -LiteralPath $script:InstallRoot) {
            throw "Portable Git installation destination appeared concurrently: $($script:InstallRoot)"
        }
        Move-Item -LiteralPath $stagingDirectory -Destination $script:InstallRoot
        $promoted = $true
    }
    catch {
        if ((-not $promoted) -and (Test-Path -LiteralPath $stagingDirectory)) {
            Move-ToQuarantine -Path $stagingDirectory -QuarantineRoot $script:PayloadQuarantineRoot -Reason $_.Exception.Message
        }
        throw
    }
    return Assert-InstalledPayload -Root $script:InstallRoot
}

function Enter-RunLock {
    $stream = [System.IO.File]::Open(
        $script:RunLockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    try {
        $stream.SetLength(0)
        $text = "pid=$PID`nstartedUtc=$([DateTime]::UtcNow.ToString('o'))`n"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
        return $stream
    }
    catch {
        $stream.Dispose()
        throw
    }
}

function Get-Plan {
    return [ordered]@{
        action = 'plan'
        mutatesDisk = $false
        releaseTag = $script:ReleaseTag
        artifact = [ordered]@{
            url = $script:ArchiveUrl
            bytes = $script:ArchiveBytes
            sha256 = $script:ArchiveSha256
            cachePath = $script:ArchivePath
            resumablePartialPath = $script:PartialPath
        }
        installRoot = $script:InstallRoot
        gitExe = Join-Path $script:InstallRoot 'cmd\git.exe'
        expectedVersionOutput = "git version $($script:GitVersion)"
        profileRoot = $script:ProfileRoot
        extraction = 'Pinned SFX with -y and a unique same-volume -o staging directory'
        promotion = 'Verified full-tree receipt is written in staging before same-volume directory promotion'
        requiresElevation = $false
        downloadStarted = $false
        installedNow = Test-Path -LiteralPath $script:InstallRoot -PathType Container
        nextAction = "Run this script with 'install -Apply' after reviewing this plan."
    }
}

Assert-ControlledPaths

switch ($Action) {
    'plan' {
        Get-Plan | ConvertTo-Json -Depth 6
    }
    'install' {
        if (-not $Apply) {
            throw "Action 'install' changes D-drive state. Re-run with -Apply after reviewing the plan."
        }
        foreach ($directory in @(
            $script:InstallParent,
            $script:InstallerRoot,
            $script:DownloadQuarantineRoot,
            $script:StagingRoot,
            $script:PayloadQuarantineRoot,
            $script:ProfileRoot,
            (Join-Path $script:ProfileRoot 'xdg\config'),
            (Join-Path $script:ProfileRoot 'xdg\cache'),
            (Join-Path $script:ProfileRoot 'xdg\data'),
            (Join-Path $script:ProfileRoot 'xdg\state')
        )) {
            [void](Ensure-ControlledDirectory -Path $directory -Purpose 'Portable Git installation directory')
        }
        $runLock = $null
        try {
            $runLock = Enter-RunLock
            Invoke-Install | ConvertTo-Json -Depth 5
        }
        finally {
            if ($null -ne $runLock) {
                $runLock.Dispose()
            }
        }
    }
    'verify' {
        $status = Assert-InstalledPayload -Root $script:InstallRoot
        [ordered]@{
            action = 'verify'
            status = 'verified'
            releaseTag = $script:ReleaseTag
            version = $status.Version
            versionOutput = $status.VersionOutput
            gitExe = $status.GitExe
            receipt = $status.Receipt
            payloadFileCount = $status.PayloadFileCount
            payloadDirectoryCount = $status.PayloadDirectoryCount
            payloadBytes = $status.PayloadBytes
            payloadTreeSha256 = $status.PayloadTreeSha256
            allControlledStateOnD = $true
        } | ConvertTo-Json -Depth 4
    }
}
