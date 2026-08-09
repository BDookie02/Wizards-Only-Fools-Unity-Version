# Multiplayer authority contract

The React build is a useful behavioral oracle but not a safe authority model. It trusts client transforms, casts, collisions, and hit reports. Unity does not preserve that flaw.

## Immediate LAN architecture

- Unity Transport + Netcode for GameObjects behind session/transport interfaces.
- Server simulation at `30 Hz`.
- Local input sampling at `60 Hz`.
- Routine state snapshots at `15 Hz`; high-priority/grab snapshots at `30 Hz`.
- Sequence numbers and simulation ticks on all input/cast commands.
- Server simulates movement, cooldowns, mana, projectiles, collisions, damage, statuses, death, and respawn.
- Owner predicts movement and reconciles to authoritative snapshots.
- Remote players interpolate timestamped snapshots; teleports and respawns snap beyond an explicit threshold.
- Lobby rules are server-owned and replicated.
- Protocol/build version mismatch rejects connection.

## Parity-facing session behavior

- `Host LAN`, `Join LAN`, and `Solo` use one session abstraction.
- Room codes retain the `wof-xxxxx` presentation.
- IP/port fallback remains available.
- Hard admission configuration remains `32`, while validation starts at 2, 4, and 8 clients.
- Empty dedicated LAN sessions may retain state for `10 s`.
- Death is server-owned and respawn remains `3 s`.

## Browser and console boundary

Native LAN success does not prove browser parity. WebGL requires a client-only build, local static host, and WebSocket-compatible server transport. Before browser parity is claimed, a phone browser must physically join the same session and complete move, cast, damage, death, and respawn checks.

Public cross-platform multiplayer will later swap LAN discovery/direct endpoints for authenticated lobby, matchmaking, relay, or dedicated allocation without changing gameplay systems. Voice must use a console-compatible managed provider; the React STUN-only WebRTC mesh is not a production target.

