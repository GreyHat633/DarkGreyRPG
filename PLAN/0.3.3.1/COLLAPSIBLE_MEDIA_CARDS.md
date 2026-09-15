# Collapsible media cards — 0.3.3.1

## Behavior
- Screen selected-image properties card defaults collapsed in both inline editor and Inspector. Clicking the arrow/header reveals fields. Refresh and changing selected image retain expansion; binding a different node resets it. No graph data changes from expansion.
- Line audio configuration is hidden by default for lines without audio. Checking 语音/音效 opens the bordered configuration card and synchronizes the inline and Inspector views.
- Existing voice_ref starts checked. Unchecking clears voice_ref through canonical history and stops that control's preview through its media binding; imported media files remain. Undo restores the reference. Opening an empty card is UI state only and does not dirty the graph. Removing audio while enabled keeps the card open for replacement.

## Validation
- Full WPF suite: 562 passed, 0 failed. Log: .tooling/0.3.3.1/collapsible-media-tests.log.
- Actual promoted EXE: tested on isolated viewport-anchor-project fixture. Existing audio opened checked; clicking Inspector checkbox off collapsed both views and shortened the inline line node; checking again exposed both controls. Screen cards opened independently in inline editor and Inspector.
- Screenshots: evidence/live/collapsible-media-audio-off.png, collapsible-media-audio-on.png, collapsible-media-screen-open.png, collapsible-media-inspector-expanded.png.
- This pass verified card interaction, not audible playback timing.
- git diff --check passed. Existing worktree changes preserved. No GitHub push in this pass.

## Authoritative delivery
- Path: E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
- Self-contained Windows x64 Release
- ProductVersion: 0.3.3.1
- Bytes: 141956310
- SHA-256: 9A05A2BEFC1352E9FE7D45C52F28DBE28510D3A570CE50F66C251EEBDB2D6D92
- USER_ACCEPTED=NO (awaiting user's own review)
