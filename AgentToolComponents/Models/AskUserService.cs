using Microsoft.AspNetCore.Components;

namespace AgentToolComponents.Models;

public class AskUserService
{
    private readonly List<object> _modals = new();
    private readonly List<AskUserReference> _askUserReferences = new();
    public bool IsOpen { get; set; }
    public AskUserParameters? Parameters { get; set; }
    public event Action<AskUserResults?>? OnModalClose;
    public event Action? OnOpen;
    public event Action<Type, AskUserParameters?, AskUserWindowOptions?>? OnOpenComponent;
    public event Action<RenderFragment<AskUserService>, AskUserParameters?, AskUserWindowOptions?>? OnOpenFragment;

    public void Open()
    {
        IsOpen = true;
        OnOpen?.Invoke();
    }   
    public void Open<T>(AskUserParameters? parameters = null, AskUserWindowOptions? options = null) where T : ComponentBase
    {
        _askUserReferences.Add(new AskUserReference());
        OnOpenComponent?.Invoke(typeof(T), parameters, options);
    }

    public void Open(RenderFragment<AskUserService> renderFragment, AskUserParameters? parameters = null, AskUserWindowOptions? options = null)
    {
        _modals.Add(new object());
        OnOpenFragment?.Invoke(renderFragment, parameters, options);
    }
    public async Task<AskUserResults?> OpenAsync<T>(AskUserParameters? parameters = null, AskUserWindowOptions? options = null, CancellationToken cancellationToken = default) where T : ComponentBase
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_askUserReferences.Count != 0) throw new InvalidOperationException("Another user prompt is already open.");
        TaskCompletionSource<AskUserResults?> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AskUserReference modalRef = new() { TaskCompletionSource = taskCompletionSource };
        _askUserReferences.Add(modalRef);
        OnOpenComponent?.Invoke(typeof(T), parameters, options);
        try { return await modalRef.TaskCompletionSource.Task.WaitAsync(cancellationToken); }
        finally
        {
            if (_askUserReferences.Remove(modalRef)) { IsOpen = false; OnClose(AskUserResults.Empty(false)); }
        }
    }

    public void Close(bool success)
    {
        Close(AskUserResults.Empty(success));
    }
    public void CloseSelf(AskUserResults? results = null)
    {
        Close(results);
    }
   
    public void Close(AskUserResults? result)
    {
        var modalRef = _askUserReferences.LastOrDefault();
        if (modalRef != null)
        {
            IsOpen = false;
            _askUserReferences.Remove(modalRef);
            OnClose(result);
        }
        var taskCompletion = modalRef?.TaskCompletionSource;
        if (taskCompletion == null || taskCompletion.Task.IsCompleted) return;
        taskCompletion.TrySetResult(result);
    }
    private void OnClose(AskUserResults? results)
    {
        OnModalClose?.Invoke(results);
    }   
}
internal class AskUserReference
{
    internal Guid Id { get; set; } = Guid.NewGuid();
    internal TaskCompletionSource<AskUserResults?> TaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public enum Location
{
    Center, Left, Right, TopLeft, TopRight, Bottom
}
