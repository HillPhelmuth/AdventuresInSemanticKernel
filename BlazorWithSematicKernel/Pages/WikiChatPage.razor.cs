using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class WikiChatPage : ComponentBase
    {
        [Inject]
        private ICustomNativePlugins CoreKernelExecution { get; set; } = default!;
        
        private ChatView? _chatView;
        private bool _isBusy;
        
        private async void Submit(string input)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            _chatView.ChatState.AddUserMessage(input);
            CoreKernelExecution.AdditionalAgentText += HandleYieldReturn;
            var hasStarted = false;
            await foreach (var response in CoreKernelExecution.RunWikiSearchChat(input))
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView.ChatState.AddAssistantMessage(response);
                    _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
                    continue;
                }

                _chatView.ChatState.UpdateAssistantMessage(response);
                StateHasChanged();
            }
            var lastAsstMessage = _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
            _isBusy = false;
            StateHasChanged();
            CoreKernelExecution.AdditionalAgentText -= HandleYieldReturn;
        }
        private void HandleYieldReturn(string text)
        {
            if (text.StartsWith("Executing func")) return;
            if (_chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role != Role.User)?
                    .IsActiveStreaming == true)
            {
                _chatView!.ChatState.UpdateAssistantMessage(text);
            }
            else
                _chatView!.ChatState.AddAssistantMessage(text);
        }

    }
}
