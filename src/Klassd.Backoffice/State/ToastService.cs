namespace Klassd.Backoffice.State;

/// <summary>Circuit-scoped toast queue with 3s auto-dismiss. Mirrors the old useToasts.</summary>
public sealed class ToastService
{
    public sealed record Toast(Guid Id, string Message, string Type);

    private readonly List<Toast> _toasts = [];
    public IReadOnlyList<Toast> Toasts => _toasts;

    /// <summary>Raised when the list changes. Subscribers should marshal via InvokeAsync(StateHasChanged).</summary>
    public event Action? Changed;

    public void Show(string message, string type = "")
    {
        var toast = new Toast(Guid.NewGuid(), message, type);
        _toasts.Add(toast);
        Changed?.Invoke();
        _ = DismissAsync(toast);
    }

    public void Success(string message) => Show(message, "success");
    public void Error(string message) => Show(message, "error");

    private async Task DismissAsync(Toast toast)
    {
        await Task.Delay(3000);
        _toasts.Remove(toast);
        Changed?.Invoke();
    }
}
