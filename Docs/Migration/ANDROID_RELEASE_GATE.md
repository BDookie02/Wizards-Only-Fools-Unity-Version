# Android v0.4.15 release gate

Verified on 2026-08-13 against the exact release APK at `Builds/Android/WizardsOnlyFools.apk`.

## Artifact identity

- Package: `com.WizardsOnlyFools.WizardsOnlyFools`
- Version: `0.4.15` (`versionCode` 18)
- Minimum/target Android API: 25/36
- Size: 157,561,899 bytes
- SHA-256: `34199140e8a2ebeb25e65500c018873bd6a9fdb375dde5c54cdd990e118fda26`
- APK Signature Scheme v2: verified
- Signing-certificate SHA-256: `844f6c9b6fa3d0cfd48febd45f0568e019ae9edca1f4dd039ab6131c5292f7eb`
- Activity orientation: Android `userLandscape` in the packaged manifest; the runtime requested landscape and rendered at 2400 x 1080.

## Runtime evidence

The exact ARM64 release APK was installed and launched on the D-hosted Android 15/API 35 Google APIs emulator. The emulator ran the ARM64 player through Android's Berberis translation layer; this was not an Editor or mock runtime.

The v0.4.15 public-session interaction was physically injected through Android and visually inspected:

1. Touch advanced `PRESS ANYWHERE TO PLAY`, selected `MULTIPLAYER`, and opened `CUSTOM LOBBY`.
2. Touch focused the public invite field and opened the native Android input overlay.
3. The exact Windows Relay-host invite `WTWWJW` was entered, then one Android Done/Enter event submitted the field without touching a second lobby action.
4. Android logged `CONNECTING TO UNITY PUBLIC ONLINE`, `JOINING PUBLIC LOBBY WTWWJW`, local `CLIENT_CONNECTED id=1`, `SESSION_READY mode=Client`, and `JOINED PUBLIC LOBBY WTWWJW`.
5. The independent Windows-host log recorded `CLIENT_CONNECTED id=1 local=0`, and the inspected Android frame showed the live authored base village, equipped hands, HUD, minimap, public invite code, and touch controls.
6. No duplicate authentication attempt or `player is already signing in` failure occurred. While the asynchronous public operation is active, the input, public-host button, and join button are locked against duplicate submission.

The immediately preceding v0.4.14 gate covered the unchanged touch Solo, 26-spell book, pause/settings, map waypoint, Lily Coil fast-travel, and controller-triggered touch-overlay hiding paths on the same Android 15 emulator. This v0.4.15 pass reverified the changed launch-flow path and the resulting live gameplay frame rather than claiming those unrelated interactions were repeated.

The v0.4.15 app log confirmed initialization of all five additive authored-location scenes, including Lily Coil, plus the streamed terrain, 2,526-tree foliage layout, five desert cactus chunks, 56,000-tuft grass field, sky, and menus before the public-session interaction.

The full Unity EditMode regression suite passed 570/570 before the final build. Two focused launch-flow tests lock keyboard submission and listener cleanup; the existing focused tests continue to lock linker preservation, landscape-only autorotation, map modal rules, and screen-relative mobile pinch math into reproducible project automation. Both `SphereCollider` and `CapsuleCollider` remain explicitly preserved for the stripped IL2CPP player.

## Deliberately open hardware gates

This emulator pass does not claim representative-phone frame rate, thermal/lifecycle behavior, a physically attached controller, or audible microphone/Vivox behavior. Android-to-Windows Relay is verified on the emulator; real-phone Relay is not. The emulator accepted single-touch map input but could not deliver a trustworthy two-contact gesture to Unity, so pinch-to-zoom remains implemented and unit-tested but not physically certified. Those gates require real phone/controller/audio/network hardware and remain open in the parity matrix.
