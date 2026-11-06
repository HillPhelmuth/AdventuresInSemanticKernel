using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Extensions.AI;
using Radzen.Blazor;
using System.ComponentModel;

namespace ChatComponents
{
    public partial class ChatView : ComponentBase, IAsyncDisposable
    {
        private ElementReference _chatColumn;
        public ChatState ChatState { get; set; } = new();
        [Inject]
        private ChatStateCollection ChatStateCollection {get; set; } = default!;
        private AppJsInterop? _interop;
        [Parameter] public string Height { get; set; } = "60vh";
        [Parameter] public bool ResetOnClose { get; set; } = true;

        /// <summary>
        /// Unique Identifier for ChatView instance. If you have multiple ChatView components in your application,
        /// you need to provide unique ViewId for each of them. If left empty, ChatState will not persist when component is disposed.
        /// </summary>
        [Parameter]
        public string ViewId { get; set; } = "";

        private string? _generatedViewId;
        private ChatState? _subscribedState;
        private bool _disposed;

        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        protected override Task OnParametersSetAsync()
        {
            if (_subscribedState is not null) _subscribedState.PropertyChanged -= ChatState_OnChatStateChanged;
            if (string.IsNullOrEmpty(ViewId))
            {
                ViewId = Guid.NewGuid().ToString();
                ChatState = ChatStateCollection.CreateChatState(ViewId);
                _generatedViewId = ViewId;
            }
            else if (ChatStateCollection.TryGetChatState(ViewId, out var chatState) && chatState is not null)
            {
                ChatState = chatState;
            }
            else
            {
                ChatState = ChatStateCollection.CreateChatState(ViewId);
                
            }
            ChatState!.PropertyChanged += ChatState_OnChatStateChanged;
            _subscribedState = ChatState;
            _interop ??= new AppJsInterop(JsRuntime);
            
            return base.OnParametersSetAsync();
        }
       
        public List<(string role, string? message)> GetMessageHistory()
        {
            return ChatState!.ChatMessages.Select(x => (x.Role.ToString(), x.Content)).ToList();
        }
        public List<ChatMessage> GetChatHistory()
        {
            return ChatState!.ChatHistory;
        }
        private void ChatState_OnChatStateChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (!_disposed && args.PropertyName == nameof(ChatState.ChatMessages))
            {
                _ = InvokeAsync(async () =>
                {
                    if (_disposed) return;
                    ChatStateCollection.ChatStates[ViewId] = ChatState;
                    StateHasChanged();
                    await _interop!.ScrollDown(_chatColumn);
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            if (_subscribedState is not null) _subscribedState.PropertyChanged -= ChatState_OnChatStateChanged;
            if (ResetOnClose) ChatState!.Reset();
            if (_generatedViewId is not null) ChatStateCollection.ChatStates.Remove(_generatedViewId);
            if (_interop is not null) await _interop.DisposeAsync();
        }
    }
}
