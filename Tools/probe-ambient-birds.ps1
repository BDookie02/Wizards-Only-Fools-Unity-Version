param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity',
    [ValidateSet('plains', 'jungle', 'desert', 'swamp', 'mushroom', 'tallgrass')]
    [string]$Biome = 'jungle',
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
    throw 'Ambient-bird probe paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('ambient-bird-probe-profile-' + [Guid]::NewGuid().ToString('N'))
foreach ($requiredRoot in @($powerShellTempRoot, $playerTempRoot, $logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofAmbientBirdCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
}
'@
[WofAmbientBirdCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logPath = Join-Path $logRoot "ambient-bird-${Biome}-probe.log"
$firstPath = Join-Path $resolvedOutputRoot "ambient-bird-${Biome}-first-${Width}x${Height}.png"
$secondPath = Join-Path $resolvedOutputRoot "ambient-bird-${Biome}-second-${Width}x${Height}.png"
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Ambient Bird QA","questUnlockedSpells":["blink"],"spellQuestAssignments":[],"questFlags":[]}')
foreach ($target in @($logPath, $firstPath, $secondPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}

$arguments = @(
    '-force-d3d11', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '--wof-solo', "--wof-ambient-bird-probe=$Biome", '--wof-sky-probe=day', '--wof-auto-exit=90',
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
            (Select-String -LiteralPath $logPath -SimpleMatch 'AMBIENT_BIRD_PROBE_POSITIONED' -Quiet) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'SURVIVAL_AMBIENT_BIRDS_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Ambient-bird probe did not become ready.' }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        Start-Sleep -Milliseconds 100
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Ambient-bird player has no main window.' }
    [WofAmbientBirdCapture]::ShowWindowAsync($windowHandle, 9) | Out-Null
    [WofAmbientBirdCapture]::SetForegroundWindow($windowHandle) | Out-Null

    $rect = New-Object WofAmbientBirdCapture+RECT
    if (-not [WofAmbientBirdCapture]::GetClientRect($windowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofAmbientBirdCapture+POINT
    if (-not [WofAmbientBirdCapture]::ClientToScreen($windowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
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

    Start-Sleep -Milliseconds 800
    Save-ClientCapture $firstPath
    $passDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        $passed = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -SimpleMatch 'AMBIENT_BIRD_PROBE_PASS' -Quiet)
    } while (-not $passed -and -not $process.HasExited -and [DateTime]::UtcNow -lt $passDeadline)
    if (-not $passed) { throw 'Ambient-bird orbit did not pass in the executable.' }
    Start-Sleep -Milliseconds 650
    Save-ClientCapture $secondPath

    $runtimeFailure = Select-String -LiteralPath $logPath `
        -Pattern 'NullReferenceException|InvalidOperationException|ArgumentException|MissingReferenceException' `
        -Quiet
    if ($runtimeFailure) { throw "Ambient-bird player logged a runtime exception: $logPath" }
    $firstHash = (Get-FileHash -LiteralPath $firstPath -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $secondPath -Algorithm SHA256).Hash
    if ($firstHash -eq $secondHash) { throw 'Ambient-bird captures are identical; motion was not visually represented.' }

    [PSCustomObject]@{
        First = $firstPath
        Second = $secondPath
        Log = $logPath
        Ready = (Select-String -LiteralPath $logPath -SimpleMatch 'AMBIENT_BIRD_PROBE_POSITIONED' |
            Select-Object -Last 1).Line
        Passed = (Select-String -LiteralPath $logPath -SimpleMatch 'AMBIENT_BIRD_PROBE_PASS' |
            Select-Object -Last 1).Line
        Width = $capturedWidth
        Height = $capturedHeight
        FirstSha256 = $firstHash
        SecondSha256 = $secondHash
    }
}
finally {
    if (-not $process.HasExited) {
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
