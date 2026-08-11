param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Quest-navigation capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofQuestNavigationCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

  public static void ForceForeground(IntPtr hWnd) {
    ShowWindowAsync(hWnd, 9);
    BringWindowToTop(hWnd);
    SetForegroundWindow(hWnd);
  }
}
'@
[WofQuestNavigationCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Windows player is missing: $executable"
}

$runId = [Guid]::NewGuid().ToString('N')
$profileRoot = Join-Path $resolvedOutputRoot "quest-navigation-profile-$runId"
$questRoot = Join-Path $resolvedOutputRoot "quest-navigation-programs-$runId"
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$logPath = Join-Path $logRoot 'quest-navigation-runtime.log'
$capturePath = Join-Path $resolvedOutputRoot 'quest-navigation-multiple-beacons.png'
foreach ($root in @($profileRoot, $questRoot, $logRoot)) {
    New-Item -ItemType Directory -Force -Path $root | Out-Null
}

$profile = @{
    version = 2
    playerName = 'Beacon QA'
    questUnlockedSpells = @('blink')
    spellQuestAssignments = @(
        @{
            npcId = 'qa:clockmaker'
            townId = 'qa-town'
            displayName = 'Clockmaker'
            questId = 'spellquest:arcanebeam'
            spell = 'arcanebeam'
            status = 'assigned'
            assignedAt = 100
            completedAt = 0
        },
        @{
            npcId = 'qa:healer'
            townId = 'qa-town'
            displayName = 'Nora'
            questId = 'spellquest:healspell'
            spell = 'healspell'
            status = 'assigned'
            assignedAt = 200
            completedAt = 0
        }
    )
    questFlags = @(
        @{ key = 'spellquest:healspell:ready'; value = 'true' }
    )
    inventory = @()
}
$programs = @{
    version = 1
    claimedDarrelNpcId = ''
    programs = @(
        @{
            npcId = 'qa:clockmaker'
            townId = 'qa-town'
            hutId = 'qa-clocktower'
            displayName = 'Clockmaker'
            role = 1
            theme = 'village'
            hasPosition = $true
            position = @{ x = -14.0; y = 1.0; z = 3.0 }
            greeting = ''
            scriptPoints = @()
            updatedAt = 100
        },
        @{
            npcId = 'qa:healer'
            townId = 'qa-town'
            hutId = 'qa-clinic'
            displayName = 'Nora'
            role = 1
            theme = 'village'
            hasPosition = $true
            position = @{ x = 14.0; y = 1.0; z = 3.0 }
            greeting = ''
            scriptPoints = @()
            updatedAt = 200
        }
    )
}
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    ($profile | ConvertTo-Json -Depth 10 -Compress),
    $utf8NoBom)
[System.IO.File]::WriteAllText(
    (Join-Path $questRoot 'quest-npc-programs-v1.json'),
    ($programs | ConvertTo-Json -Depth 10 -Compress),
    $utf8NoBom)

foreach ($target in @($logPath, $capturePath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}

$arguments = @(
    '-force-d3d11',
    '-screen-width', '1280',
    '-screen-height', '720',
    '-screen-fullscreen', '0',
    '--wof-solo',
    '--wof-auto-exit=90',
    "--wof-profile-root=$profileRoot",
    '--wof-quest-dev-root', $questRoot,
    '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofQuestNavigationImage {
    param([IntPtr]$WindowHandle, [string]$Path)

    $rect = New-Object WofQuestNavigationCapture+RECT
    if (-not [WofQuestNavigationCapture]::GetClientRect($WindowHandle, [ref]$rect)) {
        throw 'GetClientRect failed.'
    }
    $point = New-Object WofQuestNavigationCapture+POINT
    if (-not [WofQuestNavigationCapture]::ClientToScreen($WindowHandle, [ref]$point)) {
        throw 'ClientToScreen failed.'
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) {
        throw "Quest-navigation capture expected 1280x720 but found ${width}x${height}."
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
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $markers = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            @(Select-String -LiteralPath $logPath -Pattern 'QUEST_NAVIGATION_TARGET id=qa:')
        }
        else {
            @()
        }
    } while ($markers.Count -lt 2 -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if ($markers.Count -lt 2) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 100) -join [Environment]::NewLine
        }
        else {
            '<runtime log was not created>'
        }
        throw "Two quest-navigation targets were not activated.`n$tail"
    }

    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Quest-navigation player has no main window.'
    }

    [WofQuestNavigationCapture]::ForceForeground($process.MainWindowHandle)
    Start-Sleep -Seconds 2
    Save-WofQuestNavigationImage -WindowHandle $process.MainWindowHandle -Path $capturePath

    [pscustomobject]@{
        ProcessId = $process.Id
        TargetCount = $markers.Count
        Markers = @($markers | ForEach-Object Line)
        Capture = $capturePath
        CaptureBytes = (Get-Item -LiteralPath $capturePath).Length
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
    foreach ($root in @($profileRoot, $questRoot)) {
        if ($root.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $root -PathType Container)) {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }
}
