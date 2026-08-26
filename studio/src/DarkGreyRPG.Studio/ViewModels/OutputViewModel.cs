using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DarkGreyRPG.Studio.ViewModels;

public enum OutputKind
{
    Information,
    Success,
    Warning,
    Error,
}

public sealed record OutputEntry(
    DateTimeOffset Timestamp,
    string Message,
    OutputKind Kind = OutputKind.Information,
    string? Source = null)
{
    public string Text => Message;
}

public sealed class OutputViewModel : ObservableObject
{
    public const int DefaultMaxEntries = 500;

    private readonly Func<DateTimeOffset> _now;
    private readonly int _maxEntries;

    public OutputViewModel(
        int maxEntries = DefaultMaxEntries,
        Func<DateTimeOffset>? now = null)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Maximum output entries must be positive.");
        }

        _maxEntries = maxEntries;
        _now = now ?? (() => DateTimeOffset.Now);
        Entries.CollectionChanged += OnEntriesChanged;
    }

    public ObservableCollection<OutputEntry> Entries { get; } = [];

    public int MaxEntries => _maxEntries;

    public int Count => Entries.Count;

    public void Append(
        string message,
        OutputKind kind = OutputKind.Information,
        string? source = null,
        DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Output message cannot be empty.", nameof(message));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Entries.Add(new OutputEntry(timestamp ?? _now(), message, kind, source));
        while (Entries.Count > _maxEntries)
        {
            Entries.RemoveAt(0);
        }
    }

    public void Clear() => Entries.Clear();

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(Count));
    }
}
