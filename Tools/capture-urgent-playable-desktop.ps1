param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\urgent-playable-desktop',
    [ValidateRange(320, 7680)]
    [int]$Width = 1280,
    [ValidateRange(240, 4320)]
    [int]$Height = 720,
    [switch]$MapOnly,
    [switch]$TravelMountain
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Desktop interaction capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofUrgentDesktopCapture {
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
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
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
  public static void Tap(byte key) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    keybd_event(key, scan, 2, UIntPtr.Zero);
  }
  public static void ClickClient(IntPtr hWnd, int x, int y) {
    POINT point = new POINT { X = x, Y = y };
    if (!ClientToScreen(hWnd, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
}
'@
[WofUrgentDesktopCapture]::SetProcessDPIAware() | Out-Null

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$spellPath = Join-Path $resolvedOutputRoot 'keyboard-spell-menu.png'
$mapPath = Join-Path $resolvedOutputRoot 'keyboard-navigation-map.png'
$mountainMapPath = Join-Path $resolvedOutputRoot 'keyboard-navigation-map-after-mountain.png'
foreach ($target in @($logPath, $spellPath, $mapPath, $mountainMapPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

function Wait-WofMarker([string]$Pattern, [int]$Seconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $found = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern $Pattern -Quiet)
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

function Wait-WofMarkerCount([string]$Pattern, [int]$Count, [int]$Seconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $matches = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            @(Select-String -LiteralPath $logPath -Pattern $Pattern)
        } else { @() }
    } while ($matches.Count -lt $Count -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $matches.Count -ge $Count
}

function Save-WofWindow([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object WofUrgentDesktopCapture+RECT
    if (-not [WofUrgentDesktopCapture]::GetClientRect($Handle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofUrgentDesktopCapture+POINT
    if (-not [WofUrgentDesktopCapture]::ClientToScreen($Handle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            if (-not [WofUrgentDesktopCapture]::PrintWindow($Handle, $deviceContext, 3)) {
                throw 'PrintWindow failed to capture the Unity client.'
            }
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=60', '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    if (-not (Wait-WofMarker -Pattern 'PLAYER_SPAWN' -Seconds 35)) { throw 'Desktop player did not spawn.' }
    Start-Sleep -Milliseconds 750
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Desktop player has no window.' }
    if (-not [WofUrgentDesktopCapture]::Focus($handle)) { throw 'Could not focus the Unity player window.' }
    Start-Sleep -Milliseconds 300

    if (-not $MapOnly) {
        [WofUrgentDesktopCapture]::Tap(0x45)
        if (-not (Wait-WofMarker -Pattern 'SPELL_MENU open=True' -Seconds 5)) { throw 'Physical E did not open spell menu.' }
        Start-Sleep -Milliseconds 400
        Save-WofWindow -Handle $handle -Path $spellPath

        [WofUrgentDesktopCapture]::Tap(0x45)
        if (-not (Wait-WofMarker -Pattern 'SPELL_MENU open=False' -Seconds 5)) { throw 'Second physical E did not close spell menu.' }
    }
    [WofUrgentDesktopCapture]::Tap(0x4D)
    if (-not (Wait-WofMarkerCount -Pattern 'NAVIGATION_MAP expanded=True' -Count 1 -Seconds 5)) { throw 'Physical M did not open navigation map.' }
    Start-Sleep -Milliseconds 700
    Save-WofWindow -Handle $handle -Path $mapPath

    if ($TravelMountain) {
        [WofUrgentDesktopCapture]::ClickClient($handle, [Math]::Round($Width * 0.17), [Math]::Round($Height * 0.70))
        if (-not (Wait-WofMarker -Pattern 'MAP_FAST_TRAVEL_UI destination=Mountain' -Seconds 8)) { throw 'Physical Mountain fast-travel click did not complete.' }
        Start-Sleep -Milliseconds 1200
        [WofUrgentDesktopCapture]::Tap(0x4D)
        if (-not (Wait-WofMarkerCount -Pattern 'NAVIGATION_MAP expanded=True' -Count 2 -Seconds 5)) { throw 'Physical M did not reopen navigation map after Mountain travel.' }
        Start-Sleep -Milliseconds 700
        Save-WofWindow -Handle $handle -Path $mountainMapPath
    }

    [WofUrgentDesktopCapture]::Tap(0x4D)
    $expectedCloseCount = if ($TravelMountain) { 2 } else { 1 }
    if (-not (Wait-WofMarkerCount -Pattern 'NAVIGATION_MAP expanded=False' -Count $expectedCloseCount -Seconds 5)) { throw 'Physical M did not close navigation map.' }

    [pscustomobject]@{
        Status = 'PASS'
        SpellMenu = $spellPath
        NavigationMap = $mapPath
        NavigationMapAfterMountain = if ($TravelMountain) { $mountainMapPath } else { $null }
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
