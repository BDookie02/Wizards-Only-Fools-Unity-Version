# React source provenance

The Unity recreation uses two preserved React/Node references on `D:`. They serve different purposes and must not be silently merged.

## Current playable oracle

- Path: `D:\CodexProjects\Wizards-Only-Fools-React-Latest`
- Role: behavioral, visual, content, and feature-parity oracle
- Backend entry point: `server.js`
- Source modules: `539` (`.ts`, `.tsx`, `.js`, and `.jsx` under `src`)
- Public files: `407`
- Non-build, non-dependency files: `964`
- Git metadata: absent; this is a preserved working snapshot, not a commit checkout

This snapshot is the broader implementation. It includes later systems that are absent from the current GitHub `main`, including the refactored multiplayer/input trees, engine placeables, survival streaming and rendering, the expanded spell runtime, authored villages, QA routes, newer assets, and mobile/controller work. It remains the canonical gameplay oracle unless a physically verified later build replaces it.

## Current GitHub history oracle

- Repository: `https://github.com/BDookie02/Wizards-Only-Fools-`
- Preserved clean checkout: `D:\CodexProjects\Wizards-Only-Fools-React-GitHub-Main-0e293150`
- Branch: `main`
- Commit: `0e293150cc9d92dcab19f8775889b3c43f2ee54a`
- Commit date: `2026-05-19T19:59:17-04:00`
- Commit subject: `Zoom map farther out and shrink marker`
- Tracked files: `303`

The checkout was made with the pinned D-drive PortableGit and `core.autocrlf=false`; its worktree was verified clean at the exact commit above. It supplies commit history and the last public GitHub state, but it is not a replacement for the larger playable snapshot.

## Verified relationship

Comparing GitHub's 303 tracked paths to the playable snapshot after normalizing CRLF/LF line endings produced:

- `269` identical files
- `28` meaningfully different files
- `6` GitHub files absent at their old paths because the playable snapshot reorganized those systems
- hundreds of additional playable-snapshot files, especially under `src/game/systems`, `src/game/ui`, `src/game/tools`, `src/game/engine-menu`, and newer sprite/map paths

The abandoned C++ rewrite is outside both references and must not be ported.

## Authority rule

1. Use the playable snapshot for current behavior, visuals, content, constants, and parity tests.
2. Use the clean GitHub checkout for public history and for recovering context that still matches the playable snapshot.
3. When the two disagree, record the conflict and physically verify the playable snapshot; do not silently substitute the older GitHub behavior.
4. Do not describe conversation context as complete unless every relevant conversation has been retrieved and verified separately.

## Reproducible inventory

Run `Tools\inventory-react-oracle.ps1 record -Apply` to hash every playable-oracle file except `node_modules`, including the built `dist`, all public assets, package identities, and every source module. The atomic receipt is stored at `D:\UnityAutomationState\Wizards-Only-Fools-Unity\react-oracle-inventory.json`. Run the same script with `verify` before a parity slice is accepted.
