using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatComponents
{
    public partial class MessageView : ComponentBase
    {
        [Parameter]
        [EditorRequired]
        public Message Message { get; set; } = default!;

        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        private bool _shouldRender;
        private ElementReference _ref;
        protected override bool ShouldRender()
        {
            return _shouldRender;
        }

        protected override Task OnParametersSetAsync()
        {
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
                var appJsInterop = new AppJsInterop(JsRuntime);
                await appJsInterop.AddCodeStyle(_ref);
            }
            await base.OnAfterRenderAsync(firstRender);
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
    }
}
