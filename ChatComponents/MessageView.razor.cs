using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatComponents
{
    public partial class MessageView : ComponentBase, IAsyncDisposable
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
        private AppJsInterop? _interop;
        [Parameter]
        [EditorRequired]
        public Message Message { get; set; } = default!;
        private string _previousContent = "";

        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        private bool _shouldRender;
        private ElementReference _ref;
        protected override bool ShouldRender()
        {
            return _shouldRender;
        }

        protected override Task OnParametersSetAsync()
        {
            if (Message.Content != _previousContent)
            {
                _previousContent = Message.Content ?? "";
                _shouldRender = true;
            }
            if (Message.IsActiveStreaming || string.IsNullOrEmpty(Message.Content))
            {
                _shouldRender = true;
            }
            return base.OnParametersSetAsync();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!Message.IsActiveStreaming)
            {
                _shouldRender = false;
                _interop ??= new AppJsInterop(JsRuntime);
                await _interop.AddCodeStyle(_ref);
            }
            await base.OnAfterRenderAsync(firstRender);
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var result = Markdown.ToHtml(text, Pipeline);
            return result;

        }
        public async ValueTask DisposeAsync()
        {
            if (_interop is not null) await _interop.DisposeAsync();
        }
    }
}
