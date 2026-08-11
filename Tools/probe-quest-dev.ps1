param(
    [int]$Width = 1280,
    [int]$Height = 720,
    [string]$OutputRoot = 'D:\tmp\wof-unity\quest-dev-probe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity'
$playerPath = Join-Path $projectRoot 'Builds\Windows\WizardsOnlyFools.exe'
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$allowedRoot = [System.IO.Path]::GetFullPath('D:\tmp\wof-unity')
if (-not $resolvedOutputRoot.StartsWith($allowedRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output root must remain below $allowedRoot"
}
if ($Width -lt 960 -or $Height -lt 560) {
    throw 'Quest-development desktop probes require at least 960x560.'
}
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) {
    throw "Windows player is missing: $playerPath"
}
if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutputRoot | Out-Null

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$profileRoot = Join-Path $resolvedOutputRoot 'profile'
$questRoot = Join-Path $resolvedOutputRoot 'quest-data'
foreach ($path in @($powerShellTempRoot, $profileRoot, $questRoot)) {
    New-Item -ItemType Directory -Path $path | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
$automaticCapturePath = Join-Path $resolvedOutputRoot 'automatic-open.png'
$interactedCapturePath = Join-Path $resolvedOutputRoot 'physical-add-save-test.png'
$reopenedCapturePath = Join-Path $resolvedOutputRoot 'physical-command-reopen.png'

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofQuestDevProbeInput {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern short VkKeyScan(char character);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

  public static bool ForceForeground(IntPtr hWnd) {
    IntPtr foreground = GetForegroundWindow();
    uint currentThread = GetCurrentThreadId();
    uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
    bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
    try {
      ShowWindowAsync(hWnd, 9);
      BringWindowToTop(hWnd);
      SetForegroundWindow(hWnd);
      return GetForegroundWindow() == hWnd;
    }
    finally {
      if (attached) AttachThreadInput(currentThread, foregroundThread, false);
    }
  }

  public static void ClickClient(IntPtr hWnd, int clientX, int clientY) {
    POINT point = new POINT { X = clientX, Y = clientY };
    if (!ClientToScreen(hWnd, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(120);
  }

  public static void SendKey(byte virtualKey) {
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    keybd_event(virtualKey, scanCode, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(55);
    keybd_event(virtualKey, scanCode, 0x0002, UIntPtr.Zero);
    System.Threading.Thread.Sleep(55);
  }

  public static void SendText(string value) {
    foreach (char character in value) {
      short encoded = VkKeyScan(character);
      if (encoded == -1) throw new InvalidOperationException("Cannot type character: " + character);
      byte virtualKey = (byte)(encoded & 0xff);
      byte modifiers = (byte)((encoded >> 8) & 0xff);
      if ((modifiers & 1) != 0) keybd_event(0x10, (byte)MapVirtualKey(0x10, 0), 0, UIntPtr.Zero);
      SendKey(virtualKey);
      if ((modifiers & 1) != 0) keybd_event(0x10, (byte)MapVirtualKey(0x10, 0), 0x0002, UIntPtr.Zero);
    }
  }
}
'@
[WofQuestDevProbeInput]::SetProcessDPIAware() | Out-Null

function Get-LogCount {
    param([string]$Pattern)
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { return 0 }
    return @(Select-String -LiteralPath $logPath -SimpleMatch $Pattern).Count
}

function Wait-LogMarker {
    param([string]$Pattern, [int]$PreviousCount = -1, [int]$Seconds = 15)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $count = Get-LogCount -Pattern $Pattern
        $process.Refresh()
        $found = if ($PreviousCount -lt 0) { $count -gt 0 } else { $count -gt $PreviousCount }
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

function Save-ClientImage {
    param([IntPtr]$WindowHandle, [string]$Path)
    $rect = New-Object WofQuestDevProbeInput+RECT
    if (-not [WofQuestDevProbeInput]::GetClientRect($WindowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofQuestDevProbeInput+POINT
    if (-not [WofQuestDevProbeInput]::ClientToScreen($WindowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $clientWidth = $rect.Right - $rect.Left
    $clientHeight = $rect.Bottom - $rect.Top
    if ($clientWidth -ne $Width -or $clientHeight -ne $Height) {
        throw "Unexpected client dimensions: ${clientWidth}x${clientHeight}; expected ${Width}x${Height}."
    }
    $bitmap = New-Object System.Drawing.Bitmap $clientWidth, $clientHeight
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

function Get-CardPoint {
    param([float]$CardX, [float]$CardY)
    $fit = [Math]::Min(1.0, [Math]::Min(($Width - 24.0) / 1180.0, ($Height - 24.0) / 696.0))
    $left = ($Width - (1180.0 * $fit)) / 2.0
    $top = ($Height - (696.0 * $fit)) / 2.0
    return [pscustomobject]@{
        X = [int][Math]::Round($left + ($CardX * $fit))
        Y = [int][Math]::Round($top + ($CardY * $fit))
    }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=120', '--wof-quest-dev-probe',
    "--wof-profile-root=$profileRoot", '--wof-quest-dev-root', $questRoot,
    '--wof-quest-dev-capture', $automaticCapturePath, '-logFile', $logPath
)
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -PassThru

try {
    if (-not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] SESSION_READY mode=Solo' -Seconds 45)) {
        throw 'Quest-development player did not enter a solo session.'
    }
    if (-not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] QUEST_DEV_PROBE_COMPLETE open=true saved=true points=2 flag=true eventCount=2' -Seconds 20)) {
        throw 'The built-in quest-development fixture did not complete.'
    }
    $captureDeadline = [DateTime]::UtcNow.AddSeconds(8)
    while (-not (Test-Path -LiteralPath $automaticCapturePath -PathType Leaf) -and [DateTime]::UtcNow -lt $captureDeadline) {
        Start-Sleep -Milliseconds 100
    }

    $process.Refresh()
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Quest-development player has no main window.' }
    if (-not [WofQuestDevProbeInput]::ForceForeground($windowHandle)) { throw 'Could not focus the Unity player.' }
    Start-Sleep -Milliseconds 400
    Save-ClientImage -WindowHandle $windowHandle -Path $automaticCapturePath

    $addPoint = Get-CardPoint -CardX 74 -CardY 396
    # Use the left side of Save because the live minimap visually overlaps its right half.
    $save = Get-CardPoint -CardX 1024 -CardY 30
    $testPoint = Get-CardPoint -CardX 1036 -CardY 641

    [WofQuestDevProbeInput]::ClickClient($windowHandle, $addPoint.X, $addPoint.Y)
    $thirdPointSaveMarker = '[WOF-AUTOMATION] QUEST_DEV_PROGRAM_SAVED npc=manual-dev-npc points=3'
    $saveCount = Get-LogCount -Pattern $thirdPointSaveMarker
    [WofQuestDevProbeInput]::ClickClient($windowHandle, $save.X, $save.Y)
    if (-not (Wait-LogMarker -Pattern $thirdPointSaveMarker -PreviousCount $saveCount -Seconds 8)) {
        throw 'Physical Add Point + Save did not persist a third point.'
    }
    $testCount = Get-LogCount -Pattern '[WOF-AUTOMATION] QUEST_DEV_POINT_TEST npc=manual-dev-npc'
    [WofQuestDevProbeInput]::ClickClient($windowHandle, $testPoint.X, $testPoint.Y)
    if (-not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] QUEST_DEV_POINT_TEST npc=manual-dev-npc' -PreviousCount $testCount -Seconds 8)) {
        throw 'Physical Test Point did not execute the selected point.'
    }
    Save-ClientImage -WindowHandle $windowHandle -Path $interactedCapturePath

    [WofQuestDevProbeInput]::SendKey(0x1B)
    if (-not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] QUEST_DEV_EDITOR_CLOSED' -Seconds 8)) {
        throw 'Physical Close did not close the quest editor.'
    }

    $consoleOpenCount = Get-LogCount -Pattern '[WOF-AUTOMATION] COMMAND_CONSOLE_OPEN value=/'
    [WofQuestDevProbeInput]::SendKey(0xBF)
    if (-not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] COMMAND_CONSOLE_OPEN value=/' -PreviousCount $consoleOpenCount -Seconds 8)) {
        throw 'Physical Slash did not open the command console.'
    }
    [WofQuestDevProbeInput]::SendText('questdev open physical-reopen')
    [WofQuestDevProbeInput]::SendKey(0x0D)
    if (-not (Wait-LogMarker -Pattern 'action=OpenQuestNpcEditor' -Seconds 8) -or
        -not (Wait-LogMarker -Pattern '[WOF-AUTOMATION] QUEST_DEV_EDITOR_OPEN npc=physical-reopen points=1' -Seconds 8)) {
        throw 'Physical /questdev open command did not reopen a new NPC editor.'
    }
    Start-Sleep -Milliseconds 350
    Save-ClientImage -WindowHandle $windowHandle -Path $reopenedCapturePath

    $programPath = Join-Path $questRoot 'quest-npc-programs-v1.json'
    if (-not (Test-Path -LiteralPath $programPath -PathType Leaf)) { throw 'Quest program save JSON is missing.' }
    $programData = Get-Content -LiteralPath $programPath -Raw | ConvertFrom-Json
    $manualProgram = @($programData.programs | Where-Object { $_.npcId -eq 'manual-dev-npc' })
    if ($manualProgram.Count -ne 1 -or @($manualProgram[0].scriptPoints).Count -ne 3) {
        throw 'The physically saved manual NPC program does not contain three points.'
    }

    $fatalPatterns = @('NullReferenceException', 'InvalidOperationException', 'ArgumentException:', 'QUEST_DEV_PROBE_FAILED')
    foreach ($pattern in $fatalPatterns) {
        if (Select-String -LiteralPath $logPath -SimpleMatch $pattern -Quiet) {
            throw "Quest-development probe found a runtime failure matching '$pattern'."
        }
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force }
    }
}

[pscustomobject]@{
    status = 'passed'
    size = "${Width}x${Height}"
    savedPointCount = 3
    automaticCapture = $automaticCapturePath
    interactedCapture = $interactedCapturePath
    reopenedCapture = $reopenedCapturePath
    program = Join-Path $questRoot 'quest-npc-programs-v1.json'
    log = $logPath
} | ConvertTo-Json -Depth 4
