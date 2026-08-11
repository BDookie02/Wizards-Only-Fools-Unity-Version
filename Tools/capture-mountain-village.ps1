param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateSet('exterior', 'profile', 'summit', 'aerial', 'banquet', 'catwalk')]
    [string]$View = 'exterior',
    [ValidateSet('none', 'left', 'right', 'both')]
    [string]$HandFire = 'none'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mountain village capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofMountainVillageCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@
[WofMountainVillageCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ("mountain-$View-capture-profile-" + [Guid]::NewGuid().ToString('N'))
$captureSuffix = if ($HandFire -eq 'none') { '' } else { "-firing-$HandFire" }
$logPath = Join-Path $logRoot ("mountain-$View$captureSuffix-capture.log")
$capturePath = Join-Path $resolvedOutputRoot ("mountain-$View$captureSuffix-desktop.png")
$capturePathAfter = Join-Path $resolvedOutputRoot ("mountain-$View$captureSuffix-desktop-after.png")
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$requiresSnowProbe = $View -notin @('banquet', 'catwalk')
foreach ($requiredRoot in @($logRoot, $profileRoot, $playerTempRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Mountain QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $capturePath, $capturePathAfter)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$viewProbeArgument = if ($View -eq 'exterior') {
    '--wof-mountain-village-view-probe'
} else {
    "--wof-mountain-village-view-probe=$View"
}
$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', $viewProbeArgument, '--wof-mountain-access-path-probe', '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
if ($requiresSnowProbe) {
    $arguments += '--wof-mountain-snow-probe'
}
if ($HandFire -ne 'none') {
    $arguments += "--wof-hand-fire-probe=$HandFire"
}
$positionedMarker = "MOUNTAIN_VILLAGE_PROBE_POSITIONED variant=$View"
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
            (Select-String -LiteralPath $logPath -SimpleMatch 'MOUNTAIN_VILLAGE_SCENE_READY' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'MOUNTAIN_ACCESS_PATH_CONTINUITY_PASS' -Quiet) -and
            (-not $requiresSnowProbe -or
                (Select-String -LiteralPath $logPath -SimpleMatch 'MOUNTAIN_SNOW_RENDER_READY' -Quiet))
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw "Mountain village $View view probe did not become ready." }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Mountain village player has no main window.' }
    [WofMountainVillageCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofMountainVillageCapture]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 3200

    $rect = New-Object WofMountainVillageCapture+RECT
    if (-not [WofMountainVillageCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofMountainVillageCapture+POINT
    if (-not [WofMountainVillageCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
    $captureWindow = {
        param([string]$Path)
        $bitmap = New-Object System.Drawing.Bitmap $width, $height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $deviceContext = $graphics.GetHdc()
            try {
                if (-not [WofMountainVillageCapture]::PrintWindow($windowHandle, $deviceContext, 3)) {
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
    & $captureWindow $capturePath
    Start-Sleep -Milliseconds 1200
    & $captureWindow $capturePathAfter

    if ($requiresSnowProbe) {
        $snowMotionPassed = Select-String -LiteralPath $logPath -SimpleMatch 'MOUNTAIN_SNOW_MOTION_PASS' -Quiet
        if (-not $snowMotionPassed) { throw 'Mountain snow did not pass its visible motion probe.' }
    }

    [PSCustomObject]@{ View=$View; HandFire=$HandFire; Capture=$capturePath; CaptureAfter=$capturePathAfter; Log=$logPath; Width=$width; Height=$height; Bytes=(Get-Item -LiteralPath $capturePath).Length; BytesAfter=(Get-Item -LiteralPath $capturePathAfter).Length }
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
