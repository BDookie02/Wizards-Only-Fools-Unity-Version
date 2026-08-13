param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\pause-scoreboard-hands',
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

  public static void TapExtended(byte key) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, 1, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    keybd_event(key, scan, 3, UIntPtr.Zero);
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
$profileRoot = Join-Path $resolvedOutputRoot ('profile-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
$handPath = Join-Path $resolvedOutputRoot 'idle-hands-frame.png'
$pausePath = Join-Path $resolvedOutputRoot 'pause-menu.png'
$settingsPath = Join-Path $resolvedOutputRoot 'settings-video.png'
$keybindsPath = Join-Path $resolvedOutputRoot 'settings-keybinds.png'
$voicePath = Join-Path $resolvedOutputRoot 'settings-voice.png'
$voiceEnabledPath = Join-Path $resolvedOutputRoot 'settings-voice-enabled-remapped.png'
$characterPath = Join-Path $resolvedOutputRoot 'settings-character.png'
$scorePath = Join-Path $resolvedOutputRoot 'scoreboard-held.png'
foreach ($target in @($logPath, $handPath, $pausePath, $settingsPath, $keybindsPath, $voicePath, $voiceEnabledPath, $characterPath, $scorePath)) {
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
    $isWindowedProbe = $width -eq $Width -and $height -eq $Height
    $isFullscreenProbe = $width -eq 1920 -and $height -eq 1080
    if (-not $isWindowedProbe -and -not $isFullscreenProbe) { throw "Unexpected Unity client size: ${width}x${height}." }
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

function Set-WofFocus([IntPtr]$Handle) {
    if (-not [WofPauseScoreCapture]::Focus($Handle)) { throw 'Could not focus the Unity player.' }
    Start-Sleep -Milliseconds 180
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-hand-idle-probe', '--wof-auto-exit=90', "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    if (-not (Wait-WofMarker -Pattern 'PLAYER_SPAWN' -Seconds 35)) { throw 'Windows player did not spawn.' }
    if (-not (Wait-WofMarker -Pattern 'HAND_IDLE_FRAME index=3' -Seconds 5)) { throw 'Outward equipped hands did not advance through the complete four-frame idle loop.' }
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Windows player has no window.' }
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $handPath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::Tap(0x1B)
    if (-not (Wait-WofMarker -Pattern 'PAUSE_MENU open=True' -Seconds 5)) { throw 'Physical Escape did not open the pause menu.' }
    Start-Sleep -Milliseconds 250
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $pausePath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::TapExtended(0x28)
    Start-Sleep -Milliseconds 250
    [WofPauseScoreCapture]::Tap(0x0D)
    if (-not (Wait-WofMarker -Pattern 'SETTINGS_MENU open=True' -Seconds 5)) { throw 'Physical Down/Enter did not open Settings.' }
    Start-Sleep -Milliseconds 1200
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $settingsPath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::TapExtended(0x27)
    if (-not (Wait-WofMarker -Pattern 'SETTINGS_PANE pane=Keybinds' -Seconds 5)) { throw 'Physical Right did not open Keybinds.' }
    Start-Sleep -Milliseconds 1200
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $keybindsPath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::TapExtended(0x27)
    if (-not (Wait-WofMarker -Pattern 'SETTINGS_PANE pane=Voice' -Seconds 5)) { throw 'Physical Right did not open Voice.' }
    Start-Sleep -Milliseconds 1200
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $voicePath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::TapExtended(0x28)
    [WofPauseScoreCapture]::TapExtended(0x28)
    [WofPauseScoreCapture]::Tap(0x0D)
    if (-not (Wait-WofMarker -Pattern 'VOICE_ENABLED enabled=True' -Seconds 5)) { throw 'Physical Down/Enter did not enable voice.' }
    if (-not (Wait-WofMarker -Pattern 'VOICE_STATUS STATUS: WAITING FOR MULTIPLAYER SESSION' -Seconds 5)) { throw 'Enabled solo voice did not fail closed while waiting for a multiplayer session.' }

    [WofPauseScoreCapture]::TapExtended(0x28)
    [WofPauseScoreCapture]::TapExtended(0x28)
    [WofPauseScoreCapture]::Tap(0x0D)
    Start-Sleep -Milliseconds 350
    [WofPauseScoreCapture]::Tap(0x4E)
    if (-not (Wait-WofMarker -Pattern 'VOICE_KEY_BINDING key=N' -Seconds 5)) { throw 'Physical keyboard remap did not bind voice push-to-talk to N.' }
    Start-Sleep -Milliseconds 600
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $voiceEnabledPath

    Set-WofFocus -Handle $handle
    1..4 | ForEach-Object { [WofPauseScoreCapture]::TapExtended(0x26) }

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::TapExtended(0x27)
    if (-not (Wait-WofMarker -Pattern 'SETTINGS_PANE pane=Character' -Seconds 5)) { throw 'Physical Right did not open Character.' }
    Start-Sleep -Milliseconds 1200
    Set-WofFocus -Handle $handle
    Save-WofWindow -Handle $handle -Path $characterPath

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::Tap(0x1B)
    if (-not (Wait-WofMarker -Pattern 'SETTINGS_MENU open=False' -Count 2 -Seconds 5)) { throw 'Physical Escape did not return from Settings.' }

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::Tap(0x1B)
    if (-not (Wait-WofMarker -Pattern 'PAUSE_MENU open=False' -Seconds 5)) { throw 'Second physical Escape did not close the pause menu.' }

    Set-WofFocus -Handle $handle
    [WofPauseScoreCapture]::SetKey(0x09, $true)
    try {
        if (-not (Wait-WofMarker -Pattern 'SCOREBOARD_MENU open=True' -Seconds 5)) { throw 'Physical Tab hold did not open the player list.' }
        Start-Sleep -Milliseconds 250
        Set-WofFocus -Handle $handle
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
        SettingsVideo = $settingsPath
        SettingsKeybinds = $keybindsPath
        SettingsVoice = $voicePath
        SettingsVoiceEnabledRemapped = $voiceEnabledPath
        SettingsCharacter = $characterPath
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
