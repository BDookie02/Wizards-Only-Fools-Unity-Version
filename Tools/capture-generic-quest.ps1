param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Generic quest capture paths must stay on D:.'
}

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofGenericQuestCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

  public static bool ForceForeground(IntPtr hWnd) {
    IntPtr foreground = GetForegroundWindow();
    uint currentThread = GetCurrentThreadId();
    uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
    bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                    AttachThreadInput(currentThread, foregroundThread, true);
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

  public static void SendPhysicalF() {
    const byte F = 0x46;
    const uint KeyUp = 0x0002;
    byte scanCode = (byte)MapVirtualKey(F, 0);
    keybd_event(F, scanCode, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(F, scanCode, KeyUp, UIntPtr.Zero);
  }
}
'@
[WofGenericQuestCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('generic-quest-profile-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot 'generic-quest-runtime.log'
$beforePath = Join-Path $resolvedOutputRoot 'generic-quest-before.png'
$afterPath = Join-Path $resolvedOutputRoot 'generic-quest-assigned.png'
$repeatPath = Join-Path $resolvedOutputRoot 'generic-quest-repeat.png'
$expiredPath = Join-Path $resolvedOutputRoot 'generic-quest-expired.png'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Quest QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $beforePath, $afterPath, $repeatPath, $expiredPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-villager-view-probe', '--wof-auto-exit=120',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofGenericQuestImage {
    param([IntPtr]$WindowHandle, [string]$Path)
    $rect = New-Object WofGenericQuestCapture+RECT
    if (-not [WofGenericQuestCapture]::GetClientRect($WindowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofGenericQuestCapture+POINT
    if (-not [WofGenericQuestCapture]::ClientToScreen($WindowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "Invalid client dimensions: ${width}x${height}." }
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

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern 'VILLAGER_VIEW_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Generic villager view probe did not become ready.' }

    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Generic villager view probe has no main window.' }
    [WofGenericQuestCapture]::ForceForeground($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    if ([WofGenericQuestCapture]::GetForegroundWindow() -ne $windowHandle) {
        throw "Unity player did not become the foreground window (pid=$($process.Id))."
    }
    Save-WofGenericQuestImage -WindowHandle $windowHandle -Path $beforePath

    [WofGenericQuestCapture]::SendPhysicalF()
    $interactionDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $interacted = Select-String -LiteralPath $logPath -Pattern 'GENERIC_QUEST_INTERACTION npc=48-64.*assignment=(?!none)\S+.*messages=3.*changed=True' -Quiet
    } while (-not $interacted -and -not $process.HasExited -and [DateTime]::UtcNow -lt $interactionDeadline)
    if (-not $interacted) { throw 'Physical F input did not interact with the targeted generic villager.' }
    Start-Sleep -Milliseconds 500
    Save-WofGenericQuestImage -WindowHandle $windowHandle -Path $afterPath

    Start-Sleep -Milliseconds 800
    [WofGenericQuestCapture]::SendPhysicalF()
    $repeatDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $repeatMarkers = @(Select-String -LiteralPath $logPath -Pattern 'GENERIC_QUEST_INTERACTION npc=48-64.*assignment=(?!none)\S+.*messages=2.*changed=False')
        $repeated = $repeatMarkers.Count -ge 1
    } while (-not $repeated -and -not $process.HasExited -and [DateTime]::UtcNow -lt $repeatDeadline)
    if (-not $repeated) { throw 'Second physical F input did not produce the canonical repeat interaction.' }
    Start-Sleep -Milliseconds 500
    Save-WofGenericQuestImage -WindowHandle $windowHandle -Path $repeatPath

    Start-Sleep -Milliseconds 12200
    Save-WofGenericQuestImage -WindowHandle $windowHandle -Path $expiredPath

    $marker = Select-String -LiteralPath $logPath -Pattern 'GENERIC_QUEST_INTERACTION npc=48-64.*assignment=(?!none)\S+.*messages=3.*changed=True' |
        Select-Object -Last 1 -ExpandProperty Line
    [PSCustomObject]@{
        ProcessId = $process.Id
        Ready = $ready
        Interacted = $interacted
        Before = $beforePath
        After = $afterPath
        Repeat = $repeatPath
        Expired = $expiredPath
        BeforeBytes = (Get-Item -LiteralPath $beforePath).Length
        AfterBytes = (Get-Item -LiteralPath $afterPath).Length
        RepeatBytes = (Get-Item -LiteralPath $repeatPath).Length
        ExpiredBytes = (Get-Item -LiteralPath $expiredPath).Length
        Marker = $marker
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
}
