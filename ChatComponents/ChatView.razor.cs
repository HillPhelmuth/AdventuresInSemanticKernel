using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen.Blazor;
using System.ComponentModel;

namespace ChatComponents
{
    public partial class ChatView : ComponentBase, IDisposable
    {
        private RadzenColumn? _column;
        [Inject]
        public ChatState ChatState { get; set; } = default!;
        [Inject]
        private ILoggerFactory LoggerFactory { get; set; } = default!;
        //[Inject]
        private AppJsInterop AppJsInterop { get; set; } = default!;
        [Parameter] public string Height { get; set; } = "60vh";
        [Parameter] public bool ResetOnClose { get; set; } = true;


        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        protected override void OnInitialized()
        {
            
            base.OnInitialized();
        }
        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                ChatState.PropertyChanged += ChatState_OnChatStateChanged;
            }
            return base.OnAfterRenderAsync(firstRender);
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }

        public List<(string role, string? message)> GetMessageHistory()
        {
            return ChatState.ChatMessages.Select(x => (x.Role.ToString(), x.Content)).ToList();
        }
        private async void ChatState_OnChatStateChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ChatState.ChatMessages))
            {
                //LoggerFactory.CreateLogger<ChatView>()
                //    .LogInformation("ChatState.ChatMessages change Handled in {chatview}", nameof(ChatView));
                StateHasChanged();
                AppJsInterop = new AppJsInterop(JsRuntime);
                await AppJsInterop.ScrollDown(_column!.Element);
            }
        }

        public void Dispose()
        {
            ChatState.Reset();
            ChatState.PropertyChanged -= ChatState_OnChatStateChanged;
        }
    }
}
