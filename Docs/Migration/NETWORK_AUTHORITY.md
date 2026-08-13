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
- LAN room codes retain the `wof-xxxxx` presentation; public sessions use the provider-issued invite code.
- IP/port fallback remains available.
- Hard admission configuration remains `32`, while validation starts at 2, 4, and 8 clients.
- Empty dedicated LAN sessions may retain state for `10 s`.
- Death is server-owned and respawn remains `3 s`.

## Browser and console boundary

Native LAN success does not prove browser parity. WebGL requires a client-only build, local static host, and WebSocket-compatible server transport. Before browser parity is claimed, a phone browser must physically join the same session and complete move, cast, damage, death, and respawn checks.

Public cross-platform multiplayer is linked to the authorized Unity Cloud project without changing the LAN gameplay path. Public host and join actions initialize Unity Services with anonymous authentication and request Relay-backed Sessions with a provider-issued invite code. Desktop/mobile Relay uses DTLS while WebGL keeps its required WebSocket transport. A fresh two-process Windows probe created a real Relay host, joined it through a distinct anonymous authentication profile, and observed the remote NGO client on the host; the separate LAN combat/villager/audio smoke still passes. Public failures remain actionable and never silently substitute a LAN session. Android target-device and phone-WebGL Relay behavior still require their platform gates before cross-platform completion is claimed.

Voice uses Unity Vivox `16.10.0`, a console-compatible managed provider; the React STUN-only WebRTC mesh is not a production target. Production builds embed only the public Vivox server, domain, and issuer, keep test mode disabled, and never store the dashboard token key. The same live Windows host/client probe reached Vivox `CONNECTED` on both processes through Unity Authentication. Audible two-device microphone, permission/device switching, disconnect/rejoin, Android, and console policy gates remain open.
