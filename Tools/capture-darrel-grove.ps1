param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateSet('spawn', 'backyard', 'waterfall')]
    [string]$View = 'spawn'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Darrel grove capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofDarrelGroveCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
}
'@
[WofDarrelGroveCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('darrel-grove-capture-profile-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot 'darrel-grove-capture.log'
$capturePath = Join-Path $resolvedOutputRoot ("darrel-grove-$View-desktop.png")
$secondCapturePath = if ($View -eq 'waterfall') {
    Join-Path $resolvedOutputRoot 'darrel-grove-waterfall-desktop-later.png'
} else { $null }
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @($logPath, $capturePath, $secondCapturePath)) {
    if ([string]::IsNullOrWhiteSpace($target)) { continue }
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Grove QA","darrelHealingCrystalsQuestStatus":"assigned","darrelHealingCrystalsAssignedAt":1000,"questUnlockedSpells":["blink"],"spellQuestAssignments":[{"npcId":"-64--48","townId":"base-village","displayName":"Darrel","questId":"spellquest:healingcrystals","spell":"healingcrystals","status":"assigned","assignedAt":1000}],"questFlags":[{"key":"darrel:healingcrystals:accepted","value":"true"},{"key":"darrel:garden-draught","value":"drunk"},{"key":"quest:spellquest:healingcrystals","value":"started"}],"inventory":[]}')

$viewArgument = switch ($View) {
    'backyard' { '--wof-darrel-grove-view-probe=backyard' }
    'waterfall' { '--wof-darrel-grove-view-probe=waterfall' }
    default { '--wof-darrel-grove-view-probe' }
}
$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', $viewArgument, '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern 'DARREL_GROVE_VIEW_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Darrel grove view probe did not become ready.' }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Darrel grove player has no main window.' }
    [WofDarrelGroveCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofDarrelGroveCapture]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 1800

    $rect = New-Object WofDarrelGroveCapture+RECT
    if (-not [WofDarrelGroveCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofDarrelGroveCapture+POINT
    if (-not [WofDarrelGroveCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
    function Save-WofDarrelGroveFrame([string]$Path) {
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
    Save-WofDarrelGroveFrame -Path $capturePath
    if ($View -eq 'waterfall') {
        Start-Sleep -Milliseconds 850
        Save-WofDarrelGroveFrame -Path $secondCapturePath
    }

    [PSCustomObject]@{ Capture=$capturePath; LaterCapture=$secondCapturePath; Log=$logPath; Width=$width; Height=$height }
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
