param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\astral-meditation',
    [ValidateRange(640, 7680)]
    [int]$Width = 1280,
    [ValidateRange(360, 4320)]
    [int]$Height = 720
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Astral-meditation probe paths must stay on D:.'
}

$tempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$profileRoot = Join-Path $resolvedOutputRoot 'profile'
$localAppData = Join-Path $profileRoot 'AppData\Local'
$roamingAppData = Join-Path $profileRoot 'AppData\Roaming'
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot,$tempRoot,$profileRoot,$localAppData,$roamingAppData | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofAstralProbeInput {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

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

  public static void ClickLeft() {
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
}
'@
[WofAstralProbeInput]::SetProcessDPIAware() | Out-Null

$player = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) {
    throw "Windows player not found: $player"
}

$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$baselinePath = Join-Path $resolvedOutputRoot 'astral-baseline.png'
$activePath = Join-Path $resolvedOutputRoot 'astral-active.png'
$shortHoldPath = Join-Path $resolvedOutputRoot 'astral-short-hold-still-active.png'
$exitedPath = Join-Path $resolvedOutputRoot 'astral-exited.png'
foreach ($artifact in @($logPath,$baselinePath,$activePath,$shortHoldPath,$exitedPath)) {
    if (Test-Path -LiteralPath $artifact -PathType Leaf) { Remove-Item -LiteralPath $artifact -Force }
}

function Wait-WofMarker([string]$Pattern, [int]$Count = 1, [int]$Seconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $matches = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            @(Select-String -LiteralPath $logPath -Pattern $Pattern)
        } else { @() }
        $process.Refresh()
    } while ($matches.Count -lt $Count -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $matches.Count -ge $Count
}

function Set-WofFocus([IntPtr]$Handle) {
    if (-not [WofAstralProbeInput]::Focus($Handle)) { throw 'Could not focus the Unity player.' }
    Start-Sleep -Milliseconds 180
}

