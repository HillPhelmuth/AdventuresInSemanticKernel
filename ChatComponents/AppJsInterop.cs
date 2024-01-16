using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatComponents
{
    public class AppJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/ChatComponents/appJsInterop.js").AsTask());

        public async ValueTask ScrollDown(ElementReference elementReference)
        {
            try
            {
                var module = await _moduleTask.Value;
                await module.InvokeVoidAsync("scrollDown", elementReference);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on ScrollDown: {ex.Message}");
            }
        }

        public async ValueTask AddCodeStyle(ElementReference element)
        {
            try
            {
                var module = await _moduleTask.Value;
                await module.InvokeVoidAsync("addCodeStyle", element);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on AddCodeStyle: {ex.Message}");
            }
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_moduleTask.IsValueCreated)
                {
                    var module = await _moduleTask.Value;
                    await module.DisposeAsync();
                }
            }
            catch (JSDisconnectedException ex)
            {
                Console.WriteLine($"Error on DisposeAsync: {ex.Message}");
            }
        }
    }
}