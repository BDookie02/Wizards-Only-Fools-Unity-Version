param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateSet('exterior', 'tunnel')]
    [string]$View = 'exterior'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Lily Coil capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofLilyCoilCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
}
'@
[WofLilyCoilCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ("lily-coil-$View-capture-profile-" + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot "lily-coil-$View-capture.log"
$capturePath = Join-Path $resolvedOutputRoot "lily-coil-$View-desktop.png"
$motionCapturePath = Join-Path $resolvedOutputRoot "lily-coil-$View-desktop-motion-frame-b.png"
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
foreach ($requiredRoot in @($logRoot, $profileRoot, $playerTempRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Lily Coil QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $capturePath, $motionCapturePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$probeArgument = if ($View -eq 'tunnel') { '--wof-lily-coil-view-probe=tunnel' } else { '--wof-lily-coil-view-probe' }
$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', $probeArgument, '--wof-lily-ambient-motion-probe', '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$positionedMarker = "LILY_COIL_PROBE_POSITIONED variant=$View"
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch $positionedMarker -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'LILY_COIL_SCENE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw "Lily Coil $View view probe did not become ready." }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Lily Coil player has no main window.' }
    [WofLilyCoilCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofLilyCoilCapture]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 4200

    $rect = New-Object WofLilyCoilCapture+RECT
    if (-not [WofLilyCoilCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofLilyCoilCapture+POINT
    if (-not [WofLilyCoilCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
    function Save-ClientCapture([string]$Path) {
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
    Save-ClientCapture $capturePath
    Start-Sleep -Milliseconds 1700
    Save-ClientCapture $motionCapturePath

    $motionDeadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 100
        $motionPassed = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'LILY_AMBIENT_MOTION_PASS' -Quiet)
    } while (-not $motionPassed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $motionDeadline)
    if (-not $motionPassed) { throw 'Lily Coil ambient matrices did not prove motion.' }
    $firstHash = (Get-FileHash -LiteralPath $capturePath -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $motionCapturePath -Algorithm SHA256).Hash
    if ($firstHash -eq $secondHash) { throw 'Lily Coil motion frames are pixel-identical.' }

    $motionLine = Select-String -LiteralPath $logPath -SimpleMatch 'LILY_AMBIENT_MOTION_PASS' | Select-Object -Last 1
    [PSCustomObject]@{ View=$View; CaptureA=$capturePath; CaptureB=$motionCapturePath; Log=$logPath; Motion=$motionLine.Line; Width=$width; Height=$height; BytesA=(Get-Item -LiteralPath $capturePath).Length; BytesB=(Get-Item -LiteralPath $motionCapturePath).Length }
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
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
