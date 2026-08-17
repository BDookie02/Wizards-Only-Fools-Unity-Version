param(
    [string]$BuildRoot = 'D:\CodexProjects\Wizards-Only-Fools-Unity\Builds\Windows',
    [string]$OutputRoot = 'D:\tmp\wof-unity'
)

$ErrorActionPreference = 'Stop'
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $resolvedBuildRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith('D:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Engine-menu capture paths must stay on D:.'
}

$powerShellTempRoot = Join-Path $resolvedOutputRoot 'powershell-temp'
New-Item -ItemType Directory -Force -Path $powerShellTempRoot | Out-Null
$env:TEMP = $powerShellTempRoot
$env:TMP = $powerShellTempRoot

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WofEngineMenuCapture {
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
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    keybd_event(virtualKey, scanCode, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(45);
    keybd_event(virtualKey, scanCode, 0x0002, UIntPtr.Zero);
    System.Threading.Thread.Sleep(45);
  }

  public static void SetKey(byte virtualKey, bool pressed) {
    byte scanCode = (byte)MapVirtualKey(virtualKey, 0);
    keybd_event(virtualKey, scanCode, pressed ? 0u : 0x0002u, UIntPtr.Zero);
  }

  public static void SendText(string value) {
    foreach (char character in value) {
      short encoded = VkKeyScan(character);
      if (encoded == -1) throw new InvalidOperationException("Cannot type character: " + character);
      byte virtualKey = (byte)(encoded & 0xff);
      byte modifiers = (byte)((encoded >> 8) & 0xff);
      if ((modifiers & 1) != 0) keybd_event(0x10, (byte)MapVirtualKey(0x10, 0), 0, UIntPtr.Zero);
      SendKey(virtualKey);
      if ((modifiers & 1) != 0) keybd_event(0x10, (byte)MapVirtualKey(0x10, 0), 0x0002, UIntPtr.Zero);
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

  public static void DragClient(IntPtr hWnd, int startClientX, int startClientY, int endClientX, int endClientY) {
    POINT start = new POINT { X = startClientX, Y = startClientY };
    POINT end = new POINT { X = endClientX, Y = endClientY };
    if (!ClientToScreen(hWnd, ref start) || !ClientToScreen(hWnd, ref end)) {
      throw new InvalidOperationException("ClientToScreen failed.");
    }
    SetCursorPos(start.X, start.Y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(70);
    const int steps = 18;
    for (int step = 1; step <= steps; step++) {
      int x = start.X + ((end.X - start.X) * step / steps);
      int y = start.Y + ((end.Y - start.Y) * step / steps);
      SetCursorPos(x, y);
      mouse_event(0x0001, 0, 0, 0, UIntPtr.Zero);
      System.Threading.Thread.Sleep(20);
    }
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(140);
  }
}
'@
[WofEngineMenuCapture]::SetProcessDPIAware() | Out-Null

$executable = Join-Path $resolvedBuildRoot 'WizardsOnlyFools.exe'
$logRoot = Join-Path $resolvedOutputRoot 'logs'
$profileRoot = Join-Path $resolvedOutputRoot ('engine-menu-profile-' + [Guid]::NewGuid().ToString('N'))
$profilePath = Join-Path $profileRoot 'survival-save-v1.json'
$logPath = Join-Path $logRoot 'engine-menu-runtime.log'
$openPath = Join-Path $resolvedOutputRoot 'engine-menu-open.png'
$systemsScrolledPath = Join-Path $resolvedOutputRoot 'engine-menu-systems-scrolled.png'
$selectedPath = Join-Path $resolvedOutputRoot 'engine-menu-campfire-selected.png'
$placedPath = Join-Path $resolvedOutputRoot 'engine-menu-campfire-placed.png'
$placementScrolledPath = Join-Path $resolvedOutputRoot 'engine-menu-placement-scrolled.png'
$placementRestoredTopPath = Join-Path $resolvedOutputRoot 'engine-menu-placement-restored-top.png'
$dummySelectedPath = Join-Path $resolvedOutputRoot 'engine-menu-training-dummy-selected.png'
$dummyPath = Join-Path $resolvedOutputRoot 'engine-menu-training-dummy.png'
$dummyReadyPath = Join-Path $resolvedOutputRoot 'training-dummy-combat-ready.png'
$dummyDownPath = Join-Path $resolvedOutputRoot 'training-dummy-combat-down.png'
$dummyRespawnPath = Join-Path $resolvedOutputRoot 'training-dummy-combat-respawn.png'
$worldPath = Join-Path $resolvedOutputRoot 'engine-menu-world-placeables.png'
$storageRoot = Join-Path $resolvedBuildRoot 'EnginePlaceables'
foreach ($requiredRoot in @($logRoot, $profileRoot)) {
    New-Item -ItemType Directory -Force -Path $requiredRoot | Out-Null
}
foreach ($target in @(
    $logPath, $openPath, $systemsScrolledPath, $selectedPath, $placedPath, $placementScrolledPath, $placementRestoredTopPath, $dummySelectedPath, $dummyPath,
    $dummyReadyPath, $dummyDownPath, $dummyRespawnPath, $worldPath)) {
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}
if (Test-Path -LiteralPath $storageRoot -PathType Container) {
    $resolvedStorage = [System.IO.Path]::GetFullPath($storageRoot)
    if (-not $resolvedStorage.StartsWith($resolvedBuildRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to clear engine storage outside the build root.' }
    Remove-Item -LiteralPath $resolvedStorage -Recurse -Force
}

$profile = [ordered]@{
    version = 1
    playerName = 'Engine QA'
    skinColor = '#d6cf91'
    topColor = '#7c3aed'
    hairColor = '#3f2a1d'
    hatStyle = 'floppy-wizard'
    hairStyle = 'none'
    survivalLevel = 1
    survivalXp = 0
    lastMode = 'solo-survival'
    questUnlockedSpells = @('blink')
    spellQuestAssignments = @()
    questFlags = @()
    inventory = @()
}
[System.IO.File]::WriteAllText($profilePath, ($profile | ConvertTo-Json -Depth 8))

$arguments = @(
    '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
    '--wof-solo', '--wof-villager-view-probe', '--wof-auto-exit=150',
    "--wof-profile-root=$profileRoot", '-logFile', $logPath
)
$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru

function Save-WofEngineImage {
    param([IntPtr]$WindowHandle, [string]$Path)
    $rect = New-Object WofEngineMenuCapture+RECT
    if (-not [WofEngineMenuCapture]::GetClientRect($WindowHandle, [ref]$rect)) { throw 'GetClientRect failed.' }
    $point = New-Object WofEngineMenuCapture+POINT
    if (-not [WofEngineMenuCapture]::ClientToScreen($WindowHandle, [ref]$point)) { throw 'ClientToScreen failed.' }
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

function Wait-WofMarker {
    param([string]$Pattern, [int]$PreviousCount = -1, [int]$Seconds = 8)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        Start-Sleep -Milliseconds 100
        $count = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            @(Select-String -LiteralPath $logPath -Pattern $Pattern).Count
        } else { 0 }
        $process.Refresh()
        $found = if ($PreviousCount -lt 0) { $count -gt 0 } else { $count -gt $PreviousCount }
    } while (-not $found -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
    return $found
}

function Submit-WofCommand {
    param([string]$Text, [string]$Action)
    $openCount = @(Select-String -LiteralPath $logPath -Pattern 'COMMAND_CONSOLE_OPEN value=/' -ErrorAction SilentlyContinue).Count
    [WofEngineMenuCapture]::SendKey(0xBF)
    if (-not (Wait-WofMarker -Pattern 'COMMAND_CONSOLE_OPEN value=/' -PreviousCount $openCount -Seconds 5)) {
        throw 'Physical Slash did not open the command console.'
    }
    # The Unity input field focuses asynchronously. Select its contents only after
    # that focus settles, then type the complete command including its slash so a
    # late focus callback can never replace the prefix.
    Start-Sleep -Milliseconds 220
    [WofEngineMenuCapture]::SetKey(0x11, $true)
    [WofEngineMenuCapture]::SendKey(0x41)
    [WofEngineMenuCapture]::SetKey(0x11, $false)
    [WofEngineMenuCapture]::SendKey(0x08)
    [WofEngineMenuCapture]::SendText('/' + $Text)
    Start-Sleep -Milliseconds 250
    [WofEngineMenuCapture]::SendKey(0x0D)
    if (-not (Wait-WofMarker -Pattern "action=$Action" -Seconds 7)) {
        throw "Physical command submission did not produce action $Action."
    }
}

try {
    if (-not (Wait-WofMarker -Pattern 'VILLAGER_VIEW_PROBE_READY' -Seconds 90)) {
        throw 'Engine-menu player did not become ready.'
    }
    $windowHandle = $process.MainWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Engine-menu player has no main window.' }
    [WofEngineMenuCapture]::ForceForeground($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    if ([WofEngineMenuCapture]::GetForegroundWindow() -ne $windowHandle) {
        throw 'Unity player did not become the foreground window.'
    }

    # Step sideways before placement so the combat target remains centered on the
    # fixed QA aim line without occupying the town villager's world position.
    [WofEngineMenuCapture]::SetKey(0x44, $true)
    Start-Sleep -Milliseconds 1800
    [WofEngineMenuCapture]::SetKey(0x44, $false)
    Start-Sleep -Milliseconds 250

    Submit-WofCommand -Text 'engine' -Action 'OpenEngineMenu'
    if (-not (Wait-WofMarker -Pattern 'ENGINE_MENU open=true' -Seconds 7)) { throw '/engine did not open the menu.' }
    Start-Sleep -Milliseconds 450
    Save-WofEngineImage -WindowHandle $windowHandle -Path $openPath
    [WofEngineMenuCapture]::DragClient($windowHandle, 1135, 462, 1135, 622)
    Save-WofEngineImage -WindowHandle $windowHandle -Path $systemsScrolledPath

    [WofEngineMenuCapture]::ClickClient($windowHandle, 205, 213)
    Start-Sleep -Milliseconds 250
    [WofEngineMenuCapture]::ClickClient($windowHandle, 360, 248)
    Start-Sleep -Milliseconds 450
    Save-WofEngineImage -WindowHandle $windowHandle -Path $selectedPath

    [WofEngineMenuCapture]::ClickClient($windowHandle, 1026, 317)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_PLACED id=campfire-small' -Seconds 7)) {
        throw 'Physical Place Selected did not place the campfire.'
    }
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_SYNC count=1' -Seconds 7)) {
        throw 'Placed campfire did not enter the replicated object list.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofEngineImage -WindowHandle $windowHandle -Path $placedPath

    for ($pageIndex = 0; $pageIndex -lt 3; $pageIndex++) {
        [WofEngineMenuCapture]::ClickClient($windowHandle, 1144, 400)
        Start-Sleep -Milliseconds 120
    }
    Start-Sleep -Milliseconds 250
    Save-WofEngineImage -WindowHandle $windowHandle -Path $placementScrolledPath
    [WofEngineMenuCapture]::ClickClient($windowHandle, 950, 412)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_SLOT action=save slot=slot-1 count=1' -Seconds 7)) {
        throw 'Physical Save did not store slot 1.'
    }
    $storagePath = Join-Path $storageRoot 'engine-placeables-v1.json'
    if (-not (Test-Path -LiteralPath $storagePath -PathType Leaf)) { throw 'Engine save JSON is not beside the D-drive build.' }

    [WofEngineMenuCapture]::ClickClient($windowHandle, 1026, 174)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_EDIT action=select' -Seconds 7)) {
        throw 'Physical placed-object row click did not select the campfire.'
    }
    [WofEngineMenuCapture]::ClickClient($windowHandle, 950, 272)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_EDIT action=preview' -Seconds 7)) {
        throw 'Physical Preview did not target the selected placed object.'
    }
    $placedCount = @(Select-String -LiteralPath $logPath -Pattern 'ENGINE_PLACEABLE_PLACED id=campfire-small').Count
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1024, 272)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_EDIT action=move' -Seconds 7) -or
        -not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_PLACED id=campfire-small' -PreviousCount $placedCount -Seconds 7)) {
        throw 'Physical Move did not replace the selected placed object.'
    }
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1101, 272)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_EDIT action=delete' -Seconds 7)) {
        throw 'Physical Delete did not remove the selected placed object.'
    }
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1024, 412)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_SLOT action=load slot=slot-1 count=1' -Seconds 7)) {
        throw 'Physical Load did not restore slot 1.'
    }
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1101, 412)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_SLOT action=delete slot=slot-1' -Seconds 7)) {
        throw 'Physical slot Delete did not remove slot 1.'
    }
    for ($pageIndex = 0; $pageIndex -lt 6; $pageIndex++) {
        [WofEngineMenuCapture]::ClickClient($windowHandle, 1144, 105)
        Start-Sleep -Milliseconds 120
    }
    Save-WofEngineImage -WindowHandle $windowHandle -Path $placementRestoredTopPath
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1026, 355)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_EDIT action=clear' -Seconds 7) -or
        -not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_SYNC count=0' -PreviousCount 1 -Seconds 7)) {
        throw 'Physical Clear Placed did not empty the restored object list before the direct command test.'
    }

    Start-Sleep -Milliseconds 500
    [WofEngineMenuCapture]::ClickClient($windowHandle, 205, 305)
    Start-Sleep -Milliseconds 500
    [WofEngineMenuCapture]::ClickClient($windowHandle, 360, 248)
    Start-Sleep -Milliseconds 500
    Save-WofEngineImage -WindowHandle $windowHandle -Path $dummySelectedPath
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1026, 317)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_TRAINING_DUMMY_SPAWN.*persistent=false' -Seconds 7)) {
        throw 'Physical engine-menu placement did not spawn the nonpersistent React training dummy.'
    }
    Start-Sleep -Milliseconds 350
    Save-WofEngineImage -WindowHandle $windowHandle -Path $dummyPath
    $storageAfterDummy = Get-Content -LiteralPath $storagePath -Raw | ConvertFrom-Json
    if (@($storageAfterDummy.current).Count -ne 0) {
        throw 'React training dummy incorrectly entered persistent engine storage.'
    }
    [WofEngineMenuCapture]::ClickClient($windowHandle, 1100, 60)
    if (-not (Wait-WofMarker -Pattern 'ENGINE_MENU open=false' -Seconds 7)) { throw 'Physical Close did not close the menu.' }
    Start-Sleep -Milliseconds 450
    Save-WofEngineImage -WindowHandle $windowHandle -Path $dummyReadyPath

    [WofEngineMenuCapture]::SendKey(0x45)
    Start-Sleep -Milliseconds 250
    [WofEngineMenuCapture]::ClickClient($windowHandle, 344, 216)
    if (-not (Wait-WofMarker -Pattern 'SPELL_EQUIPPED owner=0 hand=Left spell=Fireball' -Seconds 7)) {
        throw 'Physical spell-menu selection did not equip Fireball for the dummy combat probe.'
    }
    [WofEngineMenuCapture]::SendKey(0x45)
    Start-Sleep -Milliseconds 300
    for ($castIndex = 0; $castIndex -lt 5; $castIndex++) {
        $hitCount = @(Select-String -LiteralPath $logPath -Pattern 'TRAINING_DUMMY_HIT' -ErrorAction SilentlyContinue).Count
        [WofEngineMenuCapture]::ClickClient($windowHandle, 640, 360)
        if (-not (Wait-WofMarker -Pattern 'TRAINING_DUMMY_HIT' -PreviousCount $hitCount -Seconds 7)) {
            throw "Physical Fireball cast $($castIndex + 1) did not damage the replicated training dummy."
        }
        if ($castIndex -lt 4) { Start-Sleep -Milliseconds 1050 }
    }
    if (-not (Wait-WofMarker -Pattern 'TRAINING_DUMMY_HIT.*health=0 down=true' -Seconds 3)) {
        throw 'The fifth exact-React Fireball hit did not knock down the training dummy.'
    }
    Save-WofEngineImage -WindowHandle $windowHandle -Path $dummyDownPath
    if (-not (Wait-WofMarker -Pattern 'TRAINING_DUMMY_RESPAWN.*health=120' -Seconds 5)) {
        throw 'The training dummy did not respawn after the exact React delay.'
    }
    Start-Sleep -Milliseconds 250
    Save-WofEngineImage -WindowHandle $windowHandle -Path $dummyRespawnPath

    Submit-WofCommand -Text 'place hut-log-cabin' -Action 'PlaceEngineObject'
    if (-not (Wait-WofMarker -Pattern 'ENGINE_PLACEABLE_PLACED id=hut-log-cabin' -Seconds 7)) {
        throw 'Physical /place did not create the requested hut.'
    }
    Submit-WofCommand -Text 'vclip on' -Action 'SetVClipEnabled'
    [WofEngineMenuCapture]::SetKey(0x10, $true)
    [WofEngineMenuCapture]::SetKey(0x20, $true)
    Start-Sleep -Milliseconds 1450
    [WofEngineMenuCapture]::SetKey(0x20, $false)
    [WofEngineMenuCapture]::SetKey(0x10, $false)
    if (-not (Wait-WofMarker -Pattern 'VCLIP_MOVEMENT' -Seconds 7)) {
        throw 'Physical VCLIP ascent did not move the camera to the survival placement surface.'
    }
    Start-Sleep -Milliseconds 500
    Save-WofEngineImage -WindowHandle $windowHandle -Path $worldPath

    [PSCustomObject]@{
        ProcessId = $process.Id
        OpenCapture = $openPath
        SystemsScrolledCapture = $systemsScrolledPath
        SelectedCapture = $selectedPath
        PlacedCapture = $placedPath
        PlacementScrolledCapture = $placementScrolledPath
        PlacementRestoredTopCapture = $placementRestoredTopPath
        TrainingDummySelectedCapture = $dummySelectedPath
        TrainingDummyCapture = $dummyPath
        TrainingDummyReadyCapture = $dummyReadyPath
        TrainingDummyDownCapture = $dummyDownPath
        TrainingDummyRespawnCapture = $dummyRespawnPath
        WorldCapture = $worldPath
        Storage = $storagePath
        Log = $logPath
        EngineCommand = $true
        CatalogSelection = $true
        PlaceMoveDelete = $true
        SaveLoadDeleteSlot = $true
        DirectPlaceCommand = $true
        NonpersistentTrainingDummy = $true
        TrainingDummyPhysicalCombat = $true
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if ($profileRoot.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $profileRoot -PathType Container)) {
        Remove-Item -LiteralPath $profileRoot -Recurse -Force
    }
}
