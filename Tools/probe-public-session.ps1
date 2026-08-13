param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\public-session'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$player = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) {
    throw "Windows player not found: $player"
}

$runToken = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$runRoot = Join-Path $resolvedOutputRoot $runToken
$hostProfile = Join-Path $runRoot 'host-profile'
$clientProfile = Join-Path $runRoot 'client-profile'
$hostLog = Join-Path $runRoot 'host.log'
$clientLog = Join-Path $runRoot 'client.log'
New-Item -ItemType Directory -Force -Path $runRoot,$hostProfile,$clientProfile | Out-Null

function Start-WofProcess {
    param(
        [Parameter(Mandatory = $true)][string]$ProfileRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $localAppData = Join-Path $ProfileRoot 'AppData\Local'
    $roamingAppData = Join-Path $ProfileRoot 'AppData\Roaming'
    $processTemp = Join-Path $ProfileRoot 'Temp'
    New-Item -ItemType Directory -Force -Path $localAppData,$roamingAppData,$processTemp | Out-Null
    $names = @('USERPROFILE','LOCALAPPDATA','APPDATA','TEMP','TMP')
    $previous = @{}
    foreach ($name in $names) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    try {
        $env:USERPROFILE = $ProfileRoot
        $env:LOCALAPPDATA = $localAppData
        $env:APPDATA = $roamingAppData
        $env:TEMP = $processTemp
        $env:TMP = $processTemp
        return Start-Process -FilePath $player -ArgumentList $Arguments -WorkingDirectory $resolvedBuildRoot -WindowStyle Hidden -PassThru
    }
    finally {
        foreach ($name in $names) {
            [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
        }
    }
}

function Wait-WofLogMatch {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [int]$Seconds = 60
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $match = if (Test-Path -LiteralPath $Path -PathType Leaf) {
            Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
        }
        else {
            $null
        }
        $Process.Refresh()
    } while ($null -eq $match -and -not $Process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $match
}

$hostArguments = @(
    '-batchmode','-nographics','-force-d3d11',
    '--wof-public-host','--wof-voice-probe','--wof-auto-exit=150',
    "--wof-auth-profile=host-$runToken",
    "--wof-profile-root=$hostProfile",'-logFile',$hostLog
)

$hostProcess = $null
$clientProcess = $null
try {
    $hostProcess = Start-WofProcess -ProfileRoot $hostProfile -Arguments $hostArguments
    $hostReady = Wait-WofLogMatch -Path $hostLog -Process $hostProcess -Pattern 'PUBLIC_SESSION_HOST_READY joinCode=([A-Z0-9-]+)' -Seconds 75
    if ($null -eq $hostReady) {
        $tail = if (Test-Path -LiteralPath $hostLog) { (Get-Content -LiteralPath $hostLog -Tail 120) -join [Environment]::NewLine } else { '<missing host log>' }
        throw "Public Relay host did not produce an invite code.`n$tail"
    }

    $joinCode = [regex]::Match($hostReady.Line, 'joinCode=([A-Z0-9-]+)').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($joinCode)) {
        throw "Public Relay host marker did not contain a valid invite code: $($hostReady.Line)"
    }
    if ($null -eq (Wait-WofLogMatch -Path $hostLog -Process $hostProcess -Pattern 'SESSION_READY mode=Host' -Seconds 20)) {
        throw 'Public Relay host did not start its Netcode session.'
    }

    $clientArguments = @(
        '-batchmode','-nographics','-force-d3d11',
        "--wof-public-client=$joinCode",'--wof-voice-probe','--wof-auto-exit=120',
        "--wof-auth-profile=client-$runToken",
        "--wof-profile-root=$clientProfile",'-logFile',$clientLog
    )
    $clientProcess = Start-WofProcess -ProfileRoot $clientProfile -Arguments $clientArguments

    if ($null -eq (Wait-WofLogMatch -Path $clientLog -Process $clientProcess -Pattern "PUBLIC_SESSION_CLIENT_READY joinCode=$joinCode" -Seconds 75)) {
        $tail = if (Test-Path -LiteralPath $clientLog) { (Get-Content -LiteralPath $clientLog -Tail 120) -join [Environment]::NewLine } else { '<missing client log>' }
        throw "Public Relay client did not join with the issued invite code.`n$tail"
    }
    if ($null -eq (Wait-WofLogMatch -Path $clientLog -Process $clientProcess -Pattern 'SESSION_READY mode=Client' -Seconds 20)) {
        throw 'Public Relay client did not start its Netcode session.'
    }
    if ($null -eq (Wait-WofLogMatch -Path $hostLog -Process $hostProcess -Pattern 'CLIENT_CONNECTED id=1' -Seconds 20)) {
        throw 'Public Relay host did not observe the remote client.'
    }
    if ($null -eq (Wait-WofLogMatch -Path $hostLog -Process $hostProcess -Pattern 'VOICE_STATUS STATUS: CONNECTED' -Seconds 45)) {
        throw 'Vivox did not connect for the public host.'
    }
    if ($null -eq (Wait-WofLogMatch -Path $clientLog -Process $clientProcess -Pattern 'VOICE_STATUS STATUS: CONNECTED' -Seconds 45)) {
        throw 'Vivox did not connect for the public client.'
    }

    $failurePattern = 'PUBLIC_SESSION_(HOST|CLIENT)_FAILED|VOICE CONNECTION FAILED|PUBLIC ONLINE INITIALIZATION FAILED|NullReferenceException|MissingReferenceException|Unhandled Exception'
    $failures = @(
        Select-String -LiteralPath $hostLog,$clientLog -Pattern $failurePattern -ErrorAction SilentlyContinue
    )
    if ($failures.Count -gt 0) {
        throw "Public-session runtime failures detected: $($failures.Line -join '; ')"
    }

    [pscustomobject]@{
        Status = 'PASS'
        JoinCode = $joinCode
        RelayHostReady = $true
        RelayClientReady = $true
        HostObservedRemoteClient = $true
        VivoxHostConnected = $true
        VivoxClientConnected = $true
        HostLog = $hostLog
        ClientLog = $clientLog
    }
}
finally {
    foreach ($process in @($clientProcess,$hostProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
