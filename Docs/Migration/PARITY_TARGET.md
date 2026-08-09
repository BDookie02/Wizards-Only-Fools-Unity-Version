# Verified React parity target

## Canonical baseline

- Current playable source: `D:\CodexProjects\Wizards-Only-Fools-React-Latest`
- Current GitHub history checkout: `D:\CodexProjects\Wizards-Only-Fools-React-GitHub-Main-0e293150`
- GitHub `main` identity: `0e293150cc9d92dcab19f8775889b3c43f2ee54a`
- Provenance and conflict rules: `Docs/Migration/REACT_SOURCE_PROVENANCE.md`
- Live backend: `server.js`
- Stale backend that must not be ported as canonical: `server.ts`
- Verified source-module count: 539
- Modes: custom lobby, solo survival, multiplayer survival
- Browser room identity: `?room=wof-xxxxx`

## Player constants

- First-person capsule, gravity `-20`
- Walk speed `8`, jump `8`, thruster impulse `6`, slide speed `18`
- Crouch multiplier `0.44`, speed/jump boost `2`, slow multiplier `0.35`
- Ground coyote window `180 ms`
- Health `100`, armor `50`, rune power `60`

## Combat constants

- General cooldown `1 s`; Ice Shard `400 ms`
- Fireball `20`, phase beam `35`, flamethrower particle `5`, rings `20`, kunai `15`, meteor `18`
- Heal `2 HP/s`; healing crystal `10 HP/s` inside radius `3`
- Slow/sleep `8 s`; poison/acid `10 s`; toxic damage `5 HP/s`
- Death respawn delay `3 s`; multiplayer respawn `[0,5,0]`

## Spell catalog

`fireball`, `iceshard`, `arcanebeam`, `healspell`, `icespell`, `ringsofpower`, `lightning`, `smokebomb`, `portal`, `blink`, `grab`, `tornado`, `meteorshower`, `flamethrower`, `discshield`, `orbshield`, `kunai`, `healingcrystals`, `magicarmor`, `jumpboost`, `speedboost`, `tungstonballsack`, `sleep`, `poison`, `acid`, `magicglassorb`.

Each Unity spell must be a versioned data definition with explicit authority, damage, status, cooldown, and presentation. Conflicting React behavior is documented rather than silently copied.

## Survival world

- Deterministic procedural terrain and biome generation
- Chunk size `512`; render radius `3`; collision radius `2`
- LOD segments: near `32`, mid `12`, far `4`
- Biomes: plains, jungle, desert, swamp, mushroom, tallgrass
- Authored locations: base village, Chicago, desert, swamp, mountain, graveyard, Darrel Grove, Lily Coil
- Day/night cycle `600 s`

The generator will be migrated against golden coordinate/height/biome fixtures before visual dressing. This protects traversal and authored-location alignment.

## Persistence

React uses browser-local, partially duplicated state. Unity will replace it with one versioned JSON profile plus schema migrations and a separate device-settings file. Autosave parity is `15 s`.

## Migration order

1. Bootstrap/profile/mode/session state machine.
2. Player motor, grounding, camera, desktop/controller/touch input.
3. Dual-hand hotbars, mana, Fireball.
4. Health, armor, status, death, respawn.
5. Two-player authoritative LAN session.
6. Base village/custom-lobby arena.
7. Deterministic survival chunk streamer and parity fixtures.
8. Remaining spell families.
9. Inventory, quests, NPC/dialogue, maps, placeables.
10. Production session services, console paths, and managed voice.
