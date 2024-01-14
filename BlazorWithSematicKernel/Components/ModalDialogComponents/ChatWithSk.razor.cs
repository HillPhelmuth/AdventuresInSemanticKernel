using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents
{
    public partial class ChatWithSk : ComponentBase
    {
        [Inject] private IChatWithSk ChatWithSkDocs { get; set; } = default!;
        private ChatView? _chatView;
        private bool _isBusy;
        private string? _history;
        private CancellationTokenSource _cancellationTokenSource = new();
        private void ClearChat()
        {
            _chatView?.ChatState.Reset();
            StateHasChanged();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await InitiateChat();

            }
            await base.OnAfterRenderAsync(firstRender);
        }
        private async Task InitiateChat()
        {
            _isBusy = true;
            _chatView!.ChatState?.Reset();
            StateHasChanged();
            await Task.Delay(1);
            var token = _cancellationTokenSource.Token;
            var hasStarted = false;
            await foreach (var response in ChatWithSkDocs.ExecuteChatWithSkStream("Quickly Introduce yourself and Semantic Kernel", _history, token))
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView.ChatState!.AddAssistantMessage(response);
                    _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
                    continue;
                }

                _chatView.ChatState?.UpdateAssistantMessage(response);
            }

            var lastAsstMessage = _chatView.ChatState?.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
            _isBusy = false;
            StateHasChanged();
            var messages = _chatView.GetMessageHistory().Select(x => $"{x.role}:\n{x.message}");
            _history = string.Join("\n", messages);

        }
        private async void HandleInput(string input)
        {
            _isBusy = true;
            _chatView.ChatState.AddUserMessage(input);
            var hasStarted = false;
            var token = _cancellationTokenSource.Token;
            await foreach (var response in ChatWithSkDocs.ExecuteChatWithSkStream(input, _history, token))
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView.ChatState.AddAssistantMessage(response);
                    _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
                    continue;
                }

                _chatView.ChatState.UpdateAssistantMessage(response);
            }

            var lastAsstMessage = _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
            _isBusy = false;
            StateHasChanged();
            var messages = _chatView.GetMessageHistory().Select(x => $"{x.role}:\n{x.message}");
            _history = string.Join("\n", messages);
        }
    }
}
