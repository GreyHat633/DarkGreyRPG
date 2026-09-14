> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-E authoring media implementation

This work package supplies reusable Inspector controls for Actor portraits and
local OGG audition. The controls own presentation and media handles; the
Workspace/Inline host owns placement and resource-reference lookup.

## Integration contract

Place the portrait control in the existing Actor Inspector template. It is a
`UserControl`, so no portrait editor window is created:

```xml
<views:ActorPortraitEditor
    DataContext="{Binding}"
    ProjectDirectory="{Binding DataContext.ProjectDirectory, RelativeSource={RelativeSource AncestorType=Window}}"
    IsReadOnly="{Binding IsReadOnly}" />
```

`DataContext` is an `ActorEditorViewModel`. For Story membership views, set
`IsReadOnly="True"` and supply `IsPortraitVariantReferenced` when the host has
the current line-reference index. The callback returning `true` rejects a
rename/remove before `ActorEditorViewModel.ApplyEdit`, preserving Undo and
preventing a stale variant name from silently resolving to another image.

The shared audio surface accepts an absolute local OGG path. A graph host can
bind an already-resolved project path or call `LoadAsync` after resolving its
content-addressed `media/<sha256>.ogg` reference:

```xml
<views:AudioPreviewControl
    MediaPath="{Binding LocalOggPath}"
    MediaName="{Binding MediaDisplayName}" />
```

Use `LocalAudioPreviewService.ResolveProjectMediaPath(projectRoot, mediaRef)`
for a canonical `media/<sha256>.ogg` reference before assigning `MediaPath`.
`LocalAudioPreviewService.Shared` is process-wide ownership. `LoadAsync`
decodes OGG with the shipped `media-tools/ffmpeg/ffmpeg.exe` to a private WAV,
then opens the derivative with WPF `MediaPlayer`; this keeps pause, seek,
duration and end-of-file behavior available without assuming WPF can decode
OGG. `AudioPreviewControl.Release()` is the host hook for an explicit context
switch; `Unloaded` also releases its current player/temp file.

## Behavior evidence and limits

- Portrait default import/replace/clear and variant import/rename/remove call
  the existing content-addressed `ProjectMediaStore` and Actor editor Undo.
- A read-only editor still renders previews and list data; all mutations are
  disabled and guarded by the view model.
- Audio audition is local only and does not set graph fields, execute Story,
  or change runtime playback state. Loading a second preview stops/releases
  the first service.
- Automated WPF tests cover inline control construction, read-only and
  referenced-variant safety, non-OGG rejection, and the seekable preview
  surface. Actual OGG play/pause/seek and packaged-directory verification
  remain a Main live acceptance check because they require the shipped binary,
  a real OGG fixture, an STA WPF message loop, and audible/file-lock evidence.
