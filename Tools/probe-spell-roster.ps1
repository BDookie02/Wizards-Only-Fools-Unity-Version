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
Add-Type -AssemblyName System.Drawing

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
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
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
    PressClient(handle, x, y);
    System.Threading.Thread.Sleep(750);
    ReleaseClient();
  }
  public static void PressClient(IntPtr handle, int x, int y) {
    POINT point = new POINT { X = x, Y = y };
    if (!ClientToScreen(handle, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
  }
  public static void ReleaseClient() {
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
  public static void SetClientSize(IntPtr handle, int width, int height) {
    RECT client;
    RECT window;
    if (!GetClientRect(handle, out client) || !GetWindowRect(handle, out window)) {
      throw new InvalidOperationException("Could not read player window geometry.");
    }
    int outerWidth = width + (window.Right - window.Left) - (client.Right - client.Left);
    int outerHeight = height + (window.Bottom - window.Top) - (client.Bottom - client.Top);
    if (!SetWindowPos(handle, IntPtr.Zero, 0, 0, outerWidth, outerHeight, 0x0002 | 0x0004)) {
      throw new InvalidOperationException("Could not enforce player client size.");
    }
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

function Save-WofWindowCapture([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object WofSpellRosterInput+RECT
    if (-not [WofSpellRosterInput]::GetWindowRect($Handle, [ref]$rect)) {
        throw 'Could not capture spell roster window bounds.'
    }
    $bitmap = New-Object Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-auto-exit=180', "--wof-profile-root=$profileRoot", '-logFile', $logPath
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
    [WofSpellRosterInput]::SetClientSize($handle, 1280, 720)
    Start-Sleep -Milliseconds 700

    for ($index = 0; $index -lt $spells.Count; $index++) {
        if (-not [WofSpellRosterInput]::Focus($handle)) { throw "Lost spell roster focus at index $index." }
        [WofSpellRosterInput]::Tap(0x45)
        Start-Sleep -Milliseconds 450
        if ($index -eq 0) {
            Save-WofWindowCapture -Handle $handle -Path (Join-Path $resolvedOutputRoot 'spell-menu-layout.png')
        }
        $column = $index % 5
        $row = [Math]::Floor($index / 5)
        $buttonX = [Math]::Round(344 + $column * 148)
        $buttonY = [Math]::Round(216 + $row * 44.3)
        $spell = $spells[$index]
        $equipPattern = "SPELL_EQUIPPED owner=0 hand=Left spell=$([regex]::Escape($spell))"
        $equipped = $false
        for ($attempt = 1; $attempt -le 3 -and -not $equipped; $attempt++) {
            [WofSpellRosterInput]::ClickClient($handle, $buttonX, $buttonY)
            $equipped = Wait-WofLog -Pattern $equipPattern -Seconds 1
        }
        if (-not $equipped) { throw "Could not equip $spell at index $index after 3 physical clicks." }

        [WofSpellRosterInput]::Tap(0x45)
        Start-Sleep -Milliseconds 350
        $safeSpell = $spell.ToLowerInvariant()
        Save-WofWindowCapture -Handle $handle -Path (
            Join-Path $resolvedOutputRoot ('held-{0:D2}-{1}.png' -f $index, $safeSpell))

        $castPattern = "\[(?:WOF-AUTOMATION|WOF)\] (?:SPELL_CAST|SELF_SPELL_CAST|HITSCAN_SPELL_CAST|AREA_SPELL_CAST|CHANNEL_SPELL_TICK) owner=0 hand=Left spell=$([regex]::Escape($spell))(?:\s|$)"
        $cast = $false
        for ($attempt = 1; $attempt -le 3 -and -not $cast; $attempt++) {
            [WofSpellRosterInput]::PressClient($handle, 640, 120)
            Start-Sleep -Milliseconds 140
            if ($attempt -eq 1) {
                Save-WofWindowCapture -Handle $handle -Path (
                    Join-Path $resolvedOutputRoot ('firing-{0:D2}-{1}.png' -f $index, $safeSpell))
            }
            [WofSpellRosterInput]::ReleaseClient()
            $cast = Wait-WofLog -Pattern $castPattern -Seconds 1
            if (-not $cast) { Start-Sleep -Milliseconds 250 }
        }
        if (-not $cast) { throw "Could not cast $spell after 3 physical clicks." }
        Start-Sleep -Milliseconds 1100
    }

    Start-Sleep -Milliseconds 500
    $log = Get-Content -LiteralPath $logPath -Raw
    $missingEquips = @($spells | Where-Object {
        $escaped = [regex]::Escape($_)
        $log -notmatch "SPELL_EQUIPPED owner=0 hand=Left spell=$escaped"
    })
    $missingCasts = @($spells | Where-Object {
        $escaped = [regex]::Escape($_)
        $log -notmatch "\[(?:WOF-AUTOMATION|WOF)\] (?:SPELL_CAST|SELF_SPELL_CAST|HITSCAN_SPELL_CAST|AREA_SPELL_CAST|CHANNEL_SPELL_TICK) owner=0 hand=Left spell=$escaped(?:\s|$)"
    })
    if ($missingEquips.Count -gt 0 -or $missingCasts.Count -gt 0) {
        throw "Spell roster probe failed. missingEquips=$($missingEquips -join ',') missingCasts=$($missingCasts -join ',')"
    }
    "[WOF-AUTOMATION] SPELL_ROSTER_PHYSICAL_PASS equipped=$($spells.Count) cast=$($spells.Count) log=$logPath"
}
finally {
    [WofSpellRosterInput]::ReleaseClient()
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
