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
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT {
    public int dx, dy;
    public uint mouseData, dwFlags, time;
    public UIntPtr dwExtraInfo;
  }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT {
    public ushort wVk, wScan;
    public uint dwFlags, time;
    public UIntPtr dwExtraInfo;
  }
  [StructLayout(LayoutKind.Sequential)] public struct HARDWAREINPUT {
    public uint uMsg;
    public ushort wParamL, wParamH;
  }
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION {
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
  }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT {
    public uint type;
    public INPUTUNION data;
  }
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
  [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);
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

  public static void HoldKey(byte virtualKey, int milliseconds) {
    SendKeyDown(virtualKey);
    System.Threading.Thread.Sleep(milliseconds);
    SendKeyUp(virtualKey);
  }

  public static void SetKey(byte virtualKey, bool pressed) {
    if (pressed) SendKeyDown(virtualKey); else SendKeyUp(virtualKey);
  }

  public static void SetScanKey(byte virtualKey, bool pressed) {
    const uint KeyboardInput = 1;
    const uint ScanCode = 0x0008;
    const uint KeyUp = 0x0002;
    INPUT[] inputs = new INPUT[] {
      new INPUT {
        type = KeyboardInput,
        data = new INPUTUNION {
          ki = new KEYBDINPUT {
            wVk = 0,
            wScan = (ushort)MapVirtualKey(virtualKey, 0),
            dwFlags = ScanCode | (pressed ? 0u : KeyUp),
            time = 0,
            dwExtraInfo = UIntPtr.Zero
          }
        }
      }
    };
    if (SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT))) != 1) {
      throw new InvalidOperationException("SendInput scan-code packet failed: " + Marshal.GetLastWin32Error());
    }
  }

  public static void MoveMouseRelative(int deltaX, int deltaY) {
    mouse_event(0x0001, unchecked((uint)deltaX), unchecked((uint)deltaY), 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
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
$vclipPath = Join-Path $resolvedOutputRoot 'command-console-vclip-on.png'
$nightPath = Join-Path $resolvedOutputRoot 'command-console-night.png'
$dayPath = Join-Path $resolvedOutputRoot 'command-console-day.png'
$slidePath = Join-Path $resolvedOutputRoot 'player-motor-slide.png'
$crouchPath = Join-Path $resolvedOutputRoot 'player-motor-crouch.png'
$jumpPath = Join-Path $resolvedOutputRoot 'player-motor-jump.png'
$collisionPath = Join-Path $resolvedOutputRoot 'player-motor-collision.png'
$navigationRoot = Join-Path $resolvedBuildRoot 'NavigationRecordings'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @($logPath, $openPath, $filterPath, $vclipPath, $nightPath, $dayPath,
    $slidePath, $crouchPath, $jumpPath, $collisionPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
    }
}
if (Test-Path -LiteralPath $navigationRoot -PathType Container) {
    Remove-Item -LiteralPath $navigationRoot -Recurse -Force
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
    '--wof-solo', '--wof-auto-exit=150',
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

function Release-WofSyntheticKeys {
    # Always unwind the complete physical-input chord set so an aborted probe
    # cannot contaminate the next player launch through global keyboard state.
    [WofCommandConsoleCapture]::SetScanKey(0x43, $false)
    foreach ($virtualKey in @(0x43, 0x57, 0x41, 0x53, 0x44, 0x20, 0x10, 0x11, 0x12)) {
        [WofCommandConsoleCapture]::SetKey([byte]$virtualKey, $false)
    }
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
    [WofCommandConsoleCapture]::SetKey(0x11, $true)
    [WofCommandConsoleCapture]::SendKey(0x41)
    [WofCommandConsoleCapture]::SetKey(0x11, $false)
    [WofCommandConsoleCapture]::SendKey(0x08)
    [WofCommandConsoleCapture]::SendText('/' + $Text)
    Start-Sleep -Milliseconds 250
    $actionCount = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        @(Select-String -LiteralPath $logPath -Pattern "action=$Action").Count
    } else { 0 }
    $closeCount = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        @(Select-String -LiteralPath $logPath -Pattern 'COMMAND_CONSOLE_CLOSED relock=true').Count
    } else { 0 }
    [WofCommandConsoleCapture]::SendKey(0x0D)
    if (-not (Wait-WofNewLogMarker -Pattern "action=$Action" -PreviousCount $actionCount -Seconds 7)) {
        throw "Physical command submission did not produce action $Action."
    }
    if (-not (Wait-WofNewLogMarker -Pattern 'COMMAND_CONSOLE_CLOSED relock=true' -PreviousCount $closeCount -Seconds 7)) {
        throw "Physical command submission for $Action did not finish closing the command console."
    }
    Start-Sleep -Milliseconds 50
}

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $ready = (Test-Path -LiteralPath $logPath -PathType Leaf) -and
                 (Select-String -LiteralPath $logPath -Pattern 'SESSION_READY mode=Solo' -Quiet)
    } while (-not $ready -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Command-console player did not become ready.' }

    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Command-console player has no main window.' }
    $focusShell = New-Object -ComObject WScript.Shell
    $foregroundDeadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $focusShell.AppActivate($process.Id) | Out-Null
        [WofCommandConsoleCapture]::ForceForeground($windowHandle) | Out-Null
        Start-Sleep -Milliseconds 250
    } while ([WofCommandConsoleCapture]::GetForegroundWindow() -ne $windowHandle -and
             [DateTime]::UtcNow -lt $foregroundDeadline)
    if ([WofCommandConsoleCapture]::GetForegroundWindow() -ne $windowHandle) {
        $foregroundHandle = [WofCommandConsoleCapture]::GetForegroundWindow()
        throw "Unity player did not become the foreground window (pid=$($process.Id), expected=$windowHandle, actual=$foregroundHandle)."
    }
    Release-WofSyntheticKeys
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

    Submit-WofCommand -Text 'night' -Action 'ForceNight'
    if (-not (Wait-WofLogMarker -Pattern 'SURVIVAL_SKY_OVERRIDE seconds=342\.0' -Seconds 5)) {
        throw 'Night command did not force the exact React night timestamp.'
    }
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $nightPath

    Submit-WofCommand -Text 'day' -Action 'ForceDay'
    if (-not (Wait-WofLogMarker -Pattern 'SURVIVAL_SKY_OVERRIDE seconds=42\.0' -Seconds 5)) {
        throw 'Day command did not force the exact React day timestamp.'
    }
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $dayPath
    Submit-WofCommand -Text 'day off' -Action 'ResumeDayNightCycle'
    if (-not (Wait-WofLogMarker -Pattern 'SURVIVAL_SKY_OVERRIDE cleared=true' -Seconds 5)) {
        throw 'Day-off command did not resume the synchronized sky cycle.'
    }

    Submit-WofCommand -Text 'vclip on' -Action 'SetVClipEnabled'
    if (-not (Wait-WofLogMarker -Pattern 'VCLIP_CHANGED owner=0 enabled=true' -Seconds 5)) {
        throw 'VCLIP command did not reach server authority.'
    }
    Open-WofCommandConsole
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $vclipPath
    [WofCommandConsoleCapture]::SendKey(0x1B)
    Start-Sleep -Milliseconds 180
    [WofCommandConsoleCapture]::HoldKey(0x20, 650)
    if (-not (Wait-WofLogMarker -Pattern 'VCLIP_MOVEMENT velocity=.*collisions=false' -Seconds 5)) {
        throw 'Physical Space input did not produce collision-free VCLIP ascent.'
    }
    # Relocate beyond the south village wall using the already verified collision-free
    # mode. The normal-motor recording then runs on open terrain and returns to a solid
    # wall segment for its explicit collision gate.
    [WofCommandConsoleCapture]::SetKey(0x10, $true)
    [WofCommandConsoleCapture]::HoldKey(0x57, 12000)
    [WofCommandConsoleCapture]::SetKey(0x10, $false)
    Submit-WofCommand -Text 'vclip off' -Action 'SetVClipEnabled'
    if (-not (Wait-WofLogMarker -Pattern 'VCLIP_CHANGED owner=0 enabled=false' -Seconds 5)) {
        throw 'VCLIP off did not restore authoritative collision movement.'
    }
    Start-Sleep -Milliseconds 2500

    Submit-WofCommand -Text 'navrecord clear' -Action 'ClearNavigationRecordings'
    Submit-WofCommand -Text 'navrecord start desktop motor route' -Action 'StartNavigationRecording'
    if (-not (Wait-WofLogMarker -Pattern 'NAV_RECORDING_STARTED label="desktop motor route"' -Seconds 5)) {
        throw 'Navigation recorder did not start with the typed label.'
    }

    # Walk at the exact React base speed.
    [WofCommandConsoleCapture]::HoldKey(0x57, 1400)
    Start-Sleep -Milliseconds 300

    # Hold keyboard Shift + W for the exact React sprint path.
    [WofCommandConsoleCapture]::SetKey(0x10, $true)
    [WofCommandConsoleCapture]::HoldKey(0x57, 1400)
    [WofCommandConsoleCapture]::SetKey(0x10, $false)
    Start-Sleep -Milliseconds 300

    # Enter the one-second slide from an active sprint and capture its physical pose.
    [WofCommandConsoleCapture]::SetKey(0x10, $true)
    [WofCommandConsoleCapture]::SetKey(0x57, $true)
    Start-Sleep -Milliseconds 350
    [WofCommandConsoleCapture]::SetKey(0x43, $true)
    Start-Sleep -Milliseconds 450
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $slidePath
    Start-Sleep -Milliseconds 500
    [WofCommandConsoleCapture]::SetKey(0x43, $false)
    [WofCommandConsoleCapture]::SetKey(0x57, $false)
    [WofCommandConsoleCapture]::SetKey(0x10, $false)
    Start-Sleep -Milliseconds 350

    # React crouch requires a stationary three-second hold, then moves at 3.52 m/s.
    [WofCommandConsoleCapture]::SetScanKey(0x43, $true)
    # Unity's Windows Input System receives the held-key snapshot on the next
    # distinct keyboard packet. F24 is intentionally unbound in WOF.
    [WofCommandConsoleCapture]::SendKey(0x87)
    Start-Sleep -Milliseconds 4000
    [WofCommandConsoleCapture]::SetKey(0x57, $true)
    Start-Sleep -Milliseconds 700
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $crouchPath
    Start-Sleep -Milliseconds 700
    [WofCommandConsoleCapture]::SetKey(0x57, $false)
    [WofCommandConsoleCapture]::SetScanKey(0x43, $false)
    Start-Sleep -Milliseconds 350

    # Hold Space long enough to verify jump/thruster lift and airborne grounding state.
    [WofCommandConsoleCapture]::SetKey(0x20, $true)
    Start-Sleep -Milliseconds 500
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $jumpPath
    Start-Sleep -Milliseconds 650
    [WofCommandConsoleCapture]::SetKey(0x20, $false)
    Start-Sleep -Milliseconds 900

    # Offset from the south gate, sprint backward into the solid perimeter section,
    # and keep pressing long enough for collision-stopped telemetry samples.
    [WofCommandConsoleCapture]::HoldKey(0x44, 6000)
    [WofCommandConsoleCapture]::SetKey(0x10, $true)
    [WofCommandConsoleCapture]::SetKey(0x53, $true)
    Start-Sleep -Milliseconds 20000
    Save-WofCommandConsoleImage -WindowHandle $windowHandle -Path $collisionPath
    [WofCommandConsoleCapture]::SetKey(0x53, $false)
    [WofCommandConsoleCapture]::SetKey(0x10, $false)
    Start-Sleep -Milliseconds 350

    # Finish with real mouse delta so the exported yaw/aim vector proves camera look.
    [WofCommandConsoleCapture]::MoveClient($windowHandle, 640, 360)
    for ($lookIndex = 0; $lookIndex -lt 18; $lookIndex++) {
        [WofCommandConsoleCapture]::MoveMouseRelative(32, -3)
    }
    Start-Sleep -Milliseconds 350

    Submit-WofCommand -Text 'navrecord stop' -Action 'StopNavigationRecording'
    if (-not (Wait-WofLogMarker -Pattern 'NAV_RECORDING_STOPPED samples=[1-9][0-9]*' -Seconds 5)) {
        throw 'Navigation recorder did not capture physical movement samples.'
    }
    Submit-WofCommand -Text 'navrecord export' -Action 'ExportNavigationRecording'
    if (-not (Wait-WofLogMarker -Pattern 'NAV_RECORDING_EXPORTED path=' -Seconds 5)) {
        throw 'Navigation recorder did not export JSON.'
    }
    $navigationExport = Get-ChildItem -LiteralPath $navigationRoot -Filter 'wizards-nav-*.json' -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $navigationExport) { throw 'Navigation JSON is not beside the D-drive Windows build.' }
    $navigationPayload = Get-Content -LiteralPath $navigationExport.FullName -Raw | ConvertFrom-Json
    $latestNavigationSession = @($navigationPayload.sessions)[-1]
    $motorSamples = @($latestNavigationSession.samples)
    if ($latestNavigationSession.label -ne 'desktop motor route' -or
        $motorSamples.Count -lt 150 -or
        $latestNavigationSession.sampleIntervalMs -ne 125) {
        throw 'Navigation JSON does not preserve the desktop motor label, complete sample sequence, and 125 ms cadence.'
    }

    function Get-PlanarSpeed([object]$sample) {
        $x = [double]$sample.velocity[0]
        $z = [double]$sample.velocity[2]
        return [Math]::Sqrt(($x * $x) + ($z * $z))
    }

    $walkSamples = @($motorSamples | Where-Object {
        [double]$_.input.forward -gt 0.9 -and -not $_.input.sprint -and -not $_.input.jump -and
        -not $_.input.slide -and $_.state.moving -and (Get-PlanarSpeed $_) -ge 6.5 -and
        (Get-PlanarSpeed $_) -le 9.5
    })
    $sprintSamples = @($motorSamples | Where-Object {
        $_.input.sprint -and $_.state.sprinting -and -not $_.input.slide -and
        (Get-PlanarSpeed $_) -ge 10.5
    })
    $slideSamples = @($motorSamples | Where-Object {
        $_.input.slide -and $_.state.sliding -and (Get-PlanarSpeed $_) -ge 10.5
    })
    $crouchSamples = @($motorSamples | Where-Object {
        $_.input.slide -and -not $_.state.sliding -and $_.state.moving -and
        (Get-PlanarSpeed $_) -ge 2.5 -and (Get-PlanarSpeed $_) -le 4.5
    })
    $jumpSamples = @($motorSamples | Where-Object { $_.input.jump -and -not $_.state.grounded })
    $groundedSamples = @($motorSamples | Where-Object { $_.state.grounded })
    $yawValues = @($motorSamples | ForEach-Object { [double]$_.rot[1] })
    $yawRange = ($yawValues | Measure-Object -Maximum).Maximum - ($yawValues | Measure-Object -Minimum).Minimum
    $collisionSamples = @($motorSamples | Where-Object {
        $_.input.sprint -and [double]$_.input.forward -lt -0.9 -and -not $_.input.slide -and
        $_.state.grounded -and (Get-PlanarSpeed $_) -lt 1.0
    })
    if ($walkSamples.Count -lt 4) { throw 'Physical W input did not produce sustained React walk-speed samples.' }
    if ($sprintSamples.Count -lt 4) { throw 'Physical Shift+W input did not produce sustained React sprint samples.' }
    if ($slideSamples.Count -lt 2) { throw 'Physical C during sprint did not enter the React sliding state.' }
    if ($crouchSamples.Count -lt 2) { throw 'Physical three-second C hold did not produce React crouch-speed samples.' }
    if ($jumpSamples.Count -lt 2) { throw 'Physical Space hold did not produce airborne jump samples.' }
    if ($groundedSamples.Count -lt 20) { throw 'Grounded movement was not sustained in the physical motor sequence.' }
    if ($yawRange -lt 5.0) { throw "Physical mouse movement did not rotate the camera enough (yaw range $yawRange)." }
    if ($collisionSamples.Count -lt 3) { throw 'Forward sprint input did not produce repeated collision-stopped samples at the village perimeter.' }

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
        VClipCapture = $vclipPath
        NightCapture = $nightPath
        DayCapture = $dayPath
        SlideCapture = $slidePath
        CrouchCapture = $crouchPath
        JumpCapture = $jumpPath
        CollisionCapture = $collisionPath
        NavigationExport = $navigationExport.FullName
        Log = $logPath
        InventorySuggestion = $true
        Forage = $true
        Brew = $true
        SkyCommands = $true
        VClip = $true
        NavigationRecording = $true
        PlayerMotor = [pscustomobject]@{
            WalkSamples = $walkSamples.Count
            SprintSamples = $sprintSamples.Count
            SlideSamples = $slideSamples.Count
            CrouchSamples = $crouchSamples.Count
            JumpSamples = $jumpSamples.Count
            GroundedSamples = $groundedSamples.Count
            CollisionStoppedSamples = $collisionSamples.Count
            YawRange = [Math]::Round($yawRange, 2)
        }
        EscapeClose = $true
        DraughtQuantity = [int]$draught[0].quantity
    }
}
finally {
    Release-WofSyntheticKeys
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
}
