[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'download', 'elevate-executables', 'install', 'install-executables', 'install-zips', 'repair-executable-receipt', 'verify')]
    [string]$Action = 'plan',

    [ValidateSet('android', 'webgl', 'windows-il2cpp', 'windows-server')]
    [string]$RecoveryComponentId,

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Pinned to Unity's official Windows x86_64 release manifest for 6000.3.21f1.
# The four target-support EXEs have Unity-published MD5 digests. Google's
# command-line tools archive has a Unity-published SHA-384 digest. The other
# Android archives have no publisher digest in that manifest; on first use we
# therefore require exact HTTPS byte length plus per-entry ZIP CRC validation,
# then pin the observed SHA-256 locally for all subsequent runs.
$targetVersion = '6000.3.21f1'
$targetChangeset = 'c02631ffc030'
$editorRoot = 'D:\UnityEditors\6000.3.21f1'
$downloadRoot = 'D:\UnityInstallers\6000.3.21f1'
# Keep these roots deliberately short: the Windows Android NDK contains paths
# close to the legacy MAX_PATH boundary even before a staging prefix is added.
$stagingRoot = 'D:\UCS\3'
$tempRoot = 'D:\UCT\3'
$hashStatePath = Join-Path $downloadRoot 'verified-downloads.json'
$receiptPath = Join-Path $downloadRoot 'component-install-receipts.json'
$installLogPath = Join-Path $downloadRoot 'component-install.log'

function New-Component {
    param(
        [string]$Id,
        [ValidateSet('EXE', 'ZIP')][string]$Type,
        [string]$Url,
        [long]$DownloadBytes,
        [long]$InstalledBytes,
        [string]$Integrity,
        [string]$DestinationRelative,
        [string]$RenameFromRelative,
        [string]$RenameToRelative,
        [string]$PayloadRelative,
        [string]$MarkerRelative
    )

    [PSCustomObject]@{
        Id = $Id
        Type = $Type
        Url = $Url
        DownloadBytes = $DownloadBytes
        InstalledBytes = $InstalledBytes
        Integrity = $Integrity
        DestinationRelative = $DestinationRelative
        RenameFromRelative = $RenameFromRelative
        RenameToRelative = $RenameToRelative
        PayloadRelative = $PayloadRelative
        MarkerRelative = $MarkerRelative
    }
}

$components = @(
    New-Component -Id 'android' -Type EXE `
        -Url 'https://download.unity3d.com/download_unity/c02631ffc030/TargetSupportInstaller/UnitySetup-Android-Support-for-Editor-6000.3.21f1.exe' `
        -DownloadBytes 1543783160 -InstalledBytes 6879726193 `
        -Integrity 'md5-uzKO3QV58pq9cE0StUBfIg==' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\modules.asset'
    New-Component -Id 'webgl' -Type EXE `
        -Url 'https://download.unity3d.com/download_unity/c02631ffc030/TargetSupportInstaller/UnitySetup-WebGL-Support-for-Editor-6000.3.21f1.exe' `
        -DownloadBytes 929681312 -InstalledBytes 4433000041 `
        -Integrity 'md5-hnnzbXOTiRTGEIqd+3YHDg==' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\WebGLSupport\BuildTools'
    New-Component -Id 'windows-il2cpp' -Type EXE `
        -Url 'https://download.unity3d.com/download_unity/c02631ffc030/TargetSupportInstaller/UnitySetup-Windows-IL2CPP-Support-for-Editor-6000.3.21f1.exe' `
        -DownloadBytes 251397640 -InstalledBytes 898785320 `
        -Integrity 'md5-QsR4RTtwCuxQQVEo2aIuYQ=='
    New-Component -Id 'windows-server' -Type EXE `
        -Url 'https://download.unity3d.com/download_unity/c02631ffc030/TargetSupportInstaller/UnitySetup-Windows-Server-Support-for-Editor-6000.3.21f1.exe' `
        -DownloadBytes 455370624 -InstalledBytes 1812476756 `
        -Integrity 'md5-WHCjED/cDDESwJFnbpH6AA=='
    New-Component -Id 'android-open-jdk-17.0.18+8' -Type ZIP `
        -Url 'https://download.unity3d.com/download_unity/open-jdk/open-jdk-win-x64/jdk17.0.18-8_15e8817d1f5db6db3571ebe7430ef37f7fa8e60e8ff6f3e18ca1cb4c29f78774.zip' `
        -DownloadBytes 118110508 -InstalledBytes 239507431 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\java.exe'
    New-Component -Id 'android-ndk-r27c' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/android-ndk-r27c-windows.zip' `
        -DownloadBytes 781511249 -InstalledBytes 2361056613 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK' `
        -RenameFromRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK\android-ndk-r27c' `
        -RenameToRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK\ndk-build.cmd'
    New-Component -Id 'cmake-3.22.1' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/cmake-3.22.1-windows.zip' `
        -DownloadBytes 16116742 -InstalledBytes 39348489 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\cmake' `
        -RenameFromRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\cmake' `
        -RenameToRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe'
    New-Component -Id 'android-sdk-build-tools-36.0.0' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/build-tools_r36_windows.zip' `
        -DownloadBytes 58699878 -InstalledBytes 143241305 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools' `
        -RenameFromRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\android-16' `
        -RenameToRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt2.exe'
    New-Component -Id 'android-sdk-platform-tools-36.0.0' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/platform-tools_r36.0.0-win.zip' `
        -DownloadBytes 7138784 -InstalledBytes 15373885 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
    New-Component -Id 'android-sdk-platforms-34' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/platform-34-ext7_r02.zip' `
        -DownloadBytes 63180079 -InstalledBytes 99855424 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-34' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-34\android.jar'
    New-Component -Id 'android-sdk-platforms-35' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/platform-35_r01.zip' `
        -DownloadBytes 64281654 -InstalledBytes 102618237 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-35' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-35\android.jar'
    New-Component -Id 'android-sdk-platforms-36' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/platform-36_r02.zip' `
        -DownloadBytes 65878410 -InstalledBytes 106177284 -Integrity '' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-36' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platforms\android-36\android.jar'
    New-Component -Id 'android-sdk-command-line-tools-16.0' -Type ZIP `
        -Url 'https://dl.google.com/android/repository/commandlinetools-win-12266719_latest.zip' `
        -DownloadBytes 143481958 -InstalledBytes 165583436 `
        -Integrity 'sha384-3H6q0QmivqDyr88w9L8AsYO9yGt41w/Or/JYqclRY2ZbCh3kdguKuyAQYM6JwyWe' `
        -DestinationRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools' `
        -RenameFromRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\cmdline-tools' `
        -RenameToRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\16.0' `
        -PayloadRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\16.0' `
        -MarkerRelative 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\16.0\bin\sdkmanager.bat'
)

