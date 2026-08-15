param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\mana-source-probes',
    [ValidateSet('base', 'well', 'rune')]
    [string]$Kind = 'base',
    [ValidateSet('quality', 'mobile')]
    [string]$PerformanceMode = 'quality',
    [ValidateRange(640, 3840)]
    [int]$Width = 1280,
    [ValidateRange(360, 2160)]
    [int]$Height = 720
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'Mana-source probe requires an existing Windows build and D:-drive output paths.'
}

$probeRoot = Join-Path $resolvedOutputRoot (Join-Path $PerformanceMode $Kind)
$profileRoot = Join-Path $probeRoot ('profile-' + [Guid]::NewGuid().ToString('N'))
$tempRoot = Join-Path $probeRoot 'temp'
foreach ($path in @($probeRoot, $profileRoot, $tempRoot)) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}
$logPath = Join-Path $probeRoot 'player.log'
$beforePath = Join-Path $probeRoot "${Kind}-before-${Width}x${Height}.png"
$afterPath = Join-Path $probeRoot "${Kind}-after-${Width}x${Height}.png"
foreach ($path in @($logPath, $beforePath, $afterPath)) {
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Mana Source QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofManaSourceCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
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
}
'@
[WofManaSourceCapture]::SetProcessDPIAware() | Out-Null

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', "--wof-mana-source-probe=$Kind", '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
if ($PerformanceMode -eq 'mobile') {
    $arguments += @('--wof-mobile-performance', '--wof-mobile-ui')
} else {
    $arguments += '--wof-quality-performance'
}
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    function Wait-LogMarker([string]$Pattern, [int]$Seconds) {
        $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
        do {
            Start-Sleep -Milliseconds 100
            $process.Refresh()
            if ((Test-Path -LiteralPath $logPath -PathType Leaf) -and
                (Select-String -LiteralPath $logPath -SimpleMatch $Pattern -Quiet)) { return $true }
        } while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
        return $false
    }

    if (-not (Wait-LogMarker -Pattern "MANA_SOURCE_PROBE_READY kind=$Kind" -Seconds 55)) {
        throw "Mana-source $Kind probe did not become ready."
    }
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and
             [DateTime]::UtcNow -lt $windowDeadline)
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Mana-source player has no main window.' }
    if (-not [WofManaSourceCapture]::Focus($handle)) {
        throw 'Could not foreground the mana-source player.'
    }
    $rect = New-Object WofManaSourceCapture+RECT
    $point = New-Object WofManaSourceCapture+POINT
    if (-not [WofManaSourceCapture]::GetClientRect($handle, [ref]$rect) -or
        -not [WofManaSourceCapture]::ClientToScreen($handle, [ref]$point)) {
        throw 'Could not resolve the mana-source player client area.'
    }
    $capturedWidth = $rect.Right - $rect.Left
    $capturedHeight = $rect.Bottom - $rect.Top
    if ($capturedWidth -ne $Width -or $capturedHeight -ne $Height) {
        throw "Unexpected client dimensions: ${capturedWidth}x${capturedHeight}."
    }

    function Save-ClientCapture([string]$Path) {
        if (-not [WofManaSourceCapture]::Focus($handle)) {
            throw 'Lost the mana-source player foreground before capture.'
        }
        Start-Sleep -Milliseconds 250
        $bitmap = New-Object System.Drawing.Bitmap $capturedWidth, $capturedHeight
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

    Save-ClientCapture $beforePath
    if (-not (Wait-LogMarker -Pattern "MANA_SOURCE_PROBE_PASS kind=$Kind" -Seconds 20)) {
        throw "Mana-source $Kind probe did not recharge mana."
    }
    Start-Sleep -Milliseconds 150
    Save-ClientCapture $afterPath
    [PSCustomObject]@{
        Kind = $Kind
        PerformanceMode = $PerformanceMode
        Before = $beforePath
        After = $afterPath
        Log = $logPath
        Ready = (Select-String -LiteralPath $logPath -SimpleMatch "MANA_SOURCE_PROBE_READY kind=$Kind" |
            Select-Object -Last 1).Line
        Collected = (Select-String -LiteralPath $logPath -SimpleMatch "MANA_SOURCE_PROBE_PASS kind=$Kind" |
            Select-Object -Last 1).Line
        Source = (Select-String -LiteralPath $logPath -SimpleMatch 'MANA_SOURCE_COLLECTED' |
            Select-Object -Last 1).Line
        BeforeBytes = (Get-Item -LiteralPath $beforePath).Length
        AfterBytes = (Get-Item -LiteralPath $afterPath).Length
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($probeRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
