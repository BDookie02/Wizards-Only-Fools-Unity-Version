param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Command-console capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofCommandConsoleCapture {
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
  [DllImport("user32.dll")] public static extern short VkKeyScan(char character);
  [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

  public static bool ForceForeground(IntPtr hWnd) {
    IntPtr foreground = GetForegroundWindow();
    uint currentThread = GetCurrentThreadId();
    uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
    bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
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
    const uint KeyUp = 0x0002;
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    keybd_event(virtualKey, scanCode, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(45);
    keybd_event(virtualKey, scanCode, KeyUp, UIntPtr.Zero);
  }

  public static void SendText(string value) {
    foreach (char character in value) {
      short encoded = VkKeyScan(character);
      if (encoded == -1) throw new InvalidOperationException("Cannot type character: " + character);
      byte virtualKey = (byte)(encoded & 0xff);
      byte modifiers = (byte)((encoded >> 8) & 0xff);
      if ((modifiers & 1) != 0) SendKeyDown(0x10);
      SendKey(virtualKey);
      if ((modifiers & 1) != 0) SendKeyUp(0x10);
    }
  }

  public static void ClickClient(IntPtr hWnd, int clientX, int clientY) {
    POINT point = new POINT { X = clientX, Y = clientY };
    if (!ClientToScreen(hWnd, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(70);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }

  public static void MoveClient(IntPtr hWnd, int clientX, int clientY) {
    POINT point = new POINT { X = clientX, Y = clientY };
    if (!ClientToScreen(hWnd, ref point)) throw new InvalidOperationException("ClientToScreen failed.");
    SetCursorPos(point.X, point.Y);
  }

  private static void SendKeyDown(byte virtualKey) {
    keybd_event(virtualKey, (byte)MapVirtualKey(virtualKey, 0), 0, UIntPtr.Zero);
  }

  private static void SendKeyUp(byte virtualKey) {
    keybd_event(virtualKey, (byte)MapVirtualKey(virtualKey, 0), 0x0002, UIntPtr.Zero);
  }
}
'@
[WofCommandConsoleCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('command-console-profile-' + [Guid]::NewGuid().ToString('N'))
$profilePath = Join-Path $profileRoot 'survival-save-v1.json'
$logPath = Join-Path $logRoot 'command-console-runtime.log'
$openPath = Join-Path $resolvedOutputRoot 'command-console-open.png'
$filterPath = Join-Path $resolvedOutputRoot 'command-console-inventory-filter.png'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @($logPath, $openPath, $filterPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}

$profile = [ordered]@{
    version = 1
    playerName = 'Console QA'
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
        [ordered]@{ npcId='darrel'; townId='village-town'; displayName='Darrel'; questId='spellquest:healingcrystals'; spell='healingcrystals'; status='assigned'; assignedAt=1000; completedAt=0 }
    )
    questFlags = @(
        [ordered]@{ key='darrel:healingcrystals:accepted'; value='true' },
        [ordered]@{ key='quest:spellquest:healingcrystals'; value='started' },
        [ordered]@{ key='darrel:ingredient:leaves'; value='needed' },
        [ordered]@{ key='darrel:ingredient:berries'; value='needed' },
        [ordered]@{ key='darrel:ingredient:roots'; value='needed' },
        [ordered]@{ key='darrel:garden-draught'; value='needed' }
    )
    inventory = @()
}
[System.IO.File]::WriteAllText($profilePath, ($profile | ConvertTo-Json -Depth 8))

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-villager-view-probe', '--wof-auto-exit=150',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofCommandConsoleImage {
    param([IntPtr]$WindowHandle, [string]$Path)
    $rect = New-Object WofCommandConsoleCapture+RECT
    if (-not [WofCommandConsoleCapture]::GetClientRect($WindowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofCommandConsoleCapture+POINT
    if (-not [WofCommandConsoleCapture]::ClientToScreen($WindowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -ne 1280 -or $height -ne 720) { throw "Unexpected client dimensions: ${width}x${height}." }
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
    param([string]$Pattern, [int]$Seconds = 7)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $found = (Test-Path -LiteralPath $logPath -PathType Leaf) -and (Select-String -LiteralPath $logPath -Pattern $Pattern -Quiet)
        $process.Refresh()
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

function Wait-WofNewLogMarker {
    param([string]$Pattern, [int]$PreviousCount, [int]$Seconds = 7)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $count = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            @(Select-String -LiteralPath $logPath -Pattern $Pattern).Count
        } else { 0 }
        $process.Refresh()
    } while ($count -le $PreviousCount -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $count -gt $PreviousCount
}

function Open-WofCommandConsole {
    $openCount = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        @(Select-String -LiteralPath $logPath -Pattern 'COMMAND_CONSOLE_OPEN value=/').Count
    } else { 0 }
    [WofCommandConsoleCapture]::SendKey(0xBF)
    if (-not (Wait-WofNewLogMarker -Pattern 'COMMAND_CONSOLE_OPEN value=/' -PreviousCount $openCount -Seconds 5)) {
        throw 'Physical Slash did not open the command console.'
    }
    Start-Sleep -Milliseconds 250
}

function Submit-WofCommand {
    param([string]$Text, [string]$Action)
    Open-WofCommandConsole
    [WofCommandConsoleCapture]::SendText($Text)
    [WofCommandConsoleCapture]::SendKey(0x0D)
    if (-not (Wait-WofLogMarker -Pattern "action=$Action" -Seconds 7)) {
        throw "Physical command submission did not produce action $Action."
    }
    Start-Sleep -Milliseconds 180
}

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and (Select-String -LiteralPath $logPath -Pattern 'VILLAGER_VIEW_PROBE_READY' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Command-console player did not become ready.' }

    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Command-console player has no main window.' }
    [WofCommandConsoleCapture]::ForceForeground($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 450
    if ([WofCommandConsoleCapture]::GetForegroundWindow() -ne $windowHandle) {
        throw "Unity player did not become the foreground window (pid=$($process.Id))."
    }
    [WofCommandConsoleCapture]::MoveClient($windowHandle, 1240, 680)

    Open-WofCommandConsole
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $openPath
    [WofCommandConsoleCapture]::SendText('inventory')
    Start-Sleep -Milliseconds 250
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $filterPath
    [WofCommandConsoleCapture]::ClickClient($windowHandle, 405, 108)
    if (-not (Wait-WofLogMarker -Pattern 'COMMAND_CONSOLE_SUGGESTION command=inventory' -Seconds 5)) {
        throw 'Physical mouse click did not select the Inventory suggestion.'
    }
    [WofCommandConsoleCapture]::SendKey(0x0D)
    if (-not (Wait-WofLogMarker -Pattern 'action=OpenInventory' -Seconds 5) -or
        -not (Wait-WofLogMarker -Pattern 'INVENTORY_OPEN source=gameplay' -Seconds 5)) {
        throw 'Enter did not transition the selected console command to inventory.'
    }
    Start-Sleep -Milliseconds 250
    [WofCommandConsoleCapture]::SendKey(0x49)
    if (-not (Wait-WofLogMarker -Pattern 'INVENTORY_CLOSED' -Seconds 5)) { throw 'Physical I did not close inventory.' }

    Submit-WofCommand -Text 'forage leaves' -Action 'ForageLeaves'
    Submit-WofCommand -Text 'forage berries' -Action 'ForageBerries'
    Submit-WofCommand -Text 'forage roots' -Action 'ForageRoots'
    Submit-WofCommand -Text 'brew' -Action 'BrewGardenDraught'

    Open-WofCommandConsole
    $closedCount = @(Select-String -LiteralPath $logPath -Pattern 'COMMAND_CONSOLE_CLOSED relock=true').Count
    [WofCommandConsoleCapture]::SendKey(0x1B)
    if (-not (Wait-WofNewLogMarker -Pattern 'COMMAND_CONSOLE_CLOSED relock=true' -PreviousCount $closedCount -Seconds 5)) {
        throw 'Physical Escape did not close the command console.'
    }

    $saved = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $draught = @($saved.inventory | Where-Object { $_.itemId -eq 'garden-draught' })
    $ingredients = @($saved.inventory | Where-Object { $_.itemId -in @('darrel-leaves', 'darrel-berries', 'darrel-roots') })
    if ($draught.Count -ne 1 -or [int]$draught[0].quantity -ne 1) {
        throw 'Brew command did not persist exactly one garden draught.'
    }
    if ($ingredients.Count -ne 0) {
        throw 'Brew command did not consume all three gathered ingredients.'
    }

    [PSCustomObject]@{
        ProcessId = $process.Id
        OpenCapture = $openPath
        FilterCapture = $filterPath
        Log = $logPath
        InventorySuggestion = $true
        Forage = $true
        Brew = $true
        EscapeClose = $true
        DraughtQuantity = [int]$draught[0].quantity
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
