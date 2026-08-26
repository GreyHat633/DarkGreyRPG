# DarkGrey RPG Live Protocol

Version 1 uses UTF-8 JSON Lines over TCP. The runtime binds only to
`127.0.0.1`; the default port is `32145`. Studio can edit projects without a
connection.

Each message is one JSON object followed by `\n`. Lines larger than 1 MiB are
rejected. Runtime-changing requests are queued onto the Minecraft server
thread.

## Runtime hello

```json
{"type":"bridge.hello","protocol":1,"mod_version":"0.5.0"}
```

## State

Studio sends:

```json
{"type":"state.request","request_id":"studio-1"}
```

The `state.snapshot` response contains project resource counts, loaded Actor
entities, online players, Quest progress, Story instances, previous nodes,
waiting state, errors, node condition explanations, and per-Story variables.

## Hot reload

```json
{
  "type": "project.reload",
  "request_id": "studio-2",
  "resource_type": "story",
  "resource_id": "tavern_slime_request"
}
```

Reload is project-atomic so cross-resource references are always checked as one
snapshot. A failure leaves the previous valid runtime snapshot active.

## Pick and Locate

Pick kinds are `actor`, `position`, `region`, `item`, and `entity_type`.

```json
{"type":"pick.begin","request_id":"studio-3","player":"UUID","kind":"actor"}
{"type":"locate.actor","request_id":"studio-4","player":"UUID","actor_id":"tavern_owner"}
```

Pick produces `pick.progress` where needed and ends with `pick.result`.

## Play Test

```json
{
  "type": "test.start",
  "request_id": "studio-5",
  "player": "UUID",
  "story_id": "tavern_slime_request",
  "node_id": "interact_owner"
}
{"type":"test.stop","request_id":"studio-6","player":"UUID"}
```

Start snapshots the player's Quest records, Story variables, and transient
Story instances. It clears the selected Story and its referenced Quests, then
starts at the requested node. Stop restores the snapshot. Online test players
are also restored during an orderly server stop.