function Assert-DDrivePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals([System.IO.Path]::GetPathRoot($fullPath), 'D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing non-D path: $fullPath"
    }
    $fullPath
}

function Assert-WithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $fullPath = Assert-DDrivePath $Path
    $fullRoot = (Assert-DDrivePath $Root).TrimEnd('\')
    $prefix = $fullRoot + '\'
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside controlled root '$fullRoot': $fullPath"
    }
    $fullPath
}

function Initialize-DOnlyState {
    foreach ($path in @($downloadRoot, $stagingRoot, $tempRoot)) {
        $validated = Assert-DDrivePath $path
        New-Item -ItemType Directory -Force -Path $validated | Out-Null
    }

    # Applies only to this process and children. Windows elevation/licensing
    # services themselves are OS-managed and cannot be relocated here.
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    $env:UNITY_NO_UPDATE_CHECK = '1'
    $env:UNITY_NO_CONSENT_PROMPT = '1'
}

function Assert-ApplyRequested {
    if (-not $Apply) {
        throw "Action '$Action' changes local state. Re-run with -Apply after reviewing the plan."
    }
}

function Assert-ExactEditor {
    $unity = Join-Path $editorRoot 'Editor\Unity.exe'
    $unityConsole = Join-Path $editorRoot 'Editor\Unity.com'
    if (-not ((Test-Path -LiteralPath $unity -PathType Leaf) -and (Test-Path -LiteralPath $unityConsole -PathType Leaf))) {
        throw "Exact Unity Editor base install is missing from D: $editorRoot"
    }

    $actual = (Get-Item -LiteralPath $unity).VersionInfo.ProductVersion
    $expected = "$targetVersion`_$targetChangeset"
    if (-not [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unity Editor identity mismatch. Expected $expected; found $actual."
    }
}

function Get-DownloadPath {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)
    $fileName = [System.IO.Path]::GetFileName(([Uri]$Component.Url).AbsolutePath)
    Assert-WithinRoot -Path (Join-Path $downloadRoot $fileName) -Root $downloadRoot
}

function Read-JsonArray {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @()
    }
    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }
    $parsed = $raw | ConvertFrom-Json
    foreach ($item in @($parsed)) {
        # Windows PowerShell 5 can preserve a JSON array as a single wrapped
        # pipeline value. Flatten that legacy shape so state remains portable
        # between Windows PowerShell and PowerShell 7.
        $valueProperty = $item.PSObject.Properties['value']
        $idProperty = $item.PSObject.Properties['id']
        if ($null -eq $idProperty -and $null -ne $valueProperty -and $valueProperty.Value -is [System.Array]) {
            foreach ($nested in $valueProperty.Value) {
                Write-Output $nested
            }
        }
        else {
            Write-Output $item
        }
    }
}

function Write-JsonArray {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object[]]$Records
    )
    Assert-WithinRoot -Path $Path -Root $downloadRoot | Out-Null
    $json = if ($Records.Count -eq 0) { '[]' } else { ConvertTo-Json -InputObject $Records -Depth 8 }
    $temporaryPath = Join-Path (Split-Path -Parent $Path) ('.' + [System.IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $backupPath = Join-Path (Split-Path -Parent $Path) ('.' + [System.IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.bak')
    Assert-WithinRoot -Path $temporaryPath -Root $downloadRoot | Out-Null
    Assert-WithinRoot -Path $backupPath -Root $downloadRoot | Out-Null
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    try {
        [System.IO.File]::WriteAllText($temporaryPath, $json + [Environment]::NewLine, $utf8NoBom)
        Read-JsonArray -Path $temporaryPath | Out-Null
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
            Remove-Item -LiteralPath $backupPath -Force
        }
        else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
        if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
            Remove-Item -LiteralPath $backupPath -Force
        }
    }
}

function Get-PinnedHashRecord {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)
    @(Read-JsonArray $hashStatePath | Where-Object {
        $null -ne $_.PSObject.Properties['id'] -and
        $_.id -eq $Component.Id -and $_.url -eq $Component.Url -and [long]$_.size -eq $Component.DownloadBytes
    }) | Select-Object -First 1
}

function Save-PinnedHashRecord {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [Parameter(Mandatory = $true)][string]$Sha256
    )
    $records = @(Read-JsonArray $hashStatePath | Where-Object {
        $null -ne $_.PSObject.Properties['id'] -and $_.id -ne $Component.Id
    })
    $records += [PSCustomObject]@{
        id = $Component.Id
        url = $Component.Url
        size = [long]$Component.DownloadBytes
        officialIntegrity = $Component.Integrity
        sha256 = $Sha256.ToLowerInvariant()
        verifiedUtc = [DateTime]::UtcNow.ToString('o')
    }
    Write-JsonArray -Path $hashStatePath -Records $records
}

function Get-FileDigestBase64 {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateSet('MD5', 'SHA384')][string]$Algorithm
    )
    $hash = [System.Security.Cryptography.HashAlgorithm]::Create($Algorithm)
    if ($null -eq $hash) {
        throw "Hash algorithm is unavailable: $Algorithm"
    }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        [Convert]::ToBase64String($hash.ComputeHash($stream))
    }
    finally {
        $stream.Dispose()
        $hash.Dispose()
    }
}

