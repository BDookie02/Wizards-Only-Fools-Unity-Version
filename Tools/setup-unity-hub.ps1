[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('plan', 'download', 'install', 'verify', 'open', 'refresh-license')]
    [string]$Action = 'plan',

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installerUri = 'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe'
$installerRoot = 'D:\UnityInstallers\Hub'
$installerPath = Join-Path $installerRoot 'UnityHubSetup.exe'
$hubRoot = 'D:\UnityHub'
$hubExecutable = Join-Path $hubRoot 'Unity Hub.exe'
$profileRoot = 'D:\UnityHubProfile'
$stateRoot = 'D:\UnityAutomationState\Wizards-Only-Fools-Unity'
$receiptPath = Join-Path $stateRoot 'unity-hub-receipt.json'
$temporaryRoot = 'D:\tmp\unity-hub'

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
        throw "Action '$Action' changes local state. Re-run with -Apply."
    }
}

function Get-AuthenticodeEvidence {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Signed file is missing: $Path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode verification failed for $Path with status $($signature.Status)."
    }
    $subject = [string]$signature.SignerCertificate.Subject
    if ($subject -notmatch '(?i)(Unity Technologies|Unity Software)') {
        throw "Unexpected signer for ${Path}: $subject"
    }
    $item = Get-Item -LiteralPath $Path
    return [PSCustomObject]@{
        path = $item.FullName
        length = $item.Length
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        signerSubject = $subject
        signerThumbprint = [string]$signature.SignerCertificate.Thumbprint
        fileVersion = [string]$item.VersionInfo.FileVersion
        productVersion = [string]$item.VersionInfo.ProductVersion
    }
}

