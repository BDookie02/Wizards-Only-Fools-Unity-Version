param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateSet('day', 'night')]
    [string]$View = 'day'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'World-quality capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofWorldQualityCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@
[WofWorldQualityCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ("world-quality-$View-profile-" + [Guid]::NewGuid().ToString('N'))
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logPath = Join-Path $logRoot "world-quality-$View.log"
$capturePath = Join-Path $resolvedOutputRoot "world-quality-$View-desktop.png"
foreach ($requiredRoot in @($logRoot, $profileRoot, $playerTempRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"World Quality QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $capturePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-grass-view-probe', "--wof-sky-probe=$View", '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(70)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'GRASS_VIEW_PROBE_READY' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'SURVIVAL_SKY_READY' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'BOTW_GRASS_BUILD_COMPLETE' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw "World-quality $View probe did not become ready." }

    $process.Refresh()
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'World-quality player has no main window.' }
    [WofWorldQualityCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofWorldQualityCapture]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 2200

    $rect = New-Object WofWorldQualityCapture+RECT
    if (-not [WofWorldQualityCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            if (-not [WofWorldQualityCapture]::PrintWindow($windowHandle, $deviceContext, 3)) {
                throw 'PrintWindow failed to capture the Unity client.'
            }
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }
        $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    $grassLine = Select-String -LiteralPath $logPath -SimpleMatch 'BOTW_GRASS_BUILD_COMPLETE' | Select-Object -Last 1
    [PSCustomObject]@{ View=$View; Capture=$capturePath; Log=$logPath; Grass=$grassLine.Line; Width=$width; Height=$height; Bytes=(Get-Item -LiteralPath $capturePath).Length }
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
