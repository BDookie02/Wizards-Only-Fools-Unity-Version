# Android v0.4.14 release gate

Verified on 2026-08-13 against the exact release APK at `Builds/Android/WizardsOnlyFools.apk`.

## Artifact identity

- Package: `com.WizardsOnlyFools.WizardsOnlyFools`
- Version: `0.4.14` (`versionCode` 17)
- Minimum/target Android API: 25/36
- Size: 157,562,839 bytes
- SHA-256: `2b69b34dd69d8f6deb0e34a670eb38e224250627f1bab67f0289c8003d80f850`
- Build receipt completed: `2026-08-13T15:08:03.6930499Z`
- APK Signature Scheme v2: verified
- Signing-certificate SHA-256: `844f6c9b6fa3d0cfd48febd45f0568e019ae9edca1f4dd039ab6131c5292f7eb`
- Activity orientation: Android `userLandscape` in the packaged manifest; the runtime requested landscape and rendered at 2400 x 1080.

## Runtime evidence

The exact ARM64 release APK was installed and launched on the D-hosted Android 15/API 35 Google APIs emulator. The emulator ran the ARM64 player through Android's Berberis translation layer; this was not an Editor or mock runtime.

The following interactions were physically injected through Android and visually inspected:

1. Touch advanced `PRESS ANYWHERE TO PLAY` to the survival-save screen.
2. Touch selected `NEW`; `WofLaunchFlow` logged `LAUNCH_STAGE NewWizard`, and the Android keyboard opened for the generated wizard name.
3. Touch selected `START SOLO SURVIVAL`; the player logged `SERVER_STARTED mode=Solo`, local `CLIENT_CONNECTED`, and `SESSION_READY mode=Solo`.
4. The live gameplay frame showed the authored base village, equipped hands, HUD, minimap, and touch movement/action overlays at 2400 x 1080.
5. An Android gamepad-source event caused Unity to add `AndroidGamepadWithDpadButtons` with controller count 1. The next inspected gameplay frame retained the world/HUD while every touch-control overlay disappeared.
6. Touch opened the complete 26-spell book from its top-left icon. The adjacent React-matching `II` touch control then opened the pause/settings overlay and logged `PAUSE_MENU open=True`.
7. Touch opened the full world map, placed a persistent waypoint, and visibly rendered its purple wizard-hat marker with red visibility outline. The runtime logged the mapped coordinates through `MAP_WAYPOINT_SET`.
8. Touch selected `LILY COIL DIMENSION`; the server-authoritative travel logged `MAP_FAST_TRAVEL destination=LilyCoil`, closed the map, and physically moved the player to `(24812.42, 71.16, -24601.35)`. The inspected gameplay frame showed the live Lily Coil realm and its animated particle field.

The final 1,270-line app-process log contained all required world/session/controller markers and zero matches for the stripped-collider failure, fatal signals, out-of-memory errors, `NullReferenceException`, `MissingMethodException`, or `TypeLoadException`. It also confirmed initialization of all five additive authored-location scenes, including Lily Coil, plus the streamed terrain, 2,526-tree foliage layout, five desert cactus chunks, 56,000-tuft grass field, sky, and menus.

The full Unity EditMode regression suite passed 568/568 before the final build. Both `SphereCollider` and `CapsuleCollider` are explicitly preserved for the stripped IL2CPP player. Focused tests lock linker preservation, landscape-only autorotation, map modal rules, and screen-relative mobile pinch math into reproducible project automation.

## Deliberately open hardware gates

This emulator pass does not claim representative-phone frame rate, thermal/lifecycle behavior, a physically attached controller, audible microphone/Vivox behavior, or an Android-to-Windows Relay session. The emulator accepted single-touch map input but could not deliver a trustworthy two-contact gesture to Unity, so pinch-to-zoom remains implemented and unit-tested but not physically certified. Those gates require real phone/controller/audio/network hardware and remain open in the parity matrix.
