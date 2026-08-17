param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateRange(640, 3840)]
    [int]$Width = 1280,
    [ValidateRange(360, 2160)]
    [int]$Height = 720,
    [ValidateSet('pyramid', 'obelisk')]
    [string]$Variant = 'pyramid'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Desert-landmark probe paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('desert-landmark-probe-profile-' + [Guid]::NewGuid().ToString('N'))
foreach ($requiredRoot in @($powerShellTempRoot, $playerTempRoot, $logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofDesertLandmarkCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
}
'@
[WofDesertLandmarkCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $logRoot "desert-landmark-${Variant}-probe.log"
$capturePath = Join-Path $resolvedOutputRoot "desert-landmark-${Variant}-${Width}x${Height}.png"
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Windows player not found: $executable"
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Desert Landmark QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $capturePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', "--wof-desert-landmark-probe=$Variant", '--wof-sky-probe=day', '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'DESERT_LANDMARK_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw "Desert-landmark $Variant probe did not become ready." }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Desert-landmark player has no main window.' }
    [WofDesertLandmarkCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofDesertLandmarkCapture]::SetWindowPos($windowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x53) | Out-Null
    [WofDesertLandmarkCapture]::SetForegroundWindow($windowHandle) | Out-Null

    $rect = New-Object WofDesertLandmarkCapture+RECT
    if (-not [WofDesertLandmarkCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofDesertLandmarkCapture+POINT
    if (-not [WofDesertLandmarkCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $capturedWidth = $rect.Right - $rect.Left
    $capturedHeight = $rect.Bottom - $rect.Top
    if ($capturedWidth -ne $Width -or $capturedHeight -ne $Height) {
        throw "Unexpected client dimensions: ${capturedWidth}x${capturedHeight}."
    }

    Start-Sleep -Milliseconds 900
    $bitmap = New-Object System.Drawing.Bitmap $capturedWidth, $capturedHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($point.X, $point.Y, 0, 0, $bitmap.Size)
        $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
    [WofDesertLandmarkCapture]::SetWindowPos($windowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x53) | Out-Null

    $runtimeFailure = Select-String -LiteralPath $logPath `
        -Pattern 'NullReferenceException|InvalidOperationException|ArgumentException|MissingReferenceException' `
        -Quiet
    if ($runtimeFailure) { throw "Desert-landmark player logged a runtime exception: $logPath" }

    [PSCustomObject]@{
        Capture = $capturePath
        Log = $logPath
        Ready = (Select-String -LiteralPath $logPath -SimpleMatch 'DESERT_LANDMARK_PROBE_READY' |
            Select-Object -Last 1).Line
        Width = $capturedWidth
        Height = $capturedHeight
        Sha256 = (Get-FileHash -LiteralPath $capturePath -Algorithm SHA256).Hash
    }
}
finally {
    if (-not $process.HasExited) {
        [WofDesertLandmarkCapture]::SetWindowPos($process.MainWindowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x53) | Out-Null
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
