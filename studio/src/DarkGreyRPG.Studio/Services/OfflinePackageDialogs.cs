using System.Windows;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;
using Microsoft.Win32;

namespace DarkGreyRPG.Studio.Services;

public interface IOfflinePackageDialogs
{
    string? PickPackageFile();

    OfflineResourceChoice? PickResource(
        IReadOnlyList<OfflineResourceChoice> native,
        IReadOnlyList<OfflineResourceChoice> external,
        string title);

    void ShowReadOnlyResource(OfflineResourceChoice choice);

    bool ConfirmRemoval(string package, IReadOnlyList<string> consumers);
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
        IReadOnlyList<OfflineResourceChoice> native,
        IReadOnlyList<OfflineResourceChoice> external,
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

    public OfflinePackageDialogs(Func<Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
    }

    /// <summary>Shows a file picker for a DGRS package and returns the selected path.</summary>
    public string? PickPackageFile()
    {
        var picker = new OpenFileDialog
        {
            Title = "选择故事包",
            Filter = "故事包 (*.dgrs)|*.dgrs|所有文件 (*.*)|*.*",
            DefaultExt = ".dgrs",
            CheckFileExists = true,
            Multiselect = false,
        };

        var owner = _ownerProvider();
        var accepted = owner is null ? picker.ShowDialog() : picker.ShowDialog(owner);
        return accepted == true ? picker.FileName : null;
    }

    /// <summary>
    /// Lets the caller choose from native project resources or read-only provider
    /// resources. The two source lists stay visibly separated in the dialog.
    /// </summary>
    public OfflineResourceChoice? PickResource(
        IReadOnlyList<OfflineResourceChoice> native,
        IReadOnlyList<OfflineResourceChoice> external,
        string title)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(external);

        var viewModel = new OfflineResourcePickerViewModel(native, external, title);
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
    public bool HasGraph => Kind is "Story" or "Session" or "Task";
    public string SourcePackageName { get; init; } = string.Empty;
    public string SourceStoryId { get; init; } = string.Empty;
    public string ResourceIdLabel { get; init; } = "资源 ID";
}
