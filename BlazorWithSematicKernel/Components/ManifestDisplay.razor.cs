using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components
{
    public partial class ManifestDisplay : ComponentBase
    {
        [Parameter]
        [EditorRequired]
        public ChatGptPluginManifest ChatGptPluginManifest { get; set; } = default!;
    }
}
