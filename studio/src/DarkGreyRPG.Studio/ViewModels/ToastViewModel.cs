namespace DarkGreyRPG.Studio.ViewModels;

public enum ToastKind
{
    Information,
    Success,
    Warning,
    Error,
}

public sealed class ToastViewModel : ObservableObject
{
    private string _message = string.Empty;
    private ToastKind _kind = ToastKind.Information;
    private bool _isVisible;

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ToastKind Kind
    {
        get => _kind;
        private set => SetProperty(ref _kind, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public void Show(string message, ToastKind kind = ToastKind.Information)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Toast message cannot be empty.", nameof(message));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Message = message;
        Kind = kind;
        IsVisible = true;
    }

    public void Dismiss() => IsVisible = false;
}