function Save-ReceiptAtomic {
    param([Parameter(Mandatory = $true)]$Receipt)
    New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
    $temporaryPath = Join-Path $stateRoot ("unity-hub-receipt.$([Guid]::NewGuid().ToString('N')).tmp")
    $backupPath = Join-Path $stateRoot ("unity-hub-receipt.$([Guid]::NewGuid().ToString('N')).bak")
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    try {
        [System.IO.File]::WriteAllText($temporaryPath, ($Receipt | ConvertTo-Json -Depth 8) + [Environment]::NewLine, $utf8NoBom)
        Get-Content -LiteralPath $temporaryPath -Raw | ConvertFrom-Json | Out-Null
        if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
            [System.IO.File]::Replace($temporaryPath, $receiptPath, $backupPath)
            Remove-Item -LiteralPath $backupPath -Force
        }
        else {
            [System.IO.File]::Move($temporaryPath, $receiptPath)
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

function Get-HubEvidence {
    return [PSCustomObject]@{
        schemaVersion = 1
        completedUtc = [DateTime]::UtcNow.ToString('o')
        installerUri = $installerUri
        installRoot = $hubRoot
        profileRoot = $profileRoot
        installer = Get-AuthenticodeEvidence -Path $installerPath
        installedExecutable = Get-AuthenticodeEvidence -Path $hubExecutable
    }
}

function Assert-HubReceipt {
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw "Unity Hub receipt is missing: $receiptPath"
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if (($receipt.schemaVersion -ne 1) -or
        (-not [string]::Equals([string]$receipt.installRoot, $hubRoot, [System.StringComparison]::OrdinalIgnoreCase)) -or
        (-not [string]::Equals([string]$receipt.profileRoot, $profileRoot, [System.StringComparison]::OrdinalIgnoreCase)) -or
        ([string]$receipt.installerUri -ne $installerUri)) {
        throw "Unity Hub receipt identity is invalid: $receiptPath"
    }
    $observed = Get-HubEvidence
    foreach ($name in @('installer', 'installedExecutable')) {
        if (-not [string]::Equals([string]$receipt.$name.path, [string]$observed.$name.path, [System.StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals([string]$receipt.$name.sha256, [string]$observed.$name.sha256, [System.StringComparison]::OrdinalIgnoreCase) -or
            ([long]$receipt.$name.length -ne [long]$observed.$name.length) -or
            -not [string]::Equals([string]$receipt.$name.signerThumbprint, [string]$observed.$name.signerThumbprint, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Unity Hub receipt no longer matches $name."
        }
    }
    return $observed
}

function Install-HubInstaller {
    New-Item -ItemType Directory -Force -Path $installerRoot | Out-Null
    if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
        Get-AuthenticodeEvidence -Path $installerPath | Out-Null
        Write-Output "Verified existing signed Unity Hub installer: $installerPath"
        return
    }
    $partialPath = Join-Path $installerRoot ("UnityHubSetup.$([Guid]::NewGuid().ToString('N')).partial")
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $installerUri -OutFile $partialPath
        Get-AuthenticodeEvidence -Path $partialPath | Out-Null
        [System.IO.File]::Move($partialPath, $installerPath)
    }
    finally {
        if (Test-Path -LiteralPath $partialPath -PathType Leaf) {
            Remove-Item -LiteralPath $partialPath -Force
        }
    }
    $evidence = Get-AuthenticodeEvidence -Path $installerPath
    Write-Output "Downloaded and verified signed Unity Hub installer $($evidence.productVersion): $installerPath"
}

function Install-UnityHub {
    Install-HubInstaller
    if (Test-Path -LiteralPath $hubExecutable -PathType Leaf) {
        Get-AuthenticodeEvidence -Path $hubExecutable | Out-Null
        Write-Output "Verified existing D-drive Unity Hub: $hubExecutable"
    }
    else {
        New-Item -ItemType Directory -Force -Path $hubRoot | Out-Null
        Write-Output "Starting the signed Unity Hub installer. Approve the Windows UAC prompt; installation target is $hubRoot."
        $process = Start-Process -FilePath $installerPath -ArgumentList @('/S', "/D=$hubRoot") -WorkingDirectory 'D:\' -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Unity Hub installer exited with code $($process.ExitCode)."
        }
        if (-not (Test-Path -LiteralPath $hubExecutable -PathType Leaf)) {
            throw "Unity Hub installer completed but the D-drive executable is missing: $hubExecutable"
        }
        Get-AuthenticodeEvidence -Path $hubExecutable | Out-Null
    }
    foreach ($directory in @(
        $profileRoot,
        (Join-Path $profileRoot 'UserData'),
        $temporaryRoot
    )) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $receipt = Get-HubEvidence
    Save-ReceiptAtomic -Receipt $receipt
    Write-Output "Unity Hub $($receipt.installedExecutable.productVersion) installed and receipt-verified on D: $hubExecutable"
}

function Open-UnityHub {
    $evidence = Assert-HubReceipt
    foreach ($directory in @(
        $profileRoot,
        (Join-Path $profileRoot 'Roaming'),
        (Join-Path $profileRoot 'Local'),
        (Join-Path $profileRoot 'User'),
        (Join-Path $profileRoot 'UserData'),
        $temporaryRoot
    )) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $hubExecutable
    $startInfo.WorkingDirectory = 'D:\'
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = "--user-data-dir=`"$(Join-Path $profileRoot 'UserData')`""
    # Keep the normal Windows identity environment so browser-based Unity sign-in
    # reuses the user's existing signed-in Chrome profile. Hub's Electron data and
    # temporary files remain explicitly confined to D:.
    $startInfo.EnvironmentVariables['TEMP'] = $temporaryRoot
    $startInfo.EnvironmentVariables['TMP'] = $temporaryRoot
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw 'Unity Hub did not start.'
    }
    # Hub rewrites its protocol handler during startup, so enforce the D-profile
    # callback command only after the process has initialized.
    Start-Sleep -Milliseconds 1500
    $protocolCommandKey = 'Registry::HKEY_CURRENT_USER\Software\Classes\unityhub\shell\open\command'
    $protocolCommand = "`"$hubExecutable`" --user-data-dir=`"$(Join-Path $profileRoot 'UserData')`" `"%1`""
    New-Item -Path $protocolCommandKey -Force | Out-Null
    Set-Item -LiteralPath $protocolCommandKey -Value $protocolCommand
    Write-Output "Opened Unity Hub $($evidence.installedExecutable.productVersion) with Hub data and temporary files on D: and the normal Windows browser identity (PID $($process.Id))."
}

function Restart-UnityHubAndRefreshLicense {
    [void](Assert-HubReceipt)
    $hubProcesses = @(Get-Process -Name 'Unity Hub', 'Unity.Licensing.Client' -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($hubRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)
    })
    foreach ($process in $hubProcesses) {
        Stop-Process -Id $process.Id -Force
    }
    foreach ($process in $hubProcesses) {
        try {
            Wait-Process -Id $process.Id -Timeout 15 -ErrorAction Stop
        }
        catch {
            if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
                throw "D-drive Unity Hub process $($process.Id) did not exit during the controlled license refresh."
            }
        }
    }

    $refreshStartedUtc = [DateTime]::UtcNow.AddSeconds(-2)
    Open-UnityHub
    $logPath = Join-Path $profileRoot 'UserData\logs\info-log.json'
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $events = foreach ($line in @(Get-Content -LiteralPath $logPath -Tail 500)) {
                try {
                    $event = $line | ConvertFrom-Json
                    $eventTime = [DateTime]::Parse(
                        [string]$event.time,
                        [System.Globalization.CultureInfo]::InvariantCulture,
                        [System.Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
                    if ($eventTime -ge $refreshStartedUtc) {
                        $event
                    }
                }
                catch {
                    # Ignore a partially flushed JSONL tail entry and retry.
                }
            }
            $hasEntitlements = @($events | Where-Object {
                $_.moduleName -eq 'LicensingSdk' -and $_.msg -match '^Received [1-9][0-9]* entitlement groups$'
            }).Count -gt 0
            $hasActivatedSeat = @($events | Where-Object {
                $_.moduleName -eq 'LicensingSdk' -and
                $_.msg -match 'Successfully activated all entitlement based licenses' -and
                $_.msg -match '"statusCode":200'
            }).Count -gt 0
            if ($hasEntitlements -and $hasActivatedSeat) {
                Write-Output "Unity Hub refreshed the signed-in Personal entitlement through its D profile. Evidence: $logPath"
                return
            }
        }
        Start-Sleep -Milliseconds 500
    }

    throw "Unity Hub did not refresh a valid entitlement within 75 seconds. Inspect the D-drive Hub window and log: $logPath"
}

foreach ($path in @($installerRoot, $installerPath, $hubRoot, $hubExecutable, $profileRoot, $stateRoot, $receiptPath, $temporaryRoot)) {
    Assert-DDrivePath -Path $path | Out-Null
}

switch ($Action) {
    'plan' {
        [ordered]@{
            action = 'plan'
            mutationPerformed = $false
            officialInstaller = $installerUri
            installerPath = $installerPath
            installRoot = $hubRoot
            profileRoot = $profileRoot
            receiptPath = $receiptPath
            systemManagedException = 'Unity authentication, license storage, certificate validation, installer registration, and the tiny unityhub:// protocol registry entry use Windows-managed state; Hub binaries, data, and temporary files remain on D:.'
            installCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' install -Apply"
            openCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' open -Apply"
            refreshLicenseCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File '$PSCommandPath' refresh-license -Apply"
        } | ConvertTo-Json -Depth 6
    }
    'download' {
        Assert-ApplyRequested
        Install-HubInstaller
    }
    'install' {
        Assert-ApplyRequested
        Install-UnityHub
    }
    'verify' {
        $evidence = Assert-HubReceipt
        Write-Output "Unity Hub receipt verification passed: $receiptPath"
        Write-Output "Installed product version: $($evidence.installedExecutable.productVersion)"
    }
    'open' {
        Assert-ApplyRequested
        Open-UnityHub
    }
    'refresh-license' {
        Assert-ApplyRequested
        Restart-UnityHubAndRefreshLicense
    }
}
