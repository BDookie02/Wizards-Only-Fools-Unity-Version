param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\astral-meditation-network'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Astral-meditation network probe paths must stay on D:.'
}

$tempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$hostProfile = Join-Path $resolvedOutputRoot 'host-profile'
$clientProfile = Join-Path $resolvedOutputRoot 'client-profile'
$hostLog = Join-Path $resolvedOutputRoot 'host.log'
$clientLog = Join-Path $resolvedOutputRoot 'client.log'
$runToken = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$hostProfile = Join-Path $hostProfile $runToken
$clientProfile = Join-Path $clientProfile $runToken
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot,$tempRoot,$hostProfile,$clientProfile | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
foreach ($log in @($hostLog,$clientLog)) {
    if (Test-Path -LiteralPath $log -PathType Leaf) { Remove-Item -LiteralPath $log -Force }
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofAstralNetworkInput {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

  public static bool Focus(IntPtr hWnd) {
    ShowWindowAsync(hWnd, 9);
    IntPtr foreground = GetForegroundWindow();
    uint currentThread = GetCurrentThreadId();
    uint targetThread = GetWindowThreadProcessId(hWnd, IntPtr.Zero);
    uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);
    if (foregroundThread != 0 && foregroundThread != currentThread) AttachThreadInput(currentThread, foregroundThread, true);
    if (targetThread != 0 && targetThread != currentThread) AttachThreadInput(currentThread, targetThread, true);
    BringWindowToTop(hWnd);
    SetForegroundWindow(hWnd);
    SetFocus(hWnd);
    if (targetThread != 0 && targetThread != currentThread) AttachThreadInput(currentThread, targetThread, false);
    if (foregroundThread != 0 && foregroundThread != currentThread) AttachThreadInput(currentThread, foregroundThread, false);
    return GetForegroundWindow() == hWnd;
  }

  public static void SetKey(byte key, bool pressed) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, pressed ? 0u : 2u, UIntPtr.Zero);
  }
}
'@

$player = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) { throw "Windows player not found: $player" }

function Start-WofDProcess([string]$ProfileRoot, [string[]]$Arguments, [bool]$Hidden = $false) {
    $localAppData = Join-Path $ProfileRoot 'AppData\Local'
    $roamingAppData = Join-Path $ProfileRoot 'AppData\Roaming'
    $processTemp = Join-Path $ProfileRoot 'Temp'
    New-Item -ItemType Directory -Force -Path $localAppData,$roamingAppData,$processTemp | Out-Null
    $names = @('USERPROFILE','LOCALAPPDATA','APPDATA','TEMP','TMP')
    $previous = @{}
    foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    try {
        $env:USERPROFILE = $ProfileRoot
        $env:LOCALAPPDATA = $localAppData
        $env:APPDATA = $roamingAppData
        $env:TEMP = $processTemp
        $env:TMP = $processTemp
        $start = @{
            FilePath = $player
            ArgumentList = $Arguments
            WorkingDirectory = $resolvedBuildRoot
            PassThru = $true
        }
        if ($Hidden) { $start.WindowStyle = 'Hidden' }
        return Start-Process @start
    }
    finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
}

function Wait-WofMarker([string]$Path, [System.Diagnostics.Process]$Process, [string]$Pattern, [int]$Count = 1, [int]$Seconds = 20) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $matches = if (Test-Path -LiteralPath $Path -PathType Leaf) {
            @(Select-String -LiteralPath $Path -Pattern $Pattern)
        } else { @() }
        $Process.Refresh()
    } while ($matches.Count -lt $Count -and -not $Process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $matches.Count -ge $Count
}

function Send-WofKey([byte]$VirtualKey, [int]$HoldMilliseconds = 100) {
    [WofAstralNetworkInput]::SetKey($VirtualKey, $true)
    Start-Sleep -Milliseconds $HoldMilliseconds
    [WofAstralNetworkInput]::SetKey($VirtualKey, $false)
}

function Test-WofLastMarker([string]$Path, [string]$Pattern, [string]$ExpectedText) {
    $last = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
    return $null -ne $last -and $last.Line.Contains($ExpectedText)
}

$hostArguments = @(
    '-force-d3d11','-screen-width','960','-screen-height','540','-screen-fullscreen','0',
    '--wof-host','--wof-auto-exit=90',"--wof-profile-root=$hostProfile",'-logFile',$hostLog
)
$clientArguments = @(
    '-force-d3d11','-screen-width','1280','-screen-height','720','-screen-fullscreen','0',
    '--wof-client=127.0.0.1','--wof-auto-exit=90',"--wof-profile-root=$clientProfile",'-logFile',$clientLog
)

