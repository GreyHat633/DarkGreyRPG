using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>One graph history entry owns the entire already-validated file transaction.</summary>
public static class StoryContentCopyHistory
{
    public static void Apply(CanonicalStoryWorkspaceViewModel workspace, CanonicalProjectGraphStore store, StoryContentCopyPlan plan)
    {
        if (workspace.StoryEditor.Id != plan.TargetStoryId) throw new InvalidOperationException("Copy target must be the active Story.");
        var editor = workspace.StoryEditor;
        if (editor.IsDirty) throw new InvalidOperationException("Save the target Story before copying content.");
        var host = editor.Host;
        var beforeGraph = editor.CreateSnapshot().Graph!;
        var beforeLayout = host.Layout.ToDictionary(pair => pair.Key, pair => pair.Value);
        var beforeFrames = host.FrameSnapshot();
        var storyPath = Path.GetRelativePath(plan.ProjectDirectory, store.Stories.GetPath(plan.TargetStoryId));
        var afterGraph = GraphResourceEnvelope.FromJson(Encoding.UTF8.GetString(plan.Changes.Single(change => change.RelativePath == storyPath).DesiredBytes!)).Graph!;
        var layoutPath = Path.GetRelativePath(plan.ProjectDirectory, new CanonicalGraphLayoutStore(plan.ProjectDirectory).LayoutPath);
        var layoutDocument = JsonSerializer.Deserialize<CanonicalGraphLayoutDocument>(plan.Changes.Single(change => change.RelativePath == layoutPath).DesiredBytes!,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        var graphKey = CanonicalGraphLayoutStore.BuildGraphKey(GraphResourceKind.Story, plan.TargetStoryId);
        var afterLayout = layoutDocument.Graphs[graphKey].ToDictionary(pair => pair.Key, pair => new GraphEditorNodePosition(pair.Value.X, pair.Value.Y));
        var afterFrames = layoutDocument.Frames.GetValueOrDefault(graphKey) ?? [];
        var service = new StoryContentCopyService();
        var first = true;
        IReadOnlyList<ProjectFileChange> ApplyHistory(bool forward)
        {
            var changes = plan.Changes.Select(change => forward ? change
                : new ProjectFileChange(change.RelativePath, change.DesiredBytes, change.ExpectedBytes)).ToList();
            var storyIndex = changes.FindIndex(change => change.RelativePath == storyPath);
            var storyBytes = File.ReadAllBytes(Path.Combine(plan.ProjectDirectory, storyPath));
            if (!editor.MatchesPersistedGraph(GraphResourceEnvelope.FromJson(Encoding.UTF8.GetString(storyBytes))))
                throw new IOException("Story changed outside this editor; copy history cannot replace it.");
            changes[storyIndex] = changes[storyIndex] with { ExpectedBytes = storyBytes };

            // The sidecar is shared by every graph. Replay only keys owned by
            // this copy and retain unrelated saves made since the operation.
            var layoutIndex = changes.FindIndex(change => change.RelativePath == layoutPath);
            var layoutChange = changes[layoutIndex];
            var fullLayoutPath = Path.Combine(plan.ProjectDirectory, layoutPath);
            var currentBytes = File.Exists(fullLayoutPath) ? File.ReadAllBytes(fullLayoutPath) : null;
            JsonObject Read(byte[]? bytes) => bytes is null ? new JsonObject { ["schema_version"] = 1 } : JsonNode.Parse(bytes)!.AsObject();
            var current = Read(currentBytes);
            var expected = Read(layoutChange.ExpectedBytes);
            var desired = Read(layoutChange.DesiredBytes);
            var savedLayout = currentBytes is null ? new CanonicalGraphLayoutDocument()
                : JsonSerializer.Deserialize<CanonicalGraphLayoutDocument>(currentBytes,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
            var targetLayoutIsSaved = editor.MatchesPersistedLayout(
                (savedLayout.Graphs.GetValueOrDefault(graphKey) ?? []).ToDictionary(pair => pair.Key, pair => new GraphEditorNodePosition(pair.Value.X, pair.Value.Y)),
                savedLayout.Frames.GetValueOrDefault(graphKey) ?? []);
            foreach (var section in new[] { "graphs", "frames" })
            {
                var oldKeys = expected[section]?.AsObject() ?? new JsonObject();
                var newKeys = desired[section]?.AsObject() ?? new JsonObject();
                var keys = oldKeys.Select(pair => pair.Key).Union(newKeys.Select(pair => pair.Key))
                    .Where(key => !JsonNode.DeepEquals(oldKeys[key], newKeys[key])).ToArray();
                if (keys.Length == 0) continue;
                var currentKeys = current[section]?.AsObject();
                if (currentKeys is null) current[section] = currentKeys = new JsonObject();
                foreach (var key in keys)
                {
                    if (!JsonNode.DeepEquals(currentKeys[key], oldKeys[key]) && !(key == graphKey && targetLayoutIsSaved))
                        throw new IOException("Copied resource layout changed outside this history: " + key);
                    if (newKeys[key] is { } value) currentKeys[key] = value.DeepClone(); else currentKeys.Remove(key);
                }
            }
            changes[layoutIndex] = new(layoutPath, currentBytes, Encoding.UTF8.GetBytes(current.ToJsonString()));
            new ProjectFileTransaction().Apply(plan.ProjectDirectory, changes, () => { });
            return changes;
        }
        void Rollback(IReadOnlyList<ProjectFileChange> changes)
            => new ProjectFileTransaction().Apply(plan.ProjectDirectory,
                changes.Select(change => new ProjectFileChange(change.RelativePath, change.DesiredBytes, change.ExpectedBytes)).ToArray(), () => { });
        void Restore(GraphDocument graph, IReadOnlyDictionary<string, GraphEditorNodePosition> layout, IReadOnlyList<GraphCommentFrame> frames)
        {
            host.RestoreClipboardSnapshot(graph, layout);
            host.RestoreFrames(frames);
            editor.MarkSaved();
            workspace.ApplyResourceSnapshot(new CanonicalStoryWorkspaceLoader(store).Load(plan.TargetStoryId), CanonicalStoryFolderKind.Actors);
        }
        void Forward()
        {
            IReadOnlyList<ProjectFileChange> changes;
            if (first) { service.Apply(plan); changes = plan.Changes; } else changes = ApplyHistory(true);
            try { Restore(afterGraph, afterLayout, afterFrames); }
            catch { Rollback(changes); Restore(beforeGraph, beforeLayout, beforeFrames); throw; }
            first = false;
        }
        void Backward()
        {
            var changes = ApplyHistory(false);
            try { Restore(beforeGraph, beforeLayout, beforeFrames); }
            catch { Rollback(changes); Restore(afterGraph, afterLayout, afterFrames); throw; }
        }
        host.EditMetadata(Backward, Forward);
    }
}
