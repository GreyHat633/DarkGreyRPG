using System.IO;
using System.Windows;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;
using Microsoft.Win32;

namespace DarkGreyRPG.Studio.Services;

public interface IOfflinePackageDialogs
{
    string? PickPackageFile();

    string? PickPackageFile(OfflinePackageDialogKind kind) => PickPackageFile();

    OfflineResourceChoice? PickResource(
        IReadOnlyList<OfflineResourceChoice> resources,
        string title);

    void ShowReadOnlyResource(OfflineResourceChoice choice);

    bool ConfirmRemoval(string package, IReadOnlyList<string> consumers);
}

public enum OfflinePackageDialogKind
{
    Import,
    Reference,
}

/// <summary>Safe default for hosts and tests that do not have a visible window.</summary>
public sealed class NullOfflinePackageDialogs : IOfflinePackageDialogs
{
    public static NullOfflinePackageDialogs Instance { get; } = new();

    private NullOfflinePackageDialogs()
    {
    }

    public string? PickPackageFile() => null;

    public OfflineResourceChoice? PickResource(
        IReadOnlyList<OfflineResourceChoice> resources,
        string title) => null;

    public void ShowReadOnlyResource(OfflineResourceChoice choice)
    {
    }

    public bool ConfirmRemoval(string package, IReadOnlyList<string> consumers) =>
        consumers is not null && consumers.Count == 0;
}

/// <summary>
/// Owns the small, offline-only dialogs used by the story package workflow.
/// Package validation, copying, and import/reference transactions remain outside
/// this UI service.
/// </summary>
public sealed class OfflinePackageDialogs : IOfflinePackageDialogs
{
    private readonly Func<Window?> _ownerProvider;
    private readonly ISettingsService? _settingsService;

    public OfflinePackageDialogs(Func<Window?> ownerProvider, ISettingsService? settingsService = null)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _settingsService = settingsService;
    }

    /// <summary>Shows a file picker for a DGRS package and returns the selected path.</summary>
    public string? PickPackageFile()
        => PickPackageFileCore(null);

    public string? PickPackageFile(OfflinePackageDialogKind kind)
        => PickPackageFileCore(kind);

    private string? PickPackageFileCore(OfflinePackageDialogKind? kind)
    {
        var picker = new OpenFileDialog
        {
            Title = "选择故事包",
            Filter = "故事包与故事组 (*.dgrs;*.dgrs.g)|*.dgrs;*.dgrs.g|所有文件 (*.*)|*.*",
            DefaultExt = ".dgrs",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = ResolveInitialDirectory(kind),
        };

        var owner = _ownerProvider();
        var accepted = owner is null ? picker.ShowDialog() : picker.ShowDialog(owner);
        if (accepted != true) return null;

        RememberDirectory(kind, picker.FileName);
        return picker.FileName;
    }

    private string ResolveInitialDirectory(OfflinePackageDialogKind? kind)
    {
        var remembered = kind switch
        {
            OfflinePackageDialogKind.Import => _settingsService?.Load().LastImportDirectory,
            OfflinePackageDialogKind.Reference => _settingsService?.Load().LastReferenceDirectory,
            _ => null,
        };
        return DgrsExportPathPicker.ResolveInitialDirectory(remembered, StudioStoragePaths.Default.Exports);
    }

    private void RememberDirectory(OfflinePackageDialogKind? kind, string selectedPath)
    {
        if (_settingsService is null || kind is null) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(selectedPath));
        if (string.IsNullOrWhiteSpace(directory)) return;

        try
        {
            var latest = _settingsService.Load();
            _settingsService.Save(kind == OfflinePackageDialogKind.Import
                ? latest with { LastImportDirectory = directory }
                : latest with { LastReferenceDirectory = directory });
        }
        catch (SettingsPersistenceException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Shows one story-organized directory, independent of storage origin.
    /// </summary>
    public OfflineResourceChoice? PickResource(
        IReadOnlyList<OfflineResourceChoice> resources,
        string title)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var viewModel = new OfflineResourcePickerViewModel(resources, title);
        var dialog = new OfflinePackageResourcePickerDialog(viewModel)
        {
            Owner = _ownerProvider(),
        };

        return dialog.ShowDialog() == true ? viewModel.SelectedChoice : null;
    }

    /// <summary>Displays provider metadata and definition content without edit commands.</summary>
    public void ShowReadOnlyResource(OfflineResourceChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var dialog = new OfflineReadOnlyResourceDialog(new OfflineReadOnlyResourceViewModel(choice))
        {
            Owner = _ownerProvider(),
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Confirms removal only when the package still has consumers. With no
    /// consumers, removal is immediately allowed and no warning is shown.
    /// </summary>
    public bool ConfirmRemoval(string package, IReadOnlyList<string> consumers)
    {
        if (string.IsNullOrWhiteSpace(package)) throw new ArgumentException("Package is required.", nameof(package));
        ArgumentNullException.ThrowIfNull(consumers);

        var activeConsumers = consumers
            .Where(consumer => !string.IsNullOrWhiteSpace(consumer))
            .Select(consumer => consumer.Trim())
            .Distinct(StringComparer.CurrentCulture)
            .ToArray();
        if (activeConsumers.Length == 0) return true;

        var consumerText = string.Join(Environment.NewLine, activeConsumers.Select(consumer => $"  • {consumer}"));
        return MessageBox.Show(
            _ownerProvider(),
            $"故事包“{package}”仍被以下内容引用：{Environment.NewLine}{consumerText}{Environment.NewLine}{Environment.NewLine}移除后这些引用会变成“缺失 / 未解析”。是否继续？",
            "移除引用故事包",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}

/// <summary>Resource data exposed by the offline native/provider picker.</summary>
public sealed record OfflineResourceChoice(
    string Kind,
    string Id,
    string DisplayName,
    string Provider,
    string DefinitionJson)
{
    public IReadOnlyList<OfflineResourceChoice> RelatedGraphs { get; init; } = [];
    public bool HasGraph => Kind is "Story" or "Session" or "Task" or "StoryGroup";
    public DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalStoryLogicGraph? ContainerGraph { get; init; }
    public string SourcePackageName { get; init; } = string.Empty;
    public string SourceStoryId { get; init; } = string.Empty;
    public string SourceStoryName { get; init; } = string.Empty;
    public bool IsExternal { get; init; }
    public string TypeLabel => Kind switch
    {
        "Actor" => ActorLabel(), "Item" => "[物品]", "ItemGroup" => "[物品组]", "Session" => "[会话]",
        "Task" => "[任务]", "Story" => "[故事]", "StoryGroup" => "[故事组]", _ => Kind,
    };
    private string ActorLabel()
    {
        using var json = System.Text.Json.JsonDocument.Parse(DefinitionJson);
        return json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "collective" ? "[角色组]" : "[角色]";
    }
    public string ResourceIdLabel { get; init; } = "资源 ID";
}