function Confirm-Download {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$PinIfNew
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Download is missing: $Path"
    }
    $actualBytes = (Get-Item -LiteralPath $Path).Length
    if ($actualBytes -ne $Component.DownloadBytes) {
        throw "Byte-length mismatch for $($Component.Id). Expected $($Component.DownloadBytes); found $actualBytes."
    }

    if (-not [string]::IsNullOrWhiteSpace($Component.Integrity)) {
        $parts = $Component.Integrity.Split('-', 2)
        if ($parts.Count -ne 2) {
            throw "Unsupported integrity value for $($Component.Id): $($Component.Integrity)"
        }
        $algorithm = $parts[0].ToUpperInvariant()
        $actualBase64 = Get-FileDigestBase64 -Path $Path -Algorithm $algorithm
        if (-not [string]::Equals($actualBase64, $parts[1], [System.StringComparison]::Ordinal)) {
            throw "Official $algorithm digest mismatch for $($Component.Id)."
        }
    }

    if ($Component.Type -eq 'EXE') {
        $signature = Get-AuthenticodeSignature -LiteralPath $Path
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Authenticode signature is not valid for $($Component.Id): $($signature.Status)"
        }
    }

    $sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $pin = Get-PinnedHashRecord $Component
    if ($null -ne $pin -and -not [string]::Equals([string]$pin.sha256, $sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Locally pinned SHA-256 mismatch for $($Component.Id). Refusing changed bytes at the same URL."
    }
    if ($PinIfNew -and $null -eq $pin) {
        Save-PinnedHashRecord -Component $Component -Sha256 $sha256
    }
    $sha256
}

function Move-ToQuarantine {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Reason
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    Assert-WithinRoot -Path $Path -Root $downloadRoot | Out-Null
    $quarantine = Join-Path $downloadRoot 'quarantine'
    New-Item -ItemType Directory -Force -Path $quarantine | Out-Null
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
    $target = Join-Path $quarantine ("$stamp-" + [System.IO.Path]::GetFileName($Path))
    Move-Item -LiteralPath $Path -Destination $target
    Write-Output "Quarantined invalid download: $target ($Reason)"
}

function Invoke-ComponentDownload {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)

    $target = Get-DownloadPath $Component
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        try {
            Confirm-Download -Component $Component -Path $target -PinIfNew | Out-Null
            Write-Output "Verified cached download: $($Component.Id)"
            return $target
        }
        catch {
            Move-ToQuarantine -Path $target -Reason $_.Exception.Message
        }
    }

    $partial = "$target.partial"
    if (Test-Path -LiteralPath $partial -PathType Leaf) {
        $partialLength = (Get-Item -LiteralPath $partial).Length
        if ($partialLength -gt $Component.DownloadBytes) {
            Move-ToQuarantine -Path $partial -Reason 'partial file exceeds pinned byte length'
        }
        elseif ($partialLength -eq $Component.DownloadBytes) {
            try {
                Confirm-Download -Component $Component -Path $partial -PinIfNew | Out-Null
                Move-Item -LiteralPath $partial -Destination $target
                Write-Output "Promoted complete verified partial download: $($Component.Id)"
                return $target
            }
            catch {
                Move-ToQuarantine -Path $partial -Reason $_.Exception.Message
            }
        }
    }

    Add-Type -AssemblyName System.Net.Http
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan
    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Get,
        [Uri]$Component.Url)

    $startAt = 0L
    if (Test-Path -LiteralPath $partial -PathType Leaf) {
        $startAt = (Get-Item -LiteralPath $partial).Length
        if ($startAt -gt 0) {
            $request.Headers.Range = New-Object System.Net.Http.Headers.RangeHeaderValue($startAt, $null)
        }
    }

    try {
        Write-Output "Downloading $($Component.Id) ($([Math]::Round($Component.DownloadBytes / 1MB, 1)) MiB) to D:"
        $response = $client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if ($startAt -gt 0 -and $response.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
            $startAt = 0L
        }
        elseif ($startAt -gt 0 -and $response.StatusCode -ne [System.Net.HttpStatusCode]::PartialContent) {
            throw "Server refused safe resume for $($Component.Id): HTTP $([int]$response.StatusCode)."
        }
        elseif ($startAt -eq 0 -and -not $response.IsSuccessStatusCode) {
            throw "Download failed for $($Component.Id): HTTP $([int]$response.StatusCode)."
        }

        $mode = if ($startAt -gt 0) { [System.IO.FileMode]::Append } else { [System.IO.FileMode]::Create }
        $output = [System.IO.FileStream]::new($partial, $mode, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None, 1048576, [System.IO.FileOptions]::SequentialScan)
        $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        try {
            $buffer = New-Object byte[] 1048576
            $received = $startAt
            $lastReport = [DateTime]::UtcNow
            while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $output.Write($buffer, 0, $read)
                $received += $read
                if (([DateTime]::UtcNow - $lastReport).TotalSeconds -ge 10) {
                    $percent = [Math]::Round(($received * 100.0) / $Component.DownloadBytes, 1)
                    Write-Output "  $($Component.Id): $percent% ($([Math]::Round($received / 1MB, 1)) MiB)"
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

    $completedBytes = (Get-Item -LiteralPath $partial).Length
    if ($completedBytes -ne $Component.DownloadBytes) {
        throw "Incomplete download for $($Component.Id). Expected $($Component.DownloadBytes); found $completedBytes bytes. The D-drive partial file was retained for resume."
    }
    Move-Item -LiteralPath $partial -Destination $target
    Confirm-Download -Component $Component -Path $target -PinIfNew | Out-Null
    Write-Output "Downloaded and verified: $($Component.Id)"
    $target
}

function Test-ComponentMarker {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [string]$Root = $editorRoot
    )

    switch ($Component.Id) {
        'windows-il2cpp' {
            $variations = Join-Path $Root 'Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations'
            if (-not (Test-Path -LiteralPath $variations -PathType Container)) { return $false }
            return @(Get-ChildItem -LiteralPath $variations -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^win64_player_.*_il2cpp$' }).Count -gt 0
        }
        'windows-server' {
            $variations = Join-Path $Root 'Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations'
            if (-not (Test-Path -LiteralPath $variations -PathType Container)) { return $false }
            return @(Get-ChildItem -LiteralPath $variations -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^win64_.*server.*$|^win64_server_.*$' }).Count -gt 0
        }
        default {
            if ([string]::IsNullOrWhiteSpace($Component.MarkerRelative)) { return $false }
            return Test-Path -LiteralPath (Join-Path $Root $Component.MarkerRelative)
        }
    }
}

function Get-Receipt {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)
    @(Read-JsonArray $receiptPath | Where-Object {
        $null -ne $_.PSObject.Properties['id'] -and $_.id -eq $Component.Id
    }) | Select-Object -First 1
}

function Test-ReceiptMatches {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [Parameter(Mandatory = $true)][string]$ArchiveSha256
    )
    $receipt = Get-Receipt $Component
    $baseMatch = $null -ne $receipt -and
        $receipt.editorVersion -eq $targetVersion -and
        $receipt.changeset -eq $targetChangeset -and
        $receipt.url -eq $Component.Url -and
        [string]::Equals([string]$receipt.archiveSha256, $ArchiveSha256, [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $baseMatch) {
        return $false
    }

    if ($Component.Type -eq 'EXE') {
        $evidenceProperty = $receipt.PSObject.Properties['evidenceSha256']
        $evidenceFileCountProperty = $receipt.PSObject.Properties['evidenceFileCount']
        $evidenceBytesProperty = $receipt.PSObject.Properties['evidenceBytes']
        if ($null -eq $evidenceProperty -or [string]::IsNullOrWhiteSpace([string]$evidenceProperty.Value) -or
            $null -eq $evidenceFileCountProperty -or $null -eq $evidenceBytesProperty) {
            return $false
        }

        $evidence = Get-ComponentEvidenceDigest -Component $Component
        return [long]$receipt.evidenceFileCount -eq $evidence.FileCount -and
            [long]$receipt.evidenceBytes -eq $evidence.PayloadBytes -and
            [string]::Equals([string]$receipt.evidenceSha256, $evidence.Sha256, [System.StringComparison]::OrdinalIgnoreCase)
    }

    $treeProperty = $receipt.PSObject.Properties['payloadTreeSha256']
    $fileCountProperty = $receipt.PSObject.Properties['fileCount']
    $payloadBytesProperty = $receipt.PSObject.Properties['payloadBytes']
    if ($null -eq $treeProperty -or [string]::IsNullOrWhiteSpace([string]$treeProperty.Value) -or
        $null -eq $fileCountProperty -or $null -eq $payloadBytesProperty) {
        return $false
    }

    $payloadRoot = Join-Path $editorRoot $Component.PayloadRelative
    if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
        return $false
    }

    $tree = Get-TreeDigest -Root $payloadRoot
    return [long]$receipt.fileCount -eq $tree.FileCount -and
        [long]$receipt.payloadBytes -eq $tree.PayloadBytes -and
        [string]::Equals([string]$receipt.payloadTreeSha256, $tree.Sha256, [System.StringComparison]::OrdinalIgnoreCase)
}

function Save-Receipt {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [Parameter(Mandatory = $true)][string]$ArchiveSha256,
        [long]$FileCount = 0,
        [long]$PayloadBytes = 0,
        [string]$PayloadTreeSha256 = '',
        [long]$EvidenceFileCount = 0,
        [long]$EvidenceBytes = 0,
        [string]$EvidenceSha256 = ''
    )
    $records = @(Read-JsonArray $receiptPath | Where-Object {
        $null -ne $_.PSObject.Properties['id'] -and $_.id -ne $Component.Id
    })
    $records += [PSCustomObject]@{
        id = $Component.Id
        editorVersion = $targetVersion
        changeset = $targetChangeset
        url = $Component.Url
        archiveSha256 = $ArchiveSha256.ToLowerInvariant()
        payloadRelative = $Component.PayloadRelative
        fileCount = $FileCount
        payloadBytes = $PayloadBytes
        payloadTreeSha256 = $PayloadTreeSha256.ToLowerInvariant()
        evidenceFileCount = $EvidenceFileCount
        evidenceBytes = $EvidenceBytes
        evidenceSha256 = $EvidenceSha256.ToLowerInvariant()
        installedUtc = [DateTime]::UtcNow.ToString('o')
    }
    Write-JsonArray -Path $receiptPath -Records $records
}

