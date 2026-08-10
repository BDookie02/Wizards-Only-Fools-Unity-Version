param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateRange(640, 3840)]
    [int]$Width = 1280,
    [ValidateRange(360, 2160)]
    [int]$Height = 720
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mana-flower probe paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('mana-flower-probe-profile-' + [Guid]::NewGuid().ToString('N'))
foreach ($requiredRoot in @($powerShellTempRoot, $playerTempRoot, $logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofManaFlowerCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
}
'@
[WofManaFlowerCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $logRoot 'mana-flower-probe.log'
$beforePath = Join-Path $resolvedOutputRoot "mana-flower-before-${Width}x${Height}.png"
$afterPath = Join-Path $resolvedOutputRoot "mana-flower-after-collection-${Width}x${Height}.png"
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Mana Flower QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $beforePath, $afterPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', '--wof-grass-view-probe', '--wof-mana-flower-probe', '--wof-auto-exit=90',
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
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'MANA_FLOWER_PROBE_READY' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'BOTW_GRASS_BUILD_COMPLETE' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Mana-flower probe did not become ready.' }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Mana-flower player has no main window.' }
    [WofManaFlowerCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofManaFlowerCapture]::SetForegroundWindow($windowHandle) | Out-Null

    $rect = New-Object WofManaFlowerCapture+RECT
    if (-not [WofManaFlowerCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofManaFlowerCapture+POINT
    if (-not [WofManaFlowerCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $capturedWidth = $rect.Right - $rect.Left
    $capturedHeight = $rect.Bottom - $rect.Top
    if ($capturedWidth -ne $Width -or $capturedHeight -ne $Height) {
        throw "Unexpected client dimensions: ${capturedWidth}x${capturedHeight}."
    }

    function Save-ClientCapture([string]$Path) {
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

    Start-Sleep -Milliseconds 900
    Save-ClientCapture $beforePath
    $collectionDeadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        $collected = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'MANA_FLOWER_COLLECTED' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'MANA_FLOWER_PROBE_PASS' -Quiet)
    } while (-not $collected -and -not $process.HasExited -and [DateTime]::UtcNow -lt $collectionDeadline)
    if (-not $collected) { throw 'Mana flower was not collected and recharged in the executable.' }
    Start-Sleep -Milliseconds 350
    Save-ClientCapture $afterPath

    $readyLine = Select-String -LiteralPath $logPath -SimpleMatch 'MANA_FLOWER_PROBE_READY' | Select-Object -Last 1
    $collectedLine = Select-String -LiteralPath $logPath -SimpleMatch 'MANA_FLOWER_PROBE_PASS' | Select-Object -Last 1
    [PSCustomObject]@{
        Before = $beforePath
        After = $afterPath
        Log = $logPath
        Ready = $readyLine.Line
        Collected = $collectedLine.Line
        Width = $capturedWidth
        Height = $capturedHeight
        BeforeBytes = (Get-Item -LiteralPath $beforePath).Length
        AfterBytes = (Get-Item -LiteralPath $afterPath).Length
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
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