function Save-WofWindow([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object WofAstralProbeInput+RECT
    if (-not [WofAstralProbeInput]::GetClientRect($Handle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofAstralProbeInput+POINT
    if (-not [WofAstralProbeInput]::ClientToScreen($Handle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $actualWidth = $rect.Right - $rect.Left
    $actualHeight = $rect.Bottom - $rect.Top
    if ($actualWidth -ne $Width -or $actualHeight -ne $Height) {
        throw "Unexpected Unity client size: ${actualWidth}x${actualHeight}; expected ${Width}x${Height}."
    }
    $bitmap = New-Object System.Drawing.Bitmap $actualWidth, $actualHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($point.X, $point.Y, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=90', "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$environmentNames = @('USERPROFILE','LOCALAPPDATA','APPDATA','TEMP','TMP')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:USERPROFILE = $profileRoot
    $env:LOCALAPPDATA = $localAppData
    $env:APPDATA = $roamingAppData
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    $process = Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
}

$leftControl = [byte]0xA2
$wKey = [byte]0x57
try {
    if (-not (Wait-WofMarker -Pattern 'PLAYER_SPAWN' -Seconds 40)) { throw 'Windows player did not spawn.' }
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Windows player has no window.' }
    Set-WofFocus -Handle $handle
    Start-Sleep -Milliseconds 900
    Save-WofWindow -Handle $handle -Path $baselinePath

    Set-WofFocus -Handle $handle
    [WofAstralProbeInput]::SetKey($leftControl, $true)
    Start-Sleep -Milliseconds 120
    [WofAstralProbeInput]::SetKey($leftControl, $false)
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_LOCAL owner=0 active=true' -Seconds 5)) {
        throw 'Physical Ctrl press did not enter astral meditation.'
    }
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_EXIT_ARMED owner=0' -Seconds 5)) {
        throw 'Physical Ctrl release did not arm the exit hold.'
    }
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_PRESENTATION owner=0 active=true cameraHeight=1\.15[0-9] handsVisible=false' -Seconds 8)) {
        throw 'Meditation presentation did not settle at the converted 0.58 m React camera height with both hands hidden.'
    }
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_SKY_PRESENTATION sky=1\.000 veil=1\.000 blink=0\.000 veilAlpha=' -Seconds 8)) {
        throw 'React astral sky, fog, and rotating veil did not finish their exact transition.'
    }
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $activePath

    Set-WofFocus -Handle $handle
    [WofAstralProbeInput]::SetKey($wKey, $true)
    Start-Sleep -Milliseconds 300
    [WofAstralProbeInput]::ClickLeft()
    Start-Sleep -Milliseconds 600
    [WofAstralProbeInput]::SetKey($wKey, $false)
    if (Select-String -LiteralPath $logPath -Pattern 'SPELL_CAST owner=0|FIRING_HAND' -Quiet) {
        throw 'Movement/cast suppression failed: a physical click fired while meditating.'
    }

    Set-WofFocus -Handle $handle
    [WofAstralProbeInput]::SetKey($leftControl, $true)
    Start-Sleep -Milliseconds 900
    [WofAstralProbeInput]::SetKey($leftControl, $false)
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_SHORT_HOLD_CANCELLED owner=0 elapsed=' -Seconds 5)) {
        throw 'Short physical Ctrl hold was not cancelled while remaining in meditation.'
    }
    if (Select-String -LiteralPath $logPath -Pattern 'ASTRAL_MEDITATION_LOCAL owner=0 active=false' -Quiet) {
        throw 'A short Ctrl hold incorrectly exited meditation.'
    }
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $shortHoldPath

    Set-WofFocus -Handle $handle
    [WofAstralProbeInput]::SetKey($leftControl, $true)
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_EXIT_HOLD_STARTED owner=0' -Count 2 -Seconds 5)) {
        throw 'Second physical Ctrl hold did not begin the five-second exit timer.'
    }
    if (-not (Wait-WofMarker -Pattern 'ASTRAL_MEDITATION_LOCAL owner=0 active=false cameraHeight=1\.650 handsVisible=true' -Seconds 8)) {
        throw 'Continuous five-second Ctrl hold did not restore standing camera and hands.'
    }
    [WofAstralProbeInput]::SetKey($leftControl, $false)

    $presentationLine = (Select-String -LiteralPath $logPath -Pattern 'ASTRAL_MEDITATION_PRESENTATION owner=0').Line | Select-Object -Last 1
    $exitLine = (Select-String -LiteralPath $logPath -Pattern 'ASTRAL_MEDITATION_LOCAL owner=0 active=false').Line | Select-Object -Last 1
    $positionPattern = 'position=(?<position>-?\d+\.\d+,-?\d+\.\d+,-?\d+\.\d+)'
    $presentationPosition = [regex]::Match($presentationLine, $positionPattern).Groups['position'].Value
    $exitPosition = [regex]::Match($exitLine, $positionPattern).Groups['position'].Value
    if ([string]::IsNullOrWhiteSpace($presentationPosition) -or $presentationPosition -ne $exitPosition) {
        throw "Physical W input moved the meditating player. active=$presentationPosition exited=$exitPosition"
    }
    Set-WofFocus -Handle $handle
    Start-Sleep -Milliseconds 250
    Save-WofWindow -Handle $handle -Path $exitedPath

    $exceptions = @(Select-String -LiteralPath $logPath -Pattern 'NullReferenceException|MissingReferenceException|ArgumentException|IndexOutOfRangeException|Unhandled Exception')
    if ($exceptions.Count -gt 0) { throw "Runtime exceptions detected: $($exceptions.Line -join '; ')" }
    foreach ($screenshot in @($baselinePath,$activePath,$shortHoldPath,$exitedPath)) {
        if (-not (Test-Path -LiteralPath $screenshot -PathType Leaf) -or (Get-Item -LiteralPath $screenshot).Length -eq 0) {
            throw "Astral-meditation screenshot is missing or empty: $screenshot"
        }
    }

    [pscustomobject]@{
        Status = 'PASS'
        Baseline = $baselinePath
        Active = $activePath
        ShortHoldStillActive = $shortHoldPath
        Exited = $exitedPath
        Log = $logPath
    }
}
finally {
    [WofAstralProbeInput]::SetKey($leftControl, $false)
    [WofAstralProbeInput]::SetKey($wKey, $false)
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