if (-not ('Wof.Toolchain.Crc32' -as [type])) {
    Add-Type -TypeDefinition @'
namespace Wof.Toolchain
{
    public sealed class Crc32
    {
        private static readonly uint[] Table = CreateTable();
        private uint value = 0xffffffffu;

        private static uint[] CreateTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint item = i;
                for (int bit = 0; bit < 8; bit++)
                    item = (item & 1) != 0 ? 0xedb88320u ^ (item >> 1) : item >> 1;
                table[i] = item;
            }
            return table;
        }

        public void Update(byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
                value = Table[(value ^ buffer[i]) & 0xff] ^ (value >> 8);
        }

        public uint Value { get { return value ^ 0xffffffffu; } }
    }
}
'@
}

function Remove-ControlledStagingDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)
    Assert-WithinRoot -Path $Path -Root $stagingRoot | Out-Null
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Expand-ZipWithCrcValidation {
    param(
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][string]$ComponentId
    )

    Add-Type -AssemblyName System.IO.Compression
    $destinationFull = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\')
    New-Item -ItemType Directory -Force -Path $destinationFull | Out-Null
    $destinationPrefix = $destinationFull + '\'
    $archiveStream = [System.IO.File]::OpenRead($ArchivePath)
    $zip = [System.IO.Compression.ZipArchive]::new($archiveStream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
    $crcField = [System.IO.Compression.ZipArchiveEntry].GetField('_crc32', [System.Reflection.BindingFlags]'Instance,NonPublic')
    if ($null -eq $crcField) {
        throw 'This PowerShell runtime does not expose the ZIP central-directory CRC needed for verified extraction.'
    }

    try {
        $entryIndex = 0
        $lastReport = [DateTime]::UtcNow
        foreach ($entry in $zip.Entries) {
            $entryIndex++
            $relative = $entry.FullName.Replace('/', '\')
            if ([string]::IsNullOrWhiteSpace($relative)) { continue }
            $target = [System.IO.Path]::GetFullPath((Join-Path $destinationFull $relative))
            if (-not $target.StartsWith($destinationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "ZIP path traversal blocked in ${ComponentId}: $($entry.FullName)"
            }

            $isDirectory = $entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')
            if ($isDirectory) {
                [System.IO.Directory]::CreateDirectory($target) | Out-Null
                continue
            }

            $parent = [System.IO.Path]::GetDirectoryName($target)
            [System.IO.Directory]::CreateDirectory($parent) | Out-Null
            $input = $entry.Open()
            $output = [System.IO.FileStream]::new($target, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None, 1048576, [System.IO.FileOptions]::SequentialScan)
            $crc = New-Object Wof.Toolchain.Crc32
            $written = 0L
            try {
                $buffer = New-Object byte[] 1048576
                while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $output.Write($buffer, 0, $read)
                    $crc.Update($buffer, 0, $read)
                    $written += $read
                }
            }
            finally {
                $input.Dispose()
                $output.Dispose()
            }

            if ($written -ne $entry.Length) {
                throw "Uncompressed length mismatch in ${ComponentId}: $($entry.FullName)"
            }
            $expectedCrc = [uint32]$crcField.GetValue($entry)
            if ($crc.Value -ne $expectedCrc) {
                throw "ZIP CRC mismatch in ${ComponentId}: $($entry.FullName)"
            }
            if (([DateTime]::UtcNow - $lastReport).TotalSeconds -ge 10) {
                Write-Output "  extracting ${ComponentId}: $entryIndex / $($zip.Entries.Count) entries"
                $lastReport = [DateTime]::UtcNow
            }
        }
    }
    finally {
        $zip.Dispose()
        $archiveStream.Dispose()
    }
}

function Apply-StagedRename {
    param(
        [Parameter(Mandatory = $true)][PSCustomObject]$Component,
        [Parameter(Mandatory = $true)][string]$StageEditorRoot
    )
    if ([string]::IsNullOrWhiteSpace($Component.RenameFromRelative)) {
        return
    }

    $source = Join-Path $StageEditorRoot $Component.RenameFromRelative
    $target = Join-Path $StageEditorRoot $Component.RenameToRelative
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Pinned rename source is absent after extracting $($Component.Id): $source"
    }
    if ([string]::Equals($source, $target, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    $targetPrefix = $target.TrimEnd('\') + '\'
    if ($source.StartsWith($targetPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $temporary = "$target.__renamed_$($Component.Id.Replace('+', '_'))"
        if (Test-Path -LiteralPath $temporary) {
            throw "Unexpected staged rename collision: $temporary"
        }
        Move-Item -LiteralPath $source -Destination $temporary
        $remaining = @(Get-ChildItem -LiteralPath $target -Force -ErrorAction SilentlyContinue)
        if ($remaining.Count -ne 0) {
            throw "Refusing parent rename for $($Component.Id): archive placed unexpected siblings beside the payload."
        }
        Remove-Item -LiteralPath $target -Force
        Move-Item -LiteralPath $temporary -Destination $target
        return
    }

    if (Test-Path -LiteralPath $target) {
        throw "Staged rename target already exists for $($Component.Id): $target"
    }
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($target)) | Out-Null
    Move-Item -LiteralPath $source -Destination $target
}

function Get-SafeTreeFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    $fullRoot = Assert-DDrivePath -Path $Root
    $relativePath = $fullRoot.Substring(3).Trim('\')
    $cursor = 'D:\'
    foreach ($segment in @($relativePath.Split('\') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $cursor = Join-Path $cursor $segment
        if (-not (Test-Path -LiteralPath $cursor)) {
            break
        }
        $cursorItem = Get-Item -LiteralPath $cursor -Force
        if (($cursorItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing a reparse point in the controlled D-drive path: $cursor"
        }
    }
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Safe tree root is missing: $fullRoot"
    }

    $rootItem = Get-Item -LiteralPath $fullRoot -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing a reparse-point tree root: $fullRoot"
    }

    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    $pending.Push($fullRoot)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a reparse point during controlled tree traversal: $($item.FullName)"
            }
            if ($item.PSIsContainer) {
                $pending.Push($item.FullName)
            }
            else {
                $files.Add($item)
            }
        }
    }
    return $files.ToArray()
}

function Get-RelativeFileMap {
    param([Parameter(Mandatory = $true)][string]$Root)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $map = @{}
    foreach ($file in @(Get-SafeTreeFiles -Root $fullRoot)) {
        $relative = $file.FullName.Substring($fullRoot.Length).TrimStart('\')
        $map[$relative] = $file
    }
    $map
}

function Get-TreeDigest {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [string]$ControlledRoot = $editorRoot
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    Assert-WithinRoot -Path $fullRoot -Root $ControlledRoot | Out-Null
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Payload tree is missing: $fullRoot"
    }

    $records = New-Object System.Collections.Generic.List[string]
    $payloadBytes = 0L
    foreach ($file in @(Get-SafeTreeFiles -Root $fullRoot)) {
        $relative = $file.FullName.Substring($fullRoot.Length).TrimStart('\')
        $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $records.Add("$relative|$($file.Length)|$fileHash")
        $payloadBytes += $file.Length
    }

    $recordArray = $records.ToArray()
    [Array]::Sort($recordArray, [System.StringComparer]::Ordinal)
    $canonicalText = [string]::Join("`n", $recordArray)
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($canonicalText)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $treeHash = ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }

    [PSCustomObject]@{
        FileCount = [long]$recordArray.Count
        PayloadBytes = [long]$payloadBytes
        Sha256 = $treeHash
    }
}

function Get-ComponentEvidenceDigest {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)

    $evidenceRoots = @()
    switch ($Component.Id) {
        'windows-il2cpp' {
            $variations = Join-Path $editorRoot 'Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations'
            $evidenceRoots = @(Get-ChildItem -LiteralPath $variations -Directory -ErrorAction Stop |
                Where-Object { $_.Name -match '^win64_player_.*_il2cpp$' } |
                ForEach-Object { $_.FullName })
        }
        'windows-server' {
            $variations = Join-Path $editorRoot 'Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations'
            $evidenceRoots = @(Get-ChildItem -LiteralPath $variations -Directory -ErrorAction Stop |
                Where-Object { $_.Name -match '^win64_.*server.*$|^win64_server_.*$' } |
                ForEach-Object { $_.FullName })
        }
        default {
            if ([string]::IsNullOrWhiteSpace($Component.MarkerRelative)) {
                throw "No installed evidence path is defined for $($Component.Id)."
            }
            $evidenceRoots = @((Join-Path $editorRoot $Component.MarkerRelative))
        }
    }

    if ($evidenceRoots.Count -eq 0) {
        throw "No installed evidence paths were found for $($Component.Id)."
    }

    $recordList = New-Object System.Collections.Generic.List[string]
    $payloadBytes = 0L
    foreach ($root in $evidenceRoots) {
        Assert-WithinRoot -Path $root -Root $editorRoot | Out-Null
        $relativeEvidencePath = ([System.IO.Path]::GetFullPath($root)).Substring(3).Trim('\')
        $evidenceCursor = 'D:\'
        foreach ($segment in @($relativeEvidencePath.Split('\') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            $evidenceCursor = Join-Path $evidenceCursor $segment
            if (-not (Test-Path -LiteralPath $evidenceCursor)) {
                break
            }
            $evidenceItem = Get-Item -LiteralPath $evidenceCursor -Force
            if (($evidenceItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a reparse point in component evidence: $evidenceCursor"
            }
        }
        if (Test-Path -LiteralPath $root -PathType Leaf) {
            $files = @(Get-Item -LiteralPath $root)
        }
        elseif (Test-Path -LiteralPath $root -PathType Container) {
            $files = @(Get-SafeTreeFiles -Root $root)
            if ($files.Count -eq 0) {
                throw "Installed evidence directory is empty for $($Component.Id): $root"
            }
        }
        else {
            throw "Installed evidence path is missing for $($Component.Id): $root"
        }

        foreach ($file in $files) {
            if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a reparse point in component evidence: $($file.FullName)"
            }
            $relative = $file.FullName.Substring($editorRoot.TrimEnd('\').Length).TrimStart('\')
            $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            $recordList.Add("$relative|$($file.Length)|$fileHash")
            $payloadBytes += $file.Length
        }
    }

    $records = $recordList.ToArray()
    [Array]::Sort($records, [System.StringComparer]::Ordinal)
    $canonicalText = [string]::Join("`n", $records)
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($canonicalText)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $treeHash = ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }

    [PSCustomObject]@{
        FileCount = [long]$records.Count
        PayloadBytes = [long]$payloadBytes
        Sha256 = $treeHash
    }
}

function Test-ExactFileTree {
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedRoot,
        [Parameter(Mandatory = $true)][string]$ActualRoot
    )
    $expected = Get-RelativeFileMap $ExpectedRoot
    $actual = Get-RelativeFileMap $ActualRoot
    if ($expected.Count -ne $actual.Count) { return $false }
    foreach ($relative in $expected.Keys) {
        if (-not $actual.ContainsKey($relative)) { return $false }
        if ($expected[$relative].Length -ne $actual[$relative].Length) { return $false }
        $expectedHash = (Get-FileHash -LiteralPath $expected[$relative].FullName -Algorithm SHA256).Hash
        $actualHash = (Get-FileHash -LiteralPath $actual[$relative].FullName -Algorithm SHA256).Hash
        if (-not [string]::Equals($expectedHash, $actualHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }
    $true
}

function Install-ZipComponent {
    param([Parameter(Mandatory = $true)][PSCustomObject]$Component)
    $archive = Get-DownloadPath $Component
    $sha256 = Confirm-Download -Component $Component -Path $archive
    if ((Test-ComponentMarker $Component) -and (Test-ReceiptMatches -Component $Component -ArchiveSha256 $sha256)) {
        Write-Output "Verified installed component receipt: $($Component.Id)"
        return
    }

    $componentIndex = [Array]::IndexOf($components, $Component)
    if ($componentIndex -lt 0) {
        throw "Component is absent from the pinned manifest: $($Component.Id)"
    }
    $componentStage = Join-Path $stagingRoot ('c{0:D2}' -f $componentIndex)
    Remove-ControlledStagingDirectory $componentStage
    New-Item -ItemType Directory -Path $componentStage | Out-Null
    try {
        $extractDestination = Join-Path $componentStage $Component.DestinationRelative
        Write-Output "CRC-validating and staging $($Component.Id)"
        Expand-ZipWithCrcValidation -ArchivePath $archive -DestinationPath $extractDestination -ComponentId $Component.Id
        Apply-StagedRename -Component $Component -StageEditorRoot $componentStage

        $stagedPayload = Join-Path $componentStage $Component.PayloadRelative
        if (-not (Test-Path -LiteralPath $stagedPayload -PathType Container)) {
            throw "Expected staged payload is missing for $($Component.Id): $stagedPayload"
        }
        if (-not (Test-ComponentMarker -Component $Component -Root $componentStage)) {
            throw "Expected staged marker is missing for $($Component.Id). No Editor files were changed."
        }

        $outsidePayload = @(Get-SafeTreeFiles -Root $componentStage | Where-Object {
            $payloadPrefix = [System.IO.Path]::GetFullPath($stagedPayload).TrimEnd('\') + '\'
            -not $_.FullName.StartsWith($payloadPrefix, [System.StringComparison]::OrdinalIgnoreCase)
        })
        if ($outsidePayload.Count -gt 0) {
            throw "Archive $($Component.Id) placed files outside its pinned payload root. No Editor files were changed."
        }
        $stagedTree = Get-TreeDigest -Root $stagedPayload -ControlledRoot $stagingRoot
        $stagedPayloadBytes = $stagedTree.PayloadBytes

        $targetPayload = Join-Path $editorRoot $Component.PayloadRelative
        Assert-WithinRoot -Path $targetPayload -Root $editorRoot | Out-Null
        if (Test-Path -LiteralPath $targetPayload -PathType Container) {
            $existingItems = @(Get-ChildItem -LiteralPath $targetPayload -Force -ErrorAction SilentlyContinue)
            if ($existingItems.Count -eq 0) {
                Remove-Item -LiteralPath $targetPayload -Force
            }
            elseif (Test-ExactFileTree -ExpectedRoot $stagedPayload -ActualRoot $targetPayload) {
                Write-Output "Verified exact existing payload: $($Component.Id)"
                Save-Receipt -Component $Component -ArchiveSha256 $sha256 -FileCount $stagedTree.FileCount -PayloadBytes $stagedPayloadBytes -PayloadTreeSha256 $stagedTree.Sha256
                return
            }
            else {
                throw "Existing payload differs from the pinned $($Component.Id) archive: $targetPayload. Refusing to merge or overwrite it."
            }
        }
        elseif (Test-Path -LiteralPath $targetPayload) {
            throw "Expected directory target is occupied by a file: $targetPayload"
        }

        New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($targetPayload)) | Out-Null
        Move-Item -LiteralPath $stagedPayload -Destination $targetPayload
        if (-not (Test-ComponentMarker -Component $Component)) {
            throw "Installed payload marker is missing for $($Component.Id): $targetPayload"
        }
        Save-Receipt -Component $Component -ArchiveSha256 $sha256 -FileCount $stagedTree.FileCount -PayloadBytes $stagedPayloadBytes -PayloadTreeSha256 $stagedTree.Sha256
        Write-Output "Installed verified payload: $($Component.Id)"
    }
    finally {
        Remove-ControlledStagingDirectory $componentStage
    }
}

function Test-IsAdministrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-ExecutableComponents {
    if (-not (Test-IsAdministrator)) {
        throw 'The signed Unity component EXEs require an elevated installer process.'
    }
    foreach ($component in @($components | Where-Object { $_.Type -eq 'EXE' })) {
        $archive = Get-DownloadPath $component
        if (Test-ComponentMarker $component) {
            $pin = Get-PinnedHashRecord $component
            if ($null -ne $pin) {
                try {
                    if (Test-ReceiptMatches -Component $component -ArchiveSha256 ([string]$pin.sha256)) {
                        Write-Output "Verified installed component receipt: $($component.Id)"
                        continue
                    }
                }
                catch {
                    Write-Output "Installed evidence verification will be repaired by reinstalling $($component.Id): $($_.Exception.Message)"
                }
            }
        }

        $sha256 = Confirm-Download -Component $component -Path $archive

        Write-Output "Installing signed Unity component: $($component.Id)"
        # Unity documents /S for silent component installs and requires /D to
        # be the final, unquoted argument identifying the Unity root.
        $process = Start-Process -FilePath $archive -ArgumentList @('/S', "/D=$editorRoot") -WorkingDirectory $downloadRoot -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Unity component installer failed for $($component.Id) with exit code $($process.ExitCode)."
        }
        if (-not (Test-ComponentMarker $component)) {
            throw "Unity component installer exited successfully, but its exact marker is missing: $($component.Id)"
        }
        $evidence = Get-ComponentEvidenceDigest -Component $component
        Save-Receipt -Component $component -ArchiveSha256 $sha256 -EvidenceFileCount $evidence.FileCount -EvidenceBytes $evidence.PayloadBytes -EvidenceSha256 $evidence.Sha256
        Write-Output "Installed signed Unity component: $($component.Id)"
    }
}

function Repair-ExecutableReceipt {
    param([Parameter(Mandatory = $true)][string]$ComponentId)

    $component = @($components | Where-Object { $_.Type -eq 'EXE' -and $_.Id -eq $ComponentId }) |
        Select-Object -First 1
    if ($null -eq $component) {
        throw "Executable component is not defined: $ComponentId"
    }
    if (-not (Test-ComponentMarker $component)) {
        throw "Cannot recover a receipt because the installed marker is missing: $ComponentId"
    }
    if ($null -ne (Get-Receipt $component)) {
        throw "Refusing receipt recovery because a receipt already exists for $ComponentId."
    }
    if (-not (Test-Path -LiteralPath $installLogPath -PathType Leaf)) {
        throw "Cannot recover a receipt without the D-drive elevated installer transcript: $installLogPath"
    }

    $logText = Get-Content -LiteralPath $installLogPath -Raw
    $installMarker = "Installing signed Unity component: $ComponentId"
    $writeFailureMarker = 'TerminatingError(Write-JsonArray)'
    $installIndex = $logText.LastIndexOf($installMarker, [System.StringComparison]::Ordinal)
    $writeFailureIndex = $logText.LastIndexOf($writeFailureMarker, [System.StringComparison]::Ordinal)
    if ($installIndex -lt 0 -or $writeFailureIndex -lt $installIndex) {
        throw "The transcript does not prove that $ComponentId reached the receipt-write failure boundary. Refusing recovery."
    }

    # Revalidate the exact publisher archive and recompute the installed
    # evidence tree. This repairs only the missing atomic JSON commit; it does
    # not infer success from a directory marker or reinstall the payload.
    $archive = Get-DownloadPath $component
    $sha256 = Confirm-Download -Component $component -Path $archive
    $evidence = Get-ComponentEvidenceDigest -Component $component
    Save-Receipt -Component $component -ArchiveSha256 $sha256 `
        -EvidenceFileCount $evidence.FileCount -EvidenceBytes $evidence.PayloadBytes -EvidenceSha256 $evidence.Sha256

    $receipt = Get-Receipt $component
    $receiptMatches = $null -ne $receipt -and
        $receipt.editorVersion -eq $targetVersion -and
        $receipt.changeset -eq $targetChangeset -and
        $receipt.url -eq $component.Url -and
        [string]::Equals([string]$receipt.archiveSha256, $sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
        [long]$receipt.evidenceFileCount -eq $evidence.FileCount -and
        [long]$receipt.evidenceBytes -eq $evidence.PayloadBytes -and
        [string]::Equals([string]$receipt.evidenceSha256, $evidence.Sha256, [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $receiptMatches) {
        throw "Recovered receipt did not round-trip exactly for $ComponentId."
    }

    Write-Output "Recovered exact installed component receipt without reinstalling payload: $ComponentId"
}

function Test-ExecutableComponentsReady {
    $executableComponents = @($components | Where-Object { $_.Type -eq 'EXE' })

    # Determine whether elevation is needed before doing any expensive hashes.
    # If even one marker is absent, the answer is already known.
    foreach ($component in $executableComponents) {
        $archive = Get-DownloadPath $component
        if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
            return $false
        }

        # A missing installed marker cannot possibly have a matching install
        # receipt. Check that cheap condition before hashing multi-gigabyte
        # archives solely to decide whether elevation is necessary. The
        # elevated installer still performs the complete archive verification
        # immediately before every install.
        if (-not (Test-ComponentMarker $component)) {
            return $false
        }
    }

    foreach ($component in $executableComponents) {
        $archive = Get-DownloadPath $component
        try {
            $pin = Get-PinnedHashRecord $component
            $sha256 = if ($null -ne $pin) {
                [string]$pin.sha256
            }
            else {
                Confirm-Download -Component $component -Path $archive
            }
            if (-not (Test-ReceiptMatches -Component $component -ArchiveSha256 $sha256)) {
                return $false
            }
        }
        catch {
            return $false
        }
    }

    return $true
}

function Invoke-ElevatedExecutableInstall {
    if (Test-ExecutableComponentsReady) {
        Write-Output 'All signed Unity executable components and their evidence receipts are already verified; elevation is not required.'
        return
    }

    if (Test-IsAdministrator) {
        Install-ExecutableComponents
        return
    }

    $hostExecutable = (Get-Process -Id $PID).Path
    Write-Output 'One Windows UAC confirmation is required for the four signed Unity component installers.'
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-Action', 'install-executables',
        '-Apply'
    )
    $process = Start-Process -FilePath $hostExecutable -Verb RunAs -ArgumentList $arguments -WorkingDirectory 'D:\' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Elevated Unity component installation failed or UAC was declined (exit $($process.ExitCode))."
    }
}

function Invoke-Downloads {
    $requiredBytes = [long](($components | Measure-Object DownloadBytes -Sum).Sum)
    $freeBytes = (Get-PSDrive -Name D).Free
    if ($freeBytes -lt ($requiredBytes + 20GB)) {
        throw "Insufficient D: free space for downloads, staging, and installation. Free at least $([Math]::Round(($requiredBytes + 20GB) / 1GB, 1)) GiB."
    }
    foreach ($component in $components) {
        Invoke-ComponentDownload $component | Out-Null
    }
}

function Verify-InstalledComponents {
    Assert-ExactEditor
    $failures = @()
    foreach ($component in $components) {
        $markerReady = Test-ComponentMarker $component
        $download = Get-DownloadPath $component
        $receiptReady = $false
        if (Test-Path -LiteralPath $download -PathType Leaf) {
            try {
                $sha256 = Confirm-Download -Component $component -Path $download
                $receiptReady = Test-ReceiptMatches -Component $component -ArchiveSha256 $sha256
            }
            catch {
                $failures += "$($component.Id): download verification failed: $($_.Exception.Message)"
            }
        }
        else {
            $failures += "$($component.Id): pinned D-drive archive is missing"
        }
        if (-not $markerReady) { $failures += "$($component.Id): installed marker is missing" }
        if (-not $receiptReady) { $failures += "$($component.Id): matching install receipt is missing" }
        [PSCustomObject]@{
            Component = $component.Id
            Marker = $markerReady
            Receipt = $receiptReady
        }
    }
    if ($failures.Count -gt 0) {
        throw "Unity component verification failed:`n - $($failures -join "`n - ")"
    }
    Write-Output "All pinned Unity $targetVersion components and Android dependencies are verified on D:."
}

function Write-Plan {
    Assert-ExactEditor
    $downloadGiB = [Math]::Round((($components | Measure-Object DownloadBytes -Sum).Sum) / 1GB, 2)
    $installedGiB = [Math]::Round((($components | Measure-Object InstalledBytes -Sum).Sum) / 1GB, 2)
    Write-Output "Unity Editor: $targetVersion ($targetChangeset)"
    Write-Output "Editor root: $editorRoot"
    Write-Output "Download/cache root: $downloadRoot"
    Write-Output "Staging/temp roots: $stagingRoot ; $tempRoot"
    Write-Output "Pinned component download total: $downloadGiB GiB"
    Write-Output "Manifest installed-size estimate: $installedGiB GiB"
    Write-Output 'Install requires one manual Windows UAC approval. Sign-in/licensing is a later manual security boundary.'
    $components | ForEach-Object {
        [PSCustomObject]@{
            Component = $_.Id
            Type = $_.Type
            Installed = Test-ComponentMarker $_
            Downloaded = Test-Path -LiteralPath (Get-DownloadPath $_) -PathType Leaf
        }
    } | Format-Table -AutoSize | Out-String | Write-Output
}

Initialize-DOnlyState

switch ($Action) {
    'plan' {
        Write-Plan
    }
    'download' {
        Assert-ApplyRequested
        Assert-ExactEditor
        Invoke-Downloads
        Write-Output "All pinned component archives are verified in $downloadRoot"
    }
    'install-executables' {
        Assert-ApplyRequested
        Assert-ExactEditor
        $transcriptStarted = $false
        try {
            Start-Transcript -LiteralPath $installLogPath -Append -Force | Out-Null
            $transcriptStarted = $true
            Install-ExecutableComponents
        }
        finally {
            if ($transcriptStarted) {
                Stop-Transcript | Out-Null
            }
        }
    }
    'repair-executable-receipt' {
        Assert-ApplyRequested
        Assert-ExactEditor
        if ([string]::IsNullOrWhiteSpace($RecoveryComponentId)) {
            throw 'Receipt recovery requires -RecoveryComponentId.'
        }
        Repair-ExecutableReceipt -ComponentId $RecoveryComponentId
    }
    'elevate-executables' {
        Assert-ApplyRequested
        Assert-ExactEditor
        Invoke-ElevatedExecutableInstall
    }
    'install-zips' {
        Assert-ApplyRequested
        Assert-ExactEditor
        foreach ($component in @($components | Where-Object { $_.Type -eq 'ZIP' })) {
            Install-ZipComponent $component
        }
        Write-Output 'All pinned ZIP components were installed or receipt-verified on D:.'
    }
    'install' {
        Assert-ApplyRequested
        Assert-ExactEditor
        Invoke-Downloads
        Invoke-ElevatedExecutableInstall
        foreach ($component in @($components | Where-Object { $_.Type -eq 'ZIP' })) {
            Install-ZipComponent $component
        }
        Verify-InstalledComponents | Format-Table -AutoSize | Out-String | Write-Output
    }
    'verify' {
        Verify-InstalledComponents | Format-Table -AutoSize | Out-String | Write-Output
    }
}
