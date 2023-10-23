using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatComponents
{
    // This class provides an example of how JavaScript functionality can be wrapped
    // in a .NET class for easy consumption. The associated JavaScript module is
    // loaded on demand when first needed.
    //
    // This class can be registered as scoped DI service and then injected into Blazor
    // components for use.

    public class AppJsInterop : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public AppJsInterop(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/ChatComponents/appJsInterop.js").AsTask());
        }

        public async ValueTask ScrollDown(ElementReference elementReference)
        {
            try
            {
                var module = await moduleTask.Value;
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
                var module = await moduleTask.Value;
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
                if (moduleTask.IsValueCreated)
                {
                    var module = await moduleTask.Value;
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