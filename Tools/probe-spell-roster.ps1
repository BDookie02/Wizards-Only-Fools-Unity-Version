param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity\spell-roster-probe'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Spell roster probe paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
$playerTempRoot = Join-Path $resolvedOutputRoot 'player-temp'
$profileRoot = Join-Path $resolvedOutputRoot ('profile-' + [Guid]::NewGuid().ToString('N'))
foreach ($root in @($resolvedOutputRoot, $powerShellTempRoot, $playerTempRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $root | Out-Null
}
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofSpellRosterInput {
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
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
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
  public static void Tap(byte key) {
    byte scan = (byte)MapVirtualKey(key, 0);
    keybd_event(key, scan, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(75);
    keybd_event(key, scan, 2, UIntPtr.Zero);
  }
  public static void ClickClient(IntPtr handle, int x, int y) {
    POINT point = new POINT { X = x, Y = y };
    if (!ClientToScreen(handle, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(75);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
}
'@
[WofSpellRosterInput]::SetProcessDPIAware() | Out-Null

$spells = @(
    'Fireball', 'IceShard', 'ArcaneBeam', 'Heal', 'IceSpell',
    'RingsOfPower', 'Lightning', 'SmokeBomb', 'Portal', 'Blink',
    'Grab', 'Tornado', 'MeteorShower', 'Flamethrower', 'DiscShield',
    'OrbShield', 'Kunai', 'HealingCrystals', 'MagicArmor', 'JumpBoost',
    'SpeedBoost', 'TungstonBallsack', 'Sleep', 'Poison', 'Acid', 'MagicGlassOrb'
)
$logPath = Join-Path $resolvedOutputRoot 'runtime.log'
if (Test-Path -LiteralPath $logPath -PathType Leaf) { Remove-Item -LiteralPath $logPath -Force }
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    '{"version":1,"playerName":"Spell Roster QA","questUnlockedSpells":[],"spellQuestAssignments":[],"questFlags":[]}')

function Wait-WofLog([string]$Pattern, [int]$Seconds = 8) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $found = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern $Pattern -Quiet)
        $process.Refresh()
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=90', "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$env:TEMP = $playerTempRoot
$env:TMP = $playerTempRoot
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $resolvedBuildRoot -PassThru
try {
    if (-not (Wait-WofLog -Pattern 'PLAYER_SPAWN' -Seconds 35)) { throw 'Spell roster player did not spawn.' }
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $windowDeadline)
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Spell roster player has no window.' }
    if (-not [WofSpellRosterInput]::Focus($handle)) { throw 'Could not focus the spell roster player.' }
    Start-Sleep -Milliseconds 700

    for ($index = 0; $index -lt $spells.Count; $index++) {
        [WofSpellRosterInput]::Tap(0x45)
        Start-Sleep -Milliseconds 140
        $column = $index % 5
        $row = [Math]::Floor($index / 5)
        $buttonX = [Math]::Round(344 + $column * 148)
        $buttonY = [Math]::Round(216 + $row * 44.3)
        [WofSpellRosterInput]::ClickClient($handle, $buttonX, $buttonY)
        Start-Sleep -Milliseconds 140
        [WofSpellRosterInput]::Tap(0x45)
        Start-Sleep -Milliseconds 130
        [WofSpellRosterInput]::ClickClient($handle, 640, 120)
        Start-Sleep -Milliseconds 1150
    }

    Start-Sleep -Milliseconds 500
    $log = Get-Content -LiteralPath $logPath -Raw
    $missingEquips = @($spells | Where-Object {
        $escaped = [regex]::Escape($_)
        $log -notmatch "SPELL_EQUIPPED owner=0 hand=Left spell=$escaped"
    })
    $missingCasts = @($spells | Where-Object {
        $escaped = [regex]::Escape($_)
        $log -notmatch "(?:SPELL_CAST|SELF_SPELL_CAST|HITSCAN_SPELL_CAST|AREA_SPELL_CAST) owner=0 hand=Left spell=$escaped"
    })
    if ($missingEquips.Count -gt 0 -or $missingCasts.Count -gt 0) {
        throw "Spell roster probe failed. missingEquips=$($missingEquips -join ',') missingCasts=$($missingCasts -join ',')"
    }
    "[WOF-AUTOMATION] SPELL_ROSTER_PHYSICAL_PASS equipped=$($spells.Count) cast=$($spells.Count) log=$logPath"
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
