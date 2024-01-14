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
        private ElementReference _chatColumn;
        public ChatState? ChatState { get; set; }
        [Inject]
        private ChatStateCollection ChatStateCollection {get; set; } = default!;
        //[Inject]
        private AppJsInterop AppJsInterop { get; set; } = default!;
        [Parameter] public string Height { get; set; } = "60vh";
        [Parameter] public bool ResetOnClose { get; set; } = true;

        /// <summary>
        /// Unique Identifier for ChatView instance. If you have multiple ChatView components in your application,
        /// you need to provide unique ViewId for each of them. If left empty, ChatState will not persist when component is disposed.
        /// </summary>
        [Parameter]
        public string ViewId { get; set; } = "";

        private bool _generatedViewId;

        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        protected override Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(ViewId))
            {
                ViewId = Guid.NewGuid().ToString();
                ChatState = ChatStateCollection.CreateChatState(ViewId);
                ChatState.PropertyChanged += ChatState_OnChatStateChanged;
                _generatedViewId = true;
            }
            else if (ChatStateCollection.TryGetChatState(ViewId, out var chatState))
            {
                ChatState = chatState;
                ChatState!.PropertyChanged += ChatState_OnChatStateChanged;
            }
            else
            {
                ChatState = ChatStateCollection.CreateChatState(ViewId);
                ChatState.PropertyChanged += ChatState_OnChatStateChanged;
            }

            StateHasChanged();
            
            return base.OnParametersSetAsync();
        }
        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                //ChatState.PropertyChanged += ChatState_OnChatStateChanged;
            }
            return base.OnAfterRenderAsync(firstRender);
        }
       
        public List<(string role, string? message)> GetMessageHistory()
        {
            return ChatState!.ChatMessages.Select(x => (x.Role.ToString(), x.Content)).ToList();
        }
        private async void ChatState_OnChatStateChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ChatState.ChatMessages))
            {
                ChatStateCollection.ChatStates[ViewId] = ChatState!;
                await InvokeAsync(StateHasChanged);
                AppJsInterop = new AppJsInterop(JsRuntime);
                await AppJsInterop.ScrollDown(_chatColumn);
            }
        }

        public void Dispose()
        {
            if (ResetOnClose) ChatState!.Reset();
            if (_generatedViewId) ChatStateCollection.ChatStates.Remove(ViewId);
            ChatState!.PropertyChanged -= ChatState_OnChatStateChanged;
        }
    }
}
