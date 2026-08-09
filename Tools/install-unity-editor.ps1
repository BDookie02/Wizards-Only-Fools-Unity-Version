[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'download', 'install', 'verify')]
    [string]$Action = 'plan',

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$targetVersion = '6000.3.21f1'
$targetChangeset = 'c02631ffc030'
$editorRoot = 'D:\UnityEditors\6000.3.21f1'
$installerRoot = 'D:\UnityInstallers'
$tempRoot = 'D:\UCT\editor'
$installerPath = Join-Path $installerRoot 'UnitySetup64-6000.3.21f1.exe'
$installerUrl = 'https://download.unity3d.com/download_unity/c02631ffc030/Windows64EditorInstaller/UnitySetup64-6000.3.21f1.exe'
$expectedBytes = 4092408560L
$expectedSha256 = 'CD9B72843DC7317DBAC847F5102B1812CA6E0B128325F2375F887FEF291AE5D3'
$expectedSignerThumbprint = '228FB6411B0A144478C86AAA3CD9473C43A8ABA7'

function Assert-DDrivePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals([System.IO.Path]::GetPathRoot($fullPath), 'D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing non-D path: $fullPath"
    }
    return $fullPath
}

function Assert-ApplyRequested {
    if (-not $Apply) {
        throw "Action '$Action' changes local state. Re-run with -Apply after reviewing the plan and applicable Unity license terms."
    }
}

