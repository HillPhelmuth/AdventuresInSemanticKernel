﻿using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class WebSearchPage : ComponentBase
    {
        [Inject]
        private ICustomNativePlugins CoreKernelService { get; set; } = default!;
        [Inject]
        private ICoreKernelExecution CoreKernelExecution { get; set; } = default!;
        private ChatView? _chatView;
        private bool _isBusy;

        private async void Submit(string input)
        {
            CoreKernelService.AdditionalAgentText += HandleYieldReturn;
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            _chatView.ChatState.AddUserMessage(input);

            var hasStarted = false;
            //var query = $"Answer the user's query by searching the web. Always include CITATIONS in your response.\n\nQuery: {input}";
            
            await foreach (var response in CoreKernelService.RunWebSearchChat(input))
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
            CoreKernelService.AdditionalAgentText -= HandleYieldReturn;
        }

        private void HandleYieldReturn(string text)
        {
            if (text.StartsWith("Executing func")) return;
            if (_chatView!.ChatState.ChatMessages.LastOrDefault().Role == Role.Assistant)
            {
                _chatView!.ChatState.UpdateAssistantMessage(text);
            }
            else
                _chatView!.ChatState.AddAssistantMessage(text);
        }
    }
}
