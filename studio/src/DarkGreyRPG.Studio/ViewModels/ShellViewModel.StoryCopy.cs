using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel
{
    public RelayCommand CopyStoryContentCommand { get; }
    private void CopyStoryContent()
    {
        if (_canonicalGraphStore is not { } store || ProjectHome.SelectedStory is not { } source) return;
        try
        {
            var candidates = store.Stories.List().Where(story => story.Id != source.Id)
                .Select(story => new ResourceDescriptor(ProjectResourceType.Story, story.Id, story.DisplayName, story.SourcePath)).ToArray();
            var target = _resourceWorkspaceDialogs.PickResource(ProjectResourceType.Story, candidates, ResourcePickerMode.CopyIntoStory, source.DisplayName);
            if (target is null) return;
            if (!candidates.Any(candidate => candidate.Id == target.Id)) throw new InvalidOperationException("复制目标必须是另一个本地可编辑故事。");
            if (HasUnsavedDocuments() && !TrySaveAll()) return;
            var selected = ProjectHome.Stories.Single(story => story.Id == target.Id);
            ProjectHome.SelectedStory = selected;
            OpenSelectedStory();
            if (CanonicalStoryWorkspace is not { } workspace || workspace.StoryEditor.Id != target.Id) return;
            var plan = new StoryContentCopyService().Prepare(ProjectDirectory, source.Id, target.Id,
                workspace.StoryEditor.CreateLayoutSnapshot().ToDictionary(pair => pair.Key, pair => new Core.Stories.ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y }));
            StoryContentCopyHistory.Apply(workspace, store, plan);
            RaiseWorkspaceCommandStates();
            ReportSuccess($"已将“{source.DisplayName}”的内容追加到“{target.DisplayName}”，可一次撤销。", "迁移故事内容");
        }
        catch (Exception exception) { ReportFailure("迁移故事内容", exception); }
    }
}
