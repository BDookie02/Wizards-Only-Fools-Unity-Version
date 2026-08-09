param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\urgent-playable-desktop'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Desktop interaction capture paths must stay on D:.'
}

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofUrgentDesktopCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  public static void Focus(IntPtr hWnd) { ShowWindowAsync(hWnd, 9); BringWindowToTop(hWnd); SetForegroundWindow(hWnd); }
  public static void Tap(byte key) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    keybd_event(key, scan, 2, UIntPtr.Zero);
  }
}
'@
[WofUrgentDesktopCapture]::SetProcessDPIAware() | Out-Null

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$spellPath = Join-Path $resolvedOutputRoot 'keyboard-spell-menu.png'
$mapPath = Join-Path $resolvedOutputRoot 'keyboard-navigation-map.png'
foreach ($target in @($logPath, $spellPath, $mapPath)) {
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

function Save-WofWindow([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object WofUrgentDesktopCapture+RECT
    if (-not [WofUrgentDesktopCapture]::GetClientRect($Handle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofUrgentDesktopCapture+POINT
    if (-not [WofUrgentDesktopCapture]::ClientToScreen($Handle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
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
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=60', '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    if (-not (Wait-WofMarker -Pattern 'PLAYER_SPAWN' -Seconds 35)) { throw 'Desktop player did not spawn.' }
    Start-Sleep -Milliseconds 750
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Desktop player has no window.' }
    [WofUrgentDesktopCapture]::Focus($handle)
    Start-Sleep -Milliseconds 300

    [WofUrgentDesktopCapture]::Tap(0x45)
    if (-not (Wait-WofMarker -Pattern 'SPELL_MENU open=True' -Seconds 5)) { throw 'Physical E did not open spell menu.' }
    Start-Sleep -Milliseconds 400
    Save-WofWindow -Handle $handle -Path $spellPath

    [WofUrgentDesktopCapture]::Tap(0x45)
    if (-not (Wait-WofMarker -Pattern 'SPELL_MENU open=False' -Seconds 5)) { throw 'Second physical E did not close spell menu.' }
    [WofUrgentDesktopCapture]::Tap(0x4D)
    if (-not (Wait-WofMarker -Pattern 'NAVIGATION_MAP expanded=True' -Seconds 5)) { throw 'Physical M did not open navigation map.' }
    Start-Sleep -Milliseconds 700
    Save-WofWindow -Handle $handle -Path $mapPath

    [WofUrgentDesktopCapture]::Tap(0x4D)
    if (-not (Wait-WofMarker -Pattern 'NAVIGATION_MAP expanded=False' -Seconds 5)) { throw 'Second physical M did not close navigation map.' }

    [pscustomobject]@{
        Status = 'PASS'
        SpellMenu = $spellPath
        NavigationMap = $mapPath
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
