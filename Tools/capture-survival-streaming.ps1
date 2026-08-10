param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [int]$ChunkX = 7,
    [int]$ChunkZ = 4
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Survival streaming capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ("streaming-$ChunkX-$ChunkZ-profile-" + [Guid]::NewGuid().ToString('N'))
foreach ($requiredRoot in @($powerShellTempRoot, $playerTempRoot, $logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofSurvivalStreamingCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@
[WofSurvivalStreamingCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $logRoot ("survival-streaming-$ChunkX-$ChunkZ-capture.log")
$capturePath = Join-Path $resolvedOutputRoot ("survival-streaming-$ChunkX-$ChunkZ-desktop.png")
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Streaming QA","questUnlockedSpells":[],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $capturePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', "--wof-survival-streaming-probe=$ChunkX,$ChunkZ", '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $positionedMarker = "SURVIVAL_STREAMING_PROBE_POSITIONED chunk=${ChunkX}:${ChunkZ}"
    $readyMarker = "SURVIVAL_STREAM_WINDOW_READY center=${ChunkX}:${ChunkZ}"
    $deadline = [DateTime]::UtcNow.AddSeconds(70)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch $positionedMarker -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch $readyMarker -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 100) -join [Environment]::NewLine
        } else { '<streaming probe log was not created>' }
        throw "Survival streaming probe did not become ready at ${ChunkX}:${ChunkZ}.`n$tail"
    }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Survival streaming player has no main window.' }
    [WofSurvivalStreamingCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofSurvivalStreamingCapture]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 3200

    $rect = New-Object WofSurvivalStreamingCapture+RECT
    if (-not [WofSurvivalStreamingCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofSurvivalStreamingCapture+POINT
    if (-not [WofSurvivalStreamingCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
    [WofSurvivalStreamingCapture]::SetCursorPos($point.X + [int]($width / 2), $point.Y + [int]($height / 2)) | Out-Null
    [WofSurvivalStreamingCapture]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [WofSurvivalStreamingCapture]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            if (-not [WofSurvivalStreamingCapture]::PrintWindow($windowHandle, $deviceContext, 3)) {
                throw 'PrintWindow failed to capture the Unity client.'
            }
        }
        finally { $graphics.ReleaseHdc($deviceContext) }
        $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    $readyLine = Select-String -LiteralPath $logPath -SimpleMatch $readyMarker | Select-Object -Last 1
    [PSCustomObject]@{
        Chunk = "${ChunkX}:${ChunkZ}"
        Capture = $capturePath
        Log = $logPath
        Width = $width
        Height = $height
        Bytes = (Get-Item -LiteralPath $capturePath).Length
        Ready = $readyLine.Line
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
