param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Inventory capture paths must stay on D:.'
}

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofInventoryCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int command);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

  public static bool ForceForeground(IntPtr hWnd) {
    IntPtr foreground = GetForegroundWindow();
    uint currentThread = GetCurrentThreadId();
    uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
    bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                    AttachThreadInput(currentThread, foregroundThread, true);
    try {
      ShowWindowAsync(hWnd, 9);
      BringWindowToTop(hWnd);
      SetForegroundWindow(hWnd);
      return GetForegroundWindow() == hWnd;
    }
    finally {
      if (attached) AttachThreadInput(currentThread, foregroundThread, false);
    }
  }

  public static void SendKey(byte virtualKey) {
    const uint ExtendedKey = 0x0001;
    const uint KeyUp = 0x0002;
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    uint downFlags = virtualKey >= 0x25 && virtualKey <= 0x28 ? ExtendedKey : 0;
    keybd_event(virtualKey, scanCode, downFlags, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(virtualKey, scanCode, downFlags | KeyUp, UIntPtr.Zero);
  }
}
'@
[WofInventoryCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('inventory-profile-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $logRoot 'inventory-runtime.log'
$inventoryPath = Join-Path $resolvedOutputRoot 'inventory-open.png'
$journalPath = Join-Path $resolvedOutputRoot 'inventory-journal-first.png'
$journalNextPath = Join-Path $resolvedOutputRoot 'inventory-journal-next.png'
$journalBackPath = Join-Path $resolvedOutputRoot 'inventory-journal-back.png'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @($logPath, $inventoryPath, $journalPath, $journalNextPath, $journalBackPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}

$profile = [ordered]@{
    version = 1
    playerName = 'Inventory QA'
    skinColor = '#d6cf91'
    topColor = '#7c3aed'
    hairColor = '#3f2a1d'
    hatStyle = 'floppy-wizard'
    hairStyle = 'none'
    survivalLevel = 1
    survivalXp = 0
    lastMode = 'solo-survival'
    darrelHealingCrystalsQuestStatus = 'assigned'
    darrelHealingCrystalsAssignedAt = 1000
    darrelHealingCrystalsCompletedAt = 0
    questUnlockedSpells = @('blink')
    spellQuestAssignments = @(
        [ordered]@{ npcId='villager-a'; townId='village-town'; displayName='Mira'; questId='spellquest:fireball'; spell='fireball'; status='assigned'; assignedAt=1000; completedAt=0 },
        [ordered]@{ npcId='villager-b'; townId='village-town'; displayName='Bram'; questId='spellquest:healspell'; spell='healspell'; status='assigned'; assignedAt=1100; completedAt=0 },
        [ordered]@{ npcId='darrel'; townId='village-town'; displayName='Darrel'; questId='spellquest:healingcrystals'; spell='healingcrystals'; status='assigned'; assignedAt=1200; completedAt=0 }
    )
    questFlags = @(
        [ordered]@{ key='quest:spellquest:fireball'; value='started' },
        [ordered]@{ key='quest:spellquest:healspell'; value='started' },
        [ordered]@{ key='quest:spellquest:healingcrystals'; value='started' },
        [ordered]@{ key='darrel:ingredient:leaves'; value='gathered' },
        [ordered]@{ key='darrel:ingredient:berries'; value='needed' },
        [ordered]@{ key='darrel:ingredient:roots'; value='gathered' },
        [ordered]@{ key='darrel:garden-draught'; value='needed' }
    )
    inventory = @(
        [ordered]@{ itemId='darrel-leaves'; quantity=1; acquiredAt=1000 },
        [ordered]@{ itemId='darrel-berries'; quantity=2; acquiredAt=1001 },
        [ordered]@{ itemId='darrel-roots'; quantity=1; acquiredAt=1002 },
        [ordered]@{ itemId='garden-draught'; quantity=1; acquiredAt=1003 },
        [ordered]@{ itemId='healing-crystals'; quantity=3; acquiredAt=1004 }
    )
}
[System.IO.File]::WriteAllText(
    (Join-Path $profileRoot 'survival-save-v1.json'),
    ($profile | ConvertTo-Json -Depth 8))

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-villager-view-probe', '--wof-auto-exit=120',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofInventoryImage {
    param([IntPtr]$WindowHandle, [string]$Path)
    $rect = New-Object WofInventoryCapture+RECT
    if (-not [WofInventoryCapture]::GetClientRect($WindowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofInventoryCapture+POINT
    if (-not [WofInventoryCapture]::ClientToScreen($WindowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "Invalid client dimensions: ${width}x${height}." }
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

function Wait-WofLogMarker {
    param([string]$Pattern, [int]$Seconds = 5)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $found = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern $Pattern -Quiet)
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
            (Select-String -LiteralPath $logPath -Pattern 'VILLAGER_VIEW_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Inventory capture player did not become ready.' }

    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Inventory capture player has no main window.' }
    [WofInventoryCapture]::ForceForeground($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    if ([WofInventoryCapture]::GetForegroundWindow() -ne $windowHandle) {
        throw "Unity player did not become the foreground window (pid=$($process.Id))."
    }

    [WofInventoryCapture]::SendKey(0x49)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_OPEN source=gameplay')) { throw 'Physical I did not open inventory.' }
    Start-Sleep -Milliseconds 400
    Save-WofInventoryImage -WindowHandle $windowHandle -Path $inventoryPath

    [WofInventoryCapture]::SendKey(0x4A)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_JOURNAL open=true active=3')) { throw 'Physical J did not open the quest journal.' }
    Start-Sleep -Milliseconds 400
    Save-WofInventoryImage -WindowHandle $windowHandle -Path $journalPath

    [WofInventoryCapture]::SendKey(0x28)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_QUEST_SELECTED index=1')) { throw 'Physical Down did not move quest selection.' }
    Start-Sleep -Milliseconds 400
    Save-WofInventoryImage -WindowHandle $windowHandle -Path $journalNextPath

    [WofInventoryCapture]::SendKey(0x49)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_JOURNAL open=false active=3')) { throw 'Physical I did not back out of the quest journal.' }
    Start-Sleep -Milliseconds 400
    Save-WofInventoryImage -WindowHandle $windowHandle -Path $journalBackPath

    [WofInventoryCapture]::SendKey(0x49)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_CLOSED')) { throw 'Second physical I did not close inventory.' }

    [PSCustomObject]@{
        ProcessId = $process.Id
        Inventory = $inventoryPath
        Journal = $journalPath
        JournalNext = $journalNextPath
        JournalBack = $journalBackPath
        InventoryBytes = (Get-Item -LiteralPath $inventoryPath).Length
        JournalBytes = (Get-Item -LiteralPath $journalPath).Length
        JournalNextBytes = (Get-Item -LiteralPath $journalNextPath).Length
        JournalBackBytes = (Get-Item -LiteralPath $journalBackPath).Length
        Log = $logPath
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
}
