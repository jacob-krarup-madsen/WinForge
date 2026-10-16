---
name: godot-networking
description: Godot 4.7.2 netcode: ENet/WebSocket transports, multiplayer authority, RPC discipline, headless dedicated servers, testing.
---

# godot networking

Multiplayer and netcode in **Godot 4** ╬ô├ç├╢ transports, authority, RPCs, dedicated servers, and how to test it all without a second human.

## Architecture defaults

Pick deliberately, in this order:

1. **Authoritative server** ╬ô├ç├╢ server owns state, clients send intents. Default for anything competitive.
2. **Client side prediction + server reconciliation** ╬ô├ç├╢ for movement; never trust client positions.
3. **Lockstep/determinism** ╬ô├ç├╢ only for RTS like games; Godot gives you no built in determinism guarantee, so you must provide fixed tick simulation and fixed point math yourself.

## Transports

| Peer | Use | Notes |
|   |   |   |
| `ENetMultiplayerPeer` | Default LAN/internet UDP | Channels, reliable/unreliable, `create_server(port, max_clients)` / `create_client(ip, port)` |
| `WebSocketPeer` | Browser builds, proxied connections | No UDP; expect higher latency |
| `WebRTCMultiplayerPeer` | P2P with NAT traversal | Complex signaling server required |
| `OfflineMultiplayerPeer` | Single player | Keeps the same code path so MP code does not fork SP |

```gdscript
var peer := ENetMultiplayerPeer.new()
var err := peer.create_server(7777, 8)
if err == OK:
    multiplayer.multiplayer_peer = peer
```

`multiplayer.multiplayer_peer = null` tears down. Always handle `ENetMultiplayerPeer.CONNECTION_*` signals and `multiplayer.connected_to_server` / `connection_failed` / `server_disconnected`.

## Scene replication

  `MultiplayerSpawner` ╬ô├ç├╢ spawns/despawns nodes for peers (set `spawn_path`, `auto_spawn` list)
  `MultiplayerSynchronizer` ╬ô├ç├╢ replicates declared properties; configure **Replication** in the inspector
  Both are editor authored configuration ╬ô├ç├╢ set them up via the editor/MCP, never by hand editing `.tscn`

## Authority

```gdscript
$Player.set_multiplayer_authority(1)              # server owns it
$Player.set_multiplayer_authority(peer_id)         # hand ownership to a client
$Player.get_multiplayer_authority()
$Player.is_multiplayer_authority()                 # gate local input on this
```

Rule: **input is read only where authority lives; state is written only where authority lives.** A client that moves its own body without server approval is a bug, not an optimization.

## RPCs

```gdscript
@rpc("authority", "call_local", "reliable")
func fire_weapon(target: Vector3)  > void: ...

@rpc("any_peer", "call_local", "unreliable_ordered")
func submit_input(state: InputState)  > void:
    if not multiplayer.is_server(): return
    var sender := multiplayer.get_remote_sender_id()
    validate(sender, state)
```

  Direction flags: `"authority"` (only the authority may call), `"any_peer"` (any client ╬ô├ç├╢ **validate the sender server side, always**).
  Transport: `reliable` for discrete events, `unreliable_ordered` for continuous state, `unreliable` for per tick input where a lost packet is acceptable.
  `call_local` decides whether the caller also executes ╬ô├ç├╢ usually what you want for server broadcasts.
  RPCs only work on nodes present in the tree on both sides with matching paths. A path mismatch fails silently ╬ô├ç├╢ check it first when "nothing happens".

## Dedicated / headless server

```bash
godot   headless   path . res://scenes/server.tscn
```

Branch on `DisplayServer.is_headless()` to load server only scenes and skip rendering/audio work. Use `  fixed fps` and `  disable render loop` for deterministic simulation runs.

## Latency handling

  Interpolate remote entities on a 100╬ô├ç├┤200 ms buffer; never snap to the last received transform.
  Predict local movement immediately, reconcile on server correction ╬ô├ç├╢ send corrections as `unreliable_ordered`, not reliable.
  Separate **input** (high rate, lossy ok) from **events** (low rate, reliable) onto different RPCs and, on ENet, different channels.

## Testing without a human

1. **Headless client+server in one process:** instantiate both peers against `127.0.0.1` in a test scene; assert state convergence after N ticks.
2. **Two processes:** launch `godot   headless` twice from a script ╬ô├ç├╢ one server, one client ╬ô├ç├╢ and assert exit codes.
3. **Simulated latency/loss:** inject delay and packet drop in a wrapper peer for regression tests.
4. **Replay:** record the server's authoritative tick stream, replay it, and diff ╬ô├ç├╢ catches desync.

Required assertions before netcode work is done:

  Server rejects malformed/hostile input from any peer
  A late joining client converges to current state
  Disconnect/reconnect leaves no orphaned nodes
  No RPC fires on a path that does not exist on the other side

## Boundaries

  Never trust client data ╬ô├ç├╢ validate and clamp everything on the server.
  Never put secrets (tokens, keys) in RPC payloads or logs.
  Console/platform certification networking requirements: research them, record the requirements as data on the issue, and build to them. No human decision step exists.

