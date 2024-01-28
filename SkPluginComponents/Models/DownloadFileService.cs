using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginComponents.Models
{
    public class DownloadFileService(IJSRuntime jSRuntime) : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/SkPluginComponents/pluginComponentsInterop.js").AsTask());
        public async ValueTask DownloadFileAsync(string fileName, byte[] fileContent)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("downloadFile", fileName, fileContent);
        }
        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}
