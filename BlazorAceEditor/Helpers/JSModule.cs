using Microsoft.JSInterop;

namespace BlazorAceEditor.Helpers
{
    public abstract class JSModule : IAsyncDisposable
    {
        private readonly Task<IJSObjectReference> moduleTask;
        private bool disposedValue;

        protected JSModule(IJSRuntime js, string moduleUrl)
            => moduleTask = js.InvokeAsync<IJSObjectReference>("import", moduleUrl).AsTask();

        protected async ValueTask InvokeVoidAsync(string identifier, params object[]? args)
            => await (await moduleTask).InvokeVoidAsync(identifier, args);
        protected async ValueTask<T> InvokeAsync<T>(string identifier, params object[]? args)
            => await (await moduleTask).InvokeAsync<T>(identifier, args);
        // Release the JS module
        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    await (await moduleTask).DisposeAsync();
                }

                disposedValue = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Do not change this code. Put cleanup code in 'DisposeAsync(bool disposing)' method
            await DisposeAsync(disposing: true);
            GC.SuppressFinalize(this);

        }
    }
}
