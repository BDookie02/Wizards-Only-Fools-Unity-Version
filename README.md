# Wizards Only Fools - Unity Version

## Playable downloads

- [Download the Android APK](https://github.com/BDookie02/Wizards-Only-Fools-Unity-Version/releases/download/unity-version-v0.4.2/WizardsOnlyFools.apk)
- [Download the Windows version](https://github.com/BDookie02/Wizards-Only-Fools-Unity-Version/releases/download/unity-version-v0.4.2/WizardsOnlyFools-Windows-Unity-Version-v0.4.2.zip)
- [View the Unity Version release and testing notes](https://github.com/BDookie02/Wizards-Only-Fools-Unity-Version/releases/tag/unity-version-v0.4.2)

For Windows, extract the complete ZIP before launching `WizardsOnlyFools.exe`. On Android, download and install `WizardsOnlyFools.apk`.

This is the clean Unity recreation of the verified React/Node game at:

`D:\CodexProjects\Wizards-Only-Fools-React-Latest`

The React build remains the behavioral and visual oracle. The abandoned C++ rewrite is not part of this project. Work proceeds in small playable slices so a slice must compile, test, build, and run before the next parity slice is added.

The larger D-drive working snapshot is the current playable oracle; the clean GitHub `main` checkout at commit `0e293150cc9d92dcab19f8775889b3c43f2ee54a` is preserved separately as the public-history oracle. Their verified relationship and conflict rules are recorded in `Docs\Migration\REACT_SOURCE_PROVENANCE.md`.

`Docs\Migration\PARITY_MATRIX.md` is the acceptance ledger. It distinguishes implemented-but-unverified work from completed parity and prevents later slices from silently bypassing build, networking, mobile, or physical-interaction gates.

## Pinned production stack

- Unity `6000.3.21f1`, changeset `c02631ffc030`
- Universal Render Pipeline `17.3.0`
- Input System `1.20.0`
- Netcode for GameObjects `2.13.1` after an isolated `2.13.0` migration baseline
- Unity Transport `2.7.4`
- Unity Test Framework `1.6.0`
- CoplayDev Unity MCP `v10.1.0` over local `stdio`

The first recreation slice contains one launch flow, first-person movement and camera input, native Unity Input System controller support, desktop and mobile controls, dual-hand fireball casting, server-authoritative health/armor/death/respawn, WebSocket-compatible transport, and a two-process LAN combat probe. The second in-progress slice ports the base village's deterministic 512 x 512 terrain, road/moat bands, 307-hut/45-path layout, four hut families, compound colliders, 480-unit arena walls, satellite trees, tree-house village, exact Three.js-baked faceted bushes, water and movement ripples, and the React campfire's low-poly visual, flicker, and fractional server-authoritative damage into reproducible scene generation. It also bakes all 307 deterministic React villagers through the canonical canvas avatar renderer into 52-frame compact archives, preserves idle/blink/startled/angry/facing/jump behavior, reproduces the procedural square-wave yelp, and resolves villager facing against the same nearest alive local-or-remote player rules as React. Darrel now uses his exact special 52-frame avatar archive and React targeting/dialogue rules, including the healing-crystals quest branch, content-sized 760 px desktop modal, profile persistence, keyboard/mouse input, and native controller X/D-pad/A/B interaction. The exact Darrel ingredient, brew, drink, dragon-fight, dragon-peace, fatal-damage, spell-unlock, one-crystal return-gate completion, and two-crystal peaceful reward transitions are also ported. Darrel's source-authored sacred garden is generated at its exact React world chunk with the original terrain, hut, moat, bridges, stairs, river, animated waterfall/runnels/spray, bonsai layouts, petals, Fuji backdrop, return gate, and all 44 original Spirit Dragon frames. The React quest-navigation chain is reproduced with its exact source positions, labels, colors, pulsing beam/rings, and 64 x 64 canvas icon for fields, brewing, drinking, Spirit Dragon, and Darrel turn-in states. The React command-console shell is reproduced at its exact 720 px desktop width with the complete ordered suggestion/filter catalog, 90-character input, physical Slash/Enter/Escape behavior, mouse suggestion selection, `/inventory`, `/forage`, `/brew`, and the now-active `/drink` command, which consumes the draught only when the server-authoritative garden teleport is accepted. Generic villagers use the exact persistent random spell assignment, first/repeat/ready/completed message rules, five-message/12-second React notification feed, physical F input, and native controller X input. The desktop gameplay overlay reproduces the bottom-anchored GRIMOIRE/SPELLS/VITALITY/AETHER status bar and dual mana meter. The React survival inventory and quest journal are ported with the exact 27-slot backpack, 9-slot quick row, five item definitions, active-quest/status rules, Darrel progress rows, screen-pixel desktop sizing, physical I/J/Enter/Escape/arrow input, and native D-pad Right/A/B/Start navigation.

The authored survival world covers React's complete 11 x 8 atlas footprint: 82 connected generated terrain chunks plus the six separately authored Base, Chicago, Swamp, Desert, Mountain, and Graveyard chunks. Outside that atlas, an exact player-centered 37-chunk runtime window now reproduces React's 512-unit coordinate rules, 368.64-unit recenter hysteresis, 32/12/4 render LODs, radius-two 32-segment collision meshes, biome/height/color/river formulas, skirts, and Lily/authored-location exclusions. Physical Windows probes at `(7,4)` and `(-17,9)` verified continuous terrain, colliders, player-centered grass, and the existing pixel treatment with no runtime exceptions; streamed source trees, water surfaces, and standalone route decoration beyond the atlas remain open. The desert village at chunk `(4,-4)`, Chicago at chunk `(-3,-3)`, swamp village at chunk `(0,-3)`, and mountain village at chunk `(3,0)` retain their authored layouts and behavior. The desert keeps its exact 17.885722662941443 base height, 55 buildings/villagers, 52 walls, 10 markets, 22 palms, 37 ladders, 41 fences, 15 clotheslines, 94 street props, source road/pad meshes, procedural sand/adobe textures, collision structures, and 55 original 52-frame villager archives. Chicago keeps its exact 21.912045982731858 base height, 35 enterable buildings/operators, four landmarks, 46 animated vehicles, 220 animated pedestrians, intersections, street furniture, Cloud Gate, source-generated facade/sign/ad textures, and original operator sprites in a separately streamed Unity scene. The swamp keeps its exact 2.7529895363497836 base height, water/platform levels, 13 stilt huts/villagers, 17 walkways, four ramps, 28 lily pads, 18 stumps, 36 reed patches, 91 rope segments, 39 bulbs, original 28-frame idle/12-frame yawn/sleep toad animation, exact colliders, and 13 original 52-frame villager archives in its own streamed scene. The mountain keeps its exact 3.364967894227928 base height and 217.54496789422794 protected summit center, source terrain/trail/summit meshes, 1,793 emitted slope-grass tufts, 48 cliff patches, eight summit cabins, three mineshaft huts, four native-controller ladders, waterfall, opening/catwalk/wall/banquet detail, and 11 original 52-frame villager archives in a fourth streamed scene. Its Unity-only terrain perimeter now forms an irregular caldera rim and broad 500-unit shoulder while preserving every central structure position. Quest targeting resolves across all village managers while preserving the base manager's exact 307-NPC contract and exact desert/swamp/mountain town identities.

The current world-quality slice adds player-centered, instanced Breath-of-the-Wild-style pixel grass with up to 56,000 irregular upright clusters and 760 raised flowers; the verified open-terrain release view emits 44,565 clusters after authored village/desert exclusions. Smoothed terrain normals root the grass into slopes while preserving mostly vertical growth, eliminating the former brushed contour bands. The exact dense React tree formulas supply 2,591 biome trees across 24 source meshes inside the atlas. The exact 600-second React day/night cycle retains its procedural sun, eight moon phases, clouds, stars, and terrain tint, and the 0.46 point-upscaled world render restores React's subtle pixel treatment without lowering HUD resolution. The compact compass is an unclipped live local map with its own restrained color grade and rotating waypoint arrow, while D-pad Left/keyboard M opens the original 4096 x 2979 world atlas with controller zoom/cursor/waypoint controls, a live player marker, highlighted current region, and persistent exploration reveal across the complete 11 x 8 world grid. Server-authoritative fast travel remains available to Base, Chicago, Swamp, Desert, Mountain, and Graveyard. The spell book exposes all 26 React spells at once at desktop and phone-landscape sizes, preserves the original mouse/trigger casting scheme, and uses the outward equipped-hand pose with a same-pose firing flex. Lily Coil has source-derived tunnel, eye animation, grass, lilies, flowers, fireflies, butterflies, seals, and exterior/inside release captures instead of placeholder geometry. The current automated gate is 365/365 tests over 1,266 baked React outputs plus independently hashed bootstrap/Chicago/swamp/mountain/graveyard/Lily-Coil scene payloads, exact React-derived streaming fixtures, Windows executable/additive-scene launch, out-of-atlas runtime probes, 26/26 physical spell-menu equip/cast coverage, live-map desktop/mobile-landscape interaction, command-console/inventory/base/desert/swamp/mountain-generic-quest/Darrel-dialog/Spirit-Dragon controller probes, the mountain left-stick ladder probe, the production-path Darrel drink/garden/return/save probe, LAN/villager/audio smoke, and release-player captures. Full physical world traversal, Android hardware launch, Lily Coil source-side comparison, and awake physical-controller testing remain open. `Docs\Migration` contains the broader parity inventory and the still-open physical/visual gates.

Current native controller bindings mirror the React defaults: left stick moves and climbs mountain ladders, right stick looks, A jumps/selects and drives the React jump thruster while held, X interacts, L3 latches sprint while movement continues, B holds slide/crouch in gameplay and goes back/closes in menus, LT casts the left hand, RT casts the right hand, Start submits highlighted actions or backs out of menus, and D-pad/left stick navigate menus and dialogue choices. D-pad Up opens the spell book, D-pad Left opens the live map, and D-pad Right retains the React inventory shortcut exactly: while standing still, a release after a tap shorter than three seconds opens inventory; A opens its journal and B/Start backs out. Mobile touch controls automatically disappear whenever Unity reports a connected controller. Sprint, slide, crouch, thruster, and ladder traversal use the React speeds, timing, camera-height delta, and original baked directional animation frames. The runtime reports every controller Unity recognizes, including hot-plug changes. Automated native-device integration and Windows-player inventory, spell-menu, live-map, gate-traversal, thruster, base-villager, exact desert/swamp/mountain villager/town, exact mountain ladder-climb, Darrel-dialog, and two-outcome Spirit-Dragon probes pass; the dragon probe verifies the actual in-world controller prompt, X interaction, both LT/RT proximity interactions, controller navigation, fatal server damage, death, respawn, peaceful completion, reward, and close. The command console retains React's keyboard-only Slash opening rather than inventing a controller shortcut, while native Gamepad movement, look, jump, sprint, slide, and casting are regression-tested as suppressed whenever the console owns input. A physical controller must still be awake and reported by Unity before hardware interaction can be marked complete.

## One-command automation

Inspect the complete resumable pipeline without launching Unity, installers, or MCP:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' plan
```

Provision and resume every automatable stage:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' resume -Apply
```

That command is idempotent and stage-aware. It verifies or installs the signed Editor, Unity Hub, and platform modules, installs the pinned D-hosted Unity MCP runtime, performs the two-phase package migration, refreshes the existing Hub entitlement immediately before Unity work, compiles and tests the project, builds Windows/WebGL/Android, and runs the two-process LAN authority probe. Checkpoints bind output evidence to deterministic fingerprints of Unity source, tests, art inputs, packages, settings, and runner scripts, so a source edit cannot inherit a stale green build.

Validate existing platform artifacts without launching the Unity Editor or rebuilding. Windows validation also starts the built player headlessly and requires a real `SESSION_READY` marker so a corrupt scene payload cannot pass on hashes alone:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\wof-unity.ps1' verify
```

The intentionally expensive lower-level full rebuild is named `rebuild-all`; it is not invoked by `verify`.

Two security boundaries intentionally remain manual:

1. Approve the signed Unity platform component installers if Windows displays UAC. Verified reruns do not request elevation again.
2. If Unity reports no license, run the following command, sign in and activate Unity Personal through Unity Hub in the normal signed-in browser, then rerun `resume -Apply`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' open-activation -Apply
```

Automation state is recorded atomically at `D:\UnityAutomationState\Wizards-Only-Fools-Unity\automation-status.json`, so a stopped run reports its exact stage and safe next action.

## D-drive boundary

Project sources, Unity editors, Unity Hub binaries and Electron data, installers, modules, package caches, Android/Gradle state, MCP runtime, Python, temporary files, logs, backups, automation state, and build outputs are pinned to `D:`. The normal browser account profile is reused instead of creating an alternate Chrome identity. Windows, the Codex host, certificate validation, the tiny `unityhub://` protocol registry entry, and Unity licensing services can still use existing operating-system-managed state on the system drive; project scripts cannot safely relocate or bypass those facilities.

After automated verification passes, physical interaction at relevant desktop and mobile/WebGL sizes is still required before a parity slice is declared complete.
