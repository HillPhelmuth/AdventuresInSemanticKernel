using Microsoft.JSInterop;

namespace BlazorAceEditor.Helpers
{
    public abstract class JSModule : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;
        private bool disposedValue;

        protected JSModule(IJSRuntime js, string moduleUrl)
            => moduleTask = new(() => js.InvokeAsync<IJSObjectReference>("import", moduleUrl).AsTask());

        protected async ValueTask InvokeVoidAsync(string identifier, params object[]? args)
            => await (await moduleTask.Value).InvokeVoidAsync(identifier, args);
        protected async ValueTask<T> InvokeAsync<T>(string identifier, params object[]? args)
            => await (await moduleTask.Value).InvokeAsync<T>(identifier, args);
        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                if (disposing && moduleTask.IsValueCreated)
                {
                    try { await (await moduleTask.Value).DisposeAsync(); }
                    catch (JSDisconnectedException) { }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(disposing: true);
            GC.SuppressFinalize(this);

        }
    }
}
