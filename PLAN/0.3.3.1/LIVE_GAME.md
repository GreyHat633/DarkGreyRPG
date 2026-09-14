> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# 0.3.3.1 isolated live game harness

This harness is prepared for a real MC 1.7.10 client run. It is intentionally
not launched by the build step. The client directory is a fresh game root at
`.tooling/0.3.3.1/live-client`; its `saves` directory starts empty and its
project fixture is independent of the Studio project. The optional DarkGrey
Studio Live Bridge is disabled because the harness owns `127.0.0.1:33071`.

## Build and launch

From the repository root, with no existing client/UI process using the port:

```powershell
$env:JAVA_HOME='E:/Java/jdk-25.0.1'
$env:GRADLE_USER_HOME='E:/Java/MinecraftMod/DarkGrey_RPG/.gradle-user-home'
./gradlew.bat acceptance0331Jar -I PLAN/0.3.3.1/tools/live.gradle --offline --no-daemon --no-configuration-cache --max-workers=2 --console=plain
./gradlew.bat runClient -I PLAN/0.3.3.1/tools/live.gradle --offline --no-daemon --no-configuration-cache --max-workers=2 --console=plain
```

The second command is the manual launch step. This task prepared it but did
not run it; screenshots and UI assertions remain the main agent's boundary.

## JSON endpoint

After the client has entered the fresh world, post one JSON object per request
to `http://127.0.0.1:33071/`:

```powershell
function Invoke-Live([hashtable]$body) {
  Invoke-RestMethod -Uri 'http://127.0.0.1:33071/' -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 20)
}
Invoke-Live @{op='start'; world='DGR0331LiveFresh'}
Invoke-Live @{op='fixture'}
Invoke-Live @{op='server'; text='/give @p minecraft:apple 2'}
Invoke-Live @{op='taskStart'; task='LiveHarness:CollectApple'; placement='collect'}
Invoke-Live @{op='taskStart'; task='LiveHarness:ReachRegion'; placement='region'}
Invoke-Live @{op='taskStart'; task='LiveHarness:SubmitApple'; placement='submit-a'}
Invoke-Live @{op='taskStart'; task='LiveHarness:SubmitApple'; placement='submit-b'}
$inspect = Invoke-Live @{op='inspect'}
$ids = @($inspect.server.entities | Where-Object { $_.actor_ids -contains 'LiveHarness:TarvenBoss' } | ForEach-Object entity_id)
Invoke-Live @{op='interact'; entity_id=$ids[0]}
Invoke-Live @{op='inspect'}
```

`fixture` creates two real `EntityCustomNpc` instances on the integrated
server, binds both to `LiveHarness:TarvenBoss` using the CustomNPC+ stored-data
bridge, and returns their server entity IDs/UUIDs/positions. `interact` runs
the real client `playerController.interactWithEntitySendPacket` path; it does
not call the task manager directly. With both submit placements active, the
server should send a task submit chooser frame. The main agent selects one
chooser row through its real UI, then calls `inspect`; a second `interact` and
chooser selection can settle the other placement.

## Expected inspect assertions

- `server.entities` contains two live entities with actor ID
  `LiveHarness:TarvenBoss`, distinct entity IDs/UUIDs, dimension `0`, and
  positions near `(2,65,0)` and `(3,65,0)`.
- `server.tasks` and `server.journal` expose detached status/progress maps.
  The `submit-a` and `submit-b` rows contain `objective_submit` with
  `submit_actor_id=LiveHarness:TarvenBoss`; region rows expose integer
  `[0,64,0]` coordinates.
- `server.inventory` initially contains two `minecraft:apple` stacks/counts;
  each accepted physical submit consumes exactly one apple. A stale/replayed
  chooser response must not consume another item.
- `server.tick`, `server.heap`, and `server.generation` expose the integrated
  world tick, JVM heap counters, project snapshot revision, and per-player
  Task presentation generation at each `inspect` point.
- `screen` becomes
  `darkgrey.rpg.client.gui.GuiCanonicalTaskSubmitChooser` after the first
  physical interaction with both submit placements active. `buttons` gives
  the real chooser button IDs and bounds for UI automation.
- `media` reports the shared session audio fields when that subsystem is
  present; an `error` field is an observation gap, not a failed Task result.

## Fixture boundaries and gaps

The fixture uses native `minecraft:apple` for an executable item stack and a
CustomNPC+ stored actor ID. It does not add BloodMagic or modify production
sources. This preparation has compile evidence only; no real client launch,
packet delivery, chooser click, task settlement, journal refresh, or media
playback is claimed until the main agent runs and observes them.
