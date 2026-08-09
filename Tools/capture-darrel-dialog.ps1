param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Darrel capture paths must stay on D:.'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofWindowCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
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

  public static void SendPhysicalKey(byte virtualKey) {
    const uint KeyUp = 0x0002;
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    keybd_event(virtualKey, scanCode, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(virtualKey, scanCode, KeyUp, UIntPtr.Zero);
  }
}
'@
[WofWindowCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('darrel-profile-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot 'darrel-dialog-runtime.log'
$beforePath = Join-Path $resolvedOutputRoot 'darrel-before-dialog.png'
$afterPath = Join-Path $resolvedOutputRoot 'darrel-dialog-open.png'
$jerkPath = Join-Path $resolvedOutputRoot 'darrel-dialog-jerk-response.png'
$jobPath = Join-Path $resolvedOutputRoot 'darrel-dialog-job-offer.png'
$acceptedPath = Join-Path $resolvedOutputRoot 'darrel-dialog-accepted.png'
$restoredPath = Join-Path $resolvedOutputRoot 'darrel-dialog-restored.png'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @($logPath, $beforePath, $afterPath, $jerkPath, $jobPath, $acceptedPath, $restoredPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}

$arguments = @(
    '-force-d3d11',
    '-screen-width',
    '1280',
    '-screen-height',
    '720',
    '-screen-fullscreen',
    '0',
    '--wof-solo',
    '--wof-darrel-dialog-probe',
    '--wof-auto-exit=150',
    "--wof-profile-root=$profileRoot",
    '-logFile',
    $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofClientImage {
    param(
        [Parameter(Mandatory = $true)]
        [IntPtr]$WindowHandle,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $rect = New-Object WofWindowCapture+RECT
    if (-not [WofWindowCapture]::GetClientRect($WindowHandle, [ref]$rect)) {
        throw 'GetClientRect failed.'
    }
    $point = New-Object WofWindowCapture+POINT
    $point.X = 0
    $point.Y = 0
    if (-not [WofWindowCapture]::ClientToScreen($WindowHandle, [ref]$point)) {
        throw 'ClientToScreen failed.'
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid client dimensions: ${width}x${height}."
    }
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
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern 'DARREL_DIALOG_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) {
        throw 'Darrel probe did not become ready.'
    }

    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) {
        throw 'Darrel probe has no main window.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $shell.AppActivate($process.Id) | Out-Null
    [WofWindowCapture]::ForceForeground($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    if ([WofWindowCapture]::GetForegroundWindow() -ne $windowHandle) {
        throw "Unity player did not become the foreground window (pid=$($process.Id))."
    }
    Save-WofClientImage -WindowHandle $windowHandle -Path $beforePath

    [WofWindowCapture]::SendPhysicalKey(0x46)
    $openDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $opened = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern 'QUEST_DIALOG_OPEN npc=-64--48 choices=2' -Quiet)
    } while (-not $opened -and -not $process.HasExited -and [DateTime]::UtcNow -lt $openDeadline)
    if (-not $opened) {
        throw 'Physical F input did not open the Darrel dialog.'
    }
    Start-Sleep -Milliseconds 500
    Save-WofClientImage -WindowHandle $windowHandle -Path $afterPath

    [WofWindowCapture]::SendPhysicalKey(0x31)
    $jerkDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $jerkOpened = Select-String -LiteralPath $logPath -Pattern 'QUEST_DIALOG_CHOICE npc=-64--48 id=darrel-jerk' -Quiet
    } while (-not $jerkOpened -and -not $process.HasExited -and [DateTime]::UtcNow -lt $jerkDeadline)
    if (-not $jerkOpened) {
        throw 'Physical number 1 input did not select the Darrel jerk response.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofClientImage -WindowHandle $windowHandle -Path $jerkPath

    [WofWindowCapture]::SendPhysicalKey(0x31)
    $jobDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $jobOpened = Select-String -LiteralPath $logPath -Pattern 'QUEST_DIALOG_CHOICE npc=-64--48 id=darrel-two-spells' -Quiet
    } while (-not $jobOpened -and -not $process.HasExited -and [DateTime]::UtcNow -lt $jobDeadline)
    if (-not $jobOpened) {
        throw 'Physical number 1 input did not advance to Darrel job offer.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofClientImage -WindowHandle $windowHandle -Path $jobPath

    [WofWindowCapture]::SendPhysicalKey(0x31)
    $acceptedDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $accepted = (Select-String -LiteralPath $logPath -Pattern 'QUEST_DIALOG_CHOICE npc=-64--48 id=darrel-accept-job' -Quiet) -and
            (Select-String -LiteralPath $logPath -Pattern 'DARREL_QUEST_ACCEPTED spell=healingcrystals' -Quiet)
    } while (-not $accepted -and -not $process.HasExited -and [DateTime]::UtcNow -lt $acceptedDeadline)
    if (-not $accepted) {
        throw 'Physical number 1 input did not accept Darrel quest.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofClientImage -WindowHandle $windowHandle -Path $acceptedPath

    [WofWindowCapture]::SendPhysicalKey(0x1B)
    $closedDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $closed = Select-String -LiteralPath $logPath -Pattern 'QUEST_DIALOG_CLOSED npc=-64--48' -Quiet
    } while (-not $closed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $closedDeadline)
    if (-not $closed) {
        throw 'Physical Escape input did not close the Darrel dialog.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofClientImage -WindowHandle $windowHandle -Path $restoredPath

    [PSCustomObject]@{
        ProcessId = $process.Id
        Ready = $ready
        DialogOpened = $opened
        Before = $beforePath
        After = $afterPath
        JerkResponse = $jerkPath
        JobOffer = $jobPath
        Accepted = $acceptedPath
        Restored = $restoredPath
        BeforeBytes = (Get-Item -LiteralPath $beforePath).Length
        AfterBytes = (Get-Item -LiteralPath $afterPath).Length
        JerkResponseBytes = (Get-Item -LiteralPath $jerkPath).Length
        JobOfferBytes = (Get-Item -LiteralPath $jobPath).Length
        AcceptedBytes = (Get-Item -LiteralPath $acceptedPath).Length
        RestoredBytes = (Get-Item -LiteralPath $restoredPath).Length
        Log = $logPath
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) {
            Stop-Process -Id $process.Id -Force
        }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
}
