# Android v0.4.18 release gate

Verified on 2026-08-13 against the exact release APK at `Builds/Android/WizardsOnlyFools.apk`.

## Artifact identity

- Package: `com.WizardsOnlyFools.WizardsOnlyFools`
- Version: `0.4.18` (`versionCode` 21)
- Minimum/target Android API: 25/36
- Size: 157,751,243 bytes
- SHA-256: `43c238b63de54301934784e10df9d525a492557201a696ad57a3360f22e3aab7`
- APK Signature Scheme v2: verified
- Signing-certificate SHA-256: `844f6c9b6fa3d0cfd48febd45f0568e019ae9edca1f4dd039ab6131c5292f7eb`
- Activity orientation: Android `userLandscape` in the packaged manifest.

## Current artifact and install evidence

The exact ARM64 v0.4.18 APK installed successfully on the D-hosted Android 15/API 35 Google APIs emulator. Android independently reported `versionName=0.4.18`, `versionCode=21`, and `primaryCpuAbi=arm64-v8a`. Its internal manifest and v2 signature independently pass the release validator.

The full 581/581 Unity EditMode suite passes. Three willow fixtures lock React's exact six placement records plus branch/lobe/vine/particle structure, biome colors, particle movement, render/particle radii, grass-inspection suppression, and mobile 24 Hz cap. The final Windows executable passed receipt/payload/`SESSION_READY` validation and a two-frame 1280 x 720 willow probe: willow 4 appeared at the exact tallgrass record `(-694.19, 58.56, -1031.89)`, its particles moved 13.78 metres, and both frames were physically inspected with surrounding terrain, foliage, HUD, hands, and minimap intact.

## Current emulator limitation

A fresh v0.4.18 Android in-player willow frame is not claimed. During Unity startup, the Windows host process `qemu-system-x86_64-headless.exe` crashes with exception `0xc0000005`, disconnecting ADB before WOF runtime initialization. The failure repeats with automatic and software GPU backends. A cold-wiped AVD running the exact published v0.4.17 control APK (157,722,347 bytes, SHA-256 `84d3488b357537e71ba2de88d847329053bb9382fd3f68049299d42ca7c3ad15`) produces the same host crash, so the evidence does not identify a v0.4.18 gameplay regression. A real phone or repaired emulator host is required for the fresh mobile willow visual gate.

## Preserved v0.4.17 runtime evidence

Before the current QEMU host failure, the v0.4.17 ambient-bird interaction executed inside the installed Android player:

1. A Solo session reached the jungle wilderness chunk `(-2,-2)` at 2400 x 1080.
2. The exact React flock generated ten jungle birds and selected a parrot for the close visual probe.
3. The mobile runtime explicitly used the React 24 Hz update cap.
4. The flock moved 23.44 metres around its deterministic orbit before the pass marker.
5. The resulting Android frame was physically inspected and showed the parrot above the jungle canopy with hands, full HUD, minimap, touch controls, trees, and terrain intact.

The same v0.4.17 implementation passed the Windows executable probe twice, including after the final versioned rebuild. Its ten-bird jungle flock moved 55.72 metres between capture gates, and both 1280 x 720 frames were physically inspected. The full 578/578 EditMode suite passes; three focused bird fixtures lock exact React counts, species, first-bird values, village/grass-inspection visibility gates, stage-one timing, orbit speed, vertical drift, and desert-specific speed.

The immediately preceding v0.4.16 mana-source gate remains unchanged evidence:

1. A Solo session reached `SESSION_READY mode=Solo` at 2400 x 1080.
2. The canonical base source became ready at `(11.50, -0.50, 31.50)`.
3. The Android-owned player entered its strict collection radius.
4. The server accepted `BaseInfinite`, chose the most-empty left bar, and refilled it from 0 to 60.
5. The resulting Android gameplay frame was physically inspected and showed the authored base village, source, hands, full HUD, minimap, and touch controls without clipping.

The same 0.4.16 implementation passed independent Windows executable probes for the base source, desert well, and a rotating hut rune. The three sources visibly appeared in their intended locations, each refilled mana under server authority, hut runes disappeared after collection, and the shared three-ring pickup pulse was visually inspected. The desert and rune probes were rerun sequentially because concurrent OS-level capture jobs can focus the wrong game window; only the isolated captures are accepted as visual evidence.

The preceding v0.4.15 public-session gate remains the unchanged networking evidence: Android submitted the provider invite through native Done/Enter, reached `SESSION_READY mode=Client`, and was observed by the independent Windows Relay host as client ID 1. The earlier unchanged gameplay gate covers touch Solo navigation, all 26 spells, pause/settings, map waypoint placement, Lily Coil fast travel, and Android-gamepad-triggered hiding of every touch overlay.

All five additive authored-location scenes, including Lily Coil, initialized in the preceding v0.4.16 Android launch. Its five focused fixtures still lock the exact React source locations, radii, timing, visibility gates, strict horizontal collection rule, and deterministic server-verifiable two-thirds hut-rune cycle.

## Deliberately open hardware gates

This emulator pass does not claim representative-phone frame rate, thermal/lifecycle behavior, a physically attached controller, or audible microphone/Vivox behavior. Android-to-Windows Relay remains verified on the immediately preceding unchanged networking build; real-phone Relay is not. The emulator accepted single-touch map input but could not deliver a trustworthy two-contact gesture to Unity, so pinch-to-zoom remains implemented and unit-tested but not physically certified. Those gates require real phone/controller/audio/network hardware and remain open in the parity matrix.
