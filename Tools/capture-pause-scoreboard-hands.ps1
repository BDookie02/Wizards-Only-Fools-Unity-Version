param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\pause-scoreboard-hands'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Pause/scoreboard/hand verification paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofPauseScoreCapture {
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

  public static void SetKey(byte key, bool pressed) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, pressed ? 0u : 2u, UIntPtr.Zero);
  }
}
'@
[WofPauseScoreCapture]::SetProcessDPIAware() | Out-Null

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$handPath = Join-Path $resolvedOutputRoot 'idle-hands-frame.png'
$pausePath = Join-Path $resolvedOutputRoot 'pause-menu.png'
$scorePath = Join-Path $resolvedOutputRoot 'scoreboard-held.png'
foreach ($target in @($logPath, $handPath, $pausePath, $scorePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
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

function Save-WofWindow([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object WofPauseScoreCapture+RECT
    if (-not [WofPauseScoreCapture]::GetClientRect($Handle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofPauseScoreCapture+POINT
    if (-not [WofPauseScoreCapture]::ClientToScreen($Handle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected Unity client size: ${width}x${height}." }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
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
    '--wof-solo', '--wof-hand-idle-probe', '--wof-auto-exit=90', '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    if (-not (Wait-WofMarker -Pattern 'PLAYER_SPAWN' -Seconds 35)) { throw 'Windows player did not spawn.' }
    if (-not (Wait-WofMarker -Pattern 'HAND_IDLE_FRAME index=3' -Seconds 5)) { throw 'Outward equipped hands did not advance through the complete four-frame idle loop.' }
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Windows player has no window.' }
    if (-not [WofPauseScoreCapture]::Focus($handle)) { throw 'Could not focus the Unity player.' }
    Start-Sleep -Milliseconds 250
    Save-WofWindow -Handle $handle -Path $handPath

    [WofPauseScoreCapture]::Tap(0x1B)
    if (-not (Wait-WofMarker -Pattern 'PAUSE_MENU open=True' -Seconds 5)) { throw 'Physical Escape did not open the pause menu.' }
    Start-Sleep -Milliseconds 250
    Save-WofWindow -Handle $handle -Path $pausePath
    [WofPauseScoreCapture]::Tap(0x1B)
    if (-not (Wait-WofMarker -Pattern 'PAUSE_MENU open=False' -Seconds 5)) { throw 'Second physical Escape did not close the pause menu.' }

    [WofPauseScoreCapture]::SetKey(0x09, $true)
    try {
        if (-not (Wait-WofMarker -Pattern 'SCOREBOARD_MENU open=True' -Seconds 5)) { throw 'Physical Tab hold did not open the player list.' }
        Start-Sleep -Milliseconds 250
        Save-WofWindow -Handle $handle -Path $scorePath
    }
    finally {
        [WofPauseScoreCapture]::SetKey(0x09, $false)
    }
    if (-not (Wait-WofMarker -Pattern 'SCOREBOARD_MENU open=False' -Seconds 5)) { throw 'Physical Tab release did not close the player list.' }

    $exceptions = @(Select-String -LiteralPath $logPath -Pattern 'NullReferenceException|MissingReferenceException|ArgumentException|IndexOutOfRangeException')
    if ($exceptions.Count -gt 0) { throw "Runtime exceptions detected: $($exceptions.Line -join '; ')" }
    [pscustomobject]@{
        Status = 'PASS'
        IdleHands = $handPath
        PauseMenu = $pausePath
        ScoreboardHeld = $scorePath
        Log = $logPath
    }
}
finally {
    [WofPauseScoreCapture]::SetKey(0x09, $false)
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