function Test-EditorInstalled {
    $unity = Join-Path $editorRoot 'Editor\Unity.exe'
    $unityConsole = Join-Path $editorRoot 'Editor\Unity.com'
    if (-not ((Test-Path -LiteralPath $unity -PathType Leaf) -and
        (Test-Path -LiteralPath $unityConsole -PathType Leaf))) {
        return $false
    }

    $unityItem = Get-Item -LiteralPath $unity
    $productVersion = $unityItem.VersionInfo.ProductVersion
    if (-not [string]::Equals(
        $productVersion,
        "$targetVersion`_$targetChangeset",
        [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $unity
    return ($signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid -and
        $null -ne $signature.SignerCertificate -and
        [string]::Equals(
            $signature.SignerCertificate.Thumbprint,
            $expectedSignerThumbprint,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        $signature.SignerCertificate.Subject -match 'O=Unity Technologies SF')
}

function Confirm-Installer {
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Unity Editor installer is missing: $installerPath"
    }

    $length = (Get-Item -LiteralPath $installerPath).Length
    if ($length -ne $expectedBytes) {
        throw "Unity Editor installer byte length is $length; expected $expectedBytes."
    }

    $sha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    if (-not [string]::Equals($sha256, $expectedSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unity Editor installer SHA-256 mismatch. Expected $expectedSha256; found $sha256."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        -not [string]::Equals($signature.SignerCertificate.Thumbprint, $expectedSignerThumbprint, [System.StringComparison]::OrdinalIgnoreCase) -or
        $signature.SignerCertificate.Subject -notmatch 'O=Unity Technologies SF') {
        throw "Unity Editor installer Authenticode verification failed. Status=$($signature.Status) signer=$($signature.SignerCertificate.Subject)"
    }

    return $sha256
}

function Move-ToQuarantine {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    Assert-DDrivePath -Path $Path | Out-Null
    $quarantineRoot = Join-Path $installerRoot 'quarantine'
    New-Item -ItemType Directory -Force -Path $quarantineRoot | Out-Null
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
    $target = Join-Path $quarantineRoot ("$stamp-" + [System.IO.Path]::GetFileName($Path))
    Move-Item -LiteralPath $Path -Destination $target
    Write-Output "Quarantined invalid Unity Editor installer: $target ($Reason)"
}

function Invoke-Download {
    if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
        try {
            Confirm-Installer | Out-Null
            Write-Output "Verified cached Unity Editor installer: $installerPath"
            return
        }
        catch {
            Move-ToQuarantine -Path $installerPath -Reason $_.Exception.Message
        }
    }

    $partialPath = "$installerPath.partial"
    if (Test-Path -LiteralPath $partialPath -PathType Leaf) {
        $partialLength = (Get-Item -LiteralPath $partialPath).Length
        if ($partialLength -gt $expectedBytes) {
            Move-ToQuarantine -Path $partialPath -Reason 'partial file exceeds the pinned byte length'
        }
        elseif ($partialLength -eq $expectedBytes) {
            Move-Item -LiteralPath $partialPath -Destination $installerPath
            try {
                Confirm-Installer | Out-Null
                Write-Output "Promoted complete verified Unity Editor partial: $installerPath"
                return
            }
            catch {
                Move-ToQuarantine -Path $installerPath -Reason $_.Exception.Message
            }
        }
    }

    Add-Type -AssemblyName System.Net.Http
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, [Uri]$installerUrl)
    $startAt = if (Test-Path -LiteralPath $partialPath -PathType Leaf) {
        (Get-Item -LiteralPath $partialPath).Length
    }
    else {
        0L
    }
    if ($startAt -gt 0) {
        $request.Headers.Range = New-Object System.Net.Http.Headers.RangeHeaderValue($startAt, $null)
    }

    try {
        Write-Output "Downloading signed Unity $targetVersion Editor installer to D: (resume byte $startAt)."
        $response = $client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if ($startAt -gt 0 -and $response.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
            $startAt = 0L
        }
        elseif ($startAt -gt 0 -and $response.StatusCode -ne [System.Net.HttpStatusCode]::PartialContent) {
            throw "Server refused safe resume: HTTP $([int]$response.StatusCode)."
        }
        elseif ($startAt -eq 0 -and -not $response.IsSuccessStatusCode) {
            throw "Unity Editor download failed: HTTP $([int]$response.StatusCode)."
        }

        $fileMode = if ($startAt -gt 0) { [System.IO.FileMode]::Append } else { [System.IO.FileMode]::Create }
        $output = [System.IO.FileStream]::new($partialPath, $fileMode, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None, 1048576, [System.IO.FileOptions]::SequentialScan)
        $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        try {
            $buffer = New-Object byte[] 1048576
            $received = $startAt
            $lastReport = [DateTime]::UtcNow
            while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $output.Write($buffer, 0, $read)
                $received += $read
                if ($received -gt $expectedBytes) {
                    throw "Unity Editor download exceeded the pinned byte length $expectedBytes."
                }
                if (([DateTime]::UtcNow - $lastReport).TotalSeconds -ge 15) {
                    Write-Output ("  {0}% ({1:N1} MiB / {2:N1} MiB)" -f [Math]::Round(($received * 100.0) / $expectedBytes, 1), ($received / 1MB), ($expectedBytes / 1MB))
                    $lastReport = [DateTime]::UtcNow
                }
            }
        }
        finally {
            $input.Dispose()
            $output.Dispose()
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
        $handler.Dispose()
    }

    $completedLength = (Get-Item -LiteralPath $partialPath).Length
    if ($completedLength -ne $expectedBytes) {
        throw "Incomplete Unity Editor download. Expected $expectedBytes; found $completedLength. The D-drive partial was retained for resume."
    }

    Move-Item -LiteralPath $partialPath -Destination $installerPath
    Confirm-Installer | Out-Null
    Write-Output "Downloaded and verified signed Unity Editor installer: $installerPath"
}

function Invoke-Install {
    if (Test-EditorInstalled) {
        Write-Output "Exact Unity Editor is already installed: $editorRoot"
        return
    }

    Invoke-Download
    Write-Output 'The signed Unity Editor installer may require one manual Windows UAC approval.'
    $process = Start-Process -FilePath $installerPath -ArgumentList @('/S', "/D=$editorRoot") -WorkingDirectory $installerRoot -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Unity Editor installer failed or elevation was declined (exit $($process.ExitCode))."
    }
    if (-not (Test-EditorInstalled)) {
        throw "Installer exited successfully, but exact Editor identity was not found at $editorRoot"
    }
    Write-Output "Installed and identity-verified Unity $targetVersion ($targetChangeset) at $editorRoot"
}

foreach ($path in @($editorRoot, $installerRoot, $tempRoot, $installerPath)) {
    Assert-DDrivePath -Path $path | Out-Null
}
foreach ($path in @($installerRoot, $tempRoot)) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}
$env:TEMP = $tempRoot
$env:TMP = $tempRoot

switch ($Action) {
    'plan' {
        Write-Output "Unity Editor target: $targetVersion ($targetChangeset)"
        Write-Output "Official installer: $installerUrl"
        Write-Output "Installer path: $installerPath"
        Write-Output "Pinned bytes/SHA-256/signer: $expectedBytes / $expectedSha256 / $expectedSignerThumbprint"
        Write-Output "Exact Editor installed: $(Test-EditorInstalled)"
        Write-Output 'Plan only: no download, installer, elevation, or Unity process was started.'
    }
    'download' {
        Assert-ApplyRequested
        Invoke-Download
    }
    'install' {
        Assert-ApplyRequested
        Invoke-Install
    }
    'verify' {
        Confirm-Installer | Out-Null
        if (-not (Test-EditorInstalled)) {
            throw "Exact Unity Editor verification failed: $editorRoot"
        }
        Write-Output "Unity Editor installer and installed identity verification passed for $targetVersion ($targetChangeset)."
    }
}