$hostProcess = $null
$clientProcess = $null
$leftControl = [byte]0xA2
try {
    $hostProcess = Start-WofDProcess -ProfileRoot $hostProfile -Arguments $hostArguments -Hidden $true
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'SERVER_STARTED' -Seconds 40)) {
        throw 'Astral network host did not start.'
    }

    $clientProcess = Start-WofDProcess -ProfileRoot $clientProfile -Arguments $clientArguments
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'SESSION_READY mode=Client' -Seconds 40)) {
        throw 'Astral network client did not become ready.'
    }
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'CLIENT_CONNECTED id=1' -Seconds 10)) {
        throw 'Astral network client did not connect to the host.'
    }
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'LILY_COIL_SCENE_READY' -Seconds 40)) {
        throw 'Astral network client did not finish loading all additive world scenes.'
    }
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'BOTW_GRASS_BUILD_COMPLETE center=0,0' -Seconds 40)) {
        throw 'Astral network client did not finish its initial grass build.'
    }
    Start-Sleep -Milliseconds 1000

    $clientProcess.Refresh()
    $handle = $clientProcess.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero -or -not [WofAstralNetworkInput]::Focus($handle)) {
        throw 'Could not focus the visible network client.'
    }
    Start-Sleep -Milliseconds 300

    # A visible Unity window may receive a physical key or click while its additive
    # scenes are still starting. Normalize only those transient overlays before the
    # actual probe so Ctrl is tested from the same gameplay state as the React oracle.
    $escape = [byte]0x1B
    $mapKey = [byte]0x4D
    if (Test-WofLastMarker -Path $clientLog -Pattern 'NAVIGATION_MAP expanded=' -ExpectedText 'expanded=True') {
        Send-WofKey -VirtualKey $mapKey
        if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'NAVIGATION_MAP expanded=False' -Seconds 5)) {
            throw 'Could not close the startup map overlay before the network meditation probe.'
        }
    }
    if (Test-WofLastMarker -Path $clientLog -Pattern 'INVENTORY_(OPEN|CLOSED)' -ExpectedText 'INVENTORY_OPEN') {
        Send-WofKey -VirtualKey $escape
        if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'INVENTORY_CLOSED' -Seconds 5)) {
            throw 'Could not close the startup inventory overlay before the network meditation probe.'
        }
    }

    if (-not [WofAstralNetworkInput]::Focus($handle)) {
        throw 'Could not refocus the settled network client.'
    }
    Start-Sleep -Milliseconds 300
    Send-WofKey -VirtualKey $leftControl -HoldMilliseconds 120

    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'ASTRAL_MEDITATION_PRESENTATION owner=1 active=true cameraHeight=1\.15[0-9] handsVisible=false' -Seconds 8)) {
        throw 'Network client did not enter the complete meditation presentation.'
    }
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'ASTRAL_SKY_PRESENTATION sky=1\.000 veil=1\.000 blink=0\.000 veilAlpha=' -Seconds 8)) {
        throw 'Network client did not render the complete React astral sky and veil presentation.'
    }
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'ASTRAL_MEDITATION_CHANGED owner=1 active=true' -Seconds 8)) {
        throw 'Host did not accept and replicate the client meditation state.'
    }
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'REMOTE_AVATAR_ANIMATION owner=1 animation=meditate frameDelay=0\.520' -Seconds 8)) {
        throw 'Host did not render the remote client with the exact meditate animation timing.'
    }

    if (-not [WofAstralNetworkInput]::Focus($handle)) { throw 'Could not refocus the network client.' }
    [WofAstralNetworkInput]::SetKey($leftControl, $true)
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'ASTRAL_MEDITATION_LOCAL owner=1 active=false' -Seconds 8)) {
        throw 'Network client did not exit after the physical five-second Ctrl hold.'
    }
    [WofAstralNetworkInput]::SetKey($leftControl, $false)
    if (-not (Wait-WofMarker -Path $clientLog -Process $clientProcess -Pattern 'ASTRAL_SKY active=false' -Seconds 8)) {
        throw 'Network client did not restore the normal sky after meditation.'
    }
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'ASTRAL_MEDITATION_CHANGED owner=1 active=false' -Seconds 8)) {
        throw 'Host did not accept and replicate meditation exit.'
    }
    if (-not (Wait-WofMarker -Path $hostLog -Process $hostProcess -Pattern 'REMOTE_AVATAR_ANIMATION owner=1 animation=idle frameDelay=0\.210' -Seconds 8)) {
        throw 'Host did not restore the remote client idle animation after meditation.'
    }

    $failurePattern = 'NullReferenceException|MissingReferenceException|ArgumentException|IndexOutOfRangeException|Unhandled Exception'
    $hostFailures = @(Select-String -LiteralPath $hostLog -Pattern $failurePattern)
    $clientFailures = @(Select-String -LiteralPath $clientLog -Pattern $failurePattern)
    if ($hostFailures.Count -gt 0 -or $clientFailures.Count -gt 0) {
        throw "Runtime exceptions detected. host=$($hostFailures.Line -join '; ') client=$($clientFailures.Line -join '; ')"
    }

    [pscustomobject]@{
        Status = 'PASS'
        EnterReplicated = $true
        RemoteMeditateAnimation = $true
        ExitReplicated = $true
        RemoteIdleRestored = $true
        HostLog = $hostLog
        ClientLog = $clientLog
    }
}
finally {
    [WofAstralNetworkInput]::SetKey($leftControl, $false)
    foreach ($process in @($clientProcess,$hostProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
        }
    }
}
