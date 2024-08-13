using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel.ChatCompletion;
using SkPluginLibrary.Plugins;

namespace BlazorWithSematicKernel.Pages
{
    public partial class PromptEngineerAgent : ComponentBase
    {
        [Inject]
        private ICustomNativePlugins CoreKernelService { get; set; } = default!;
        [Inject]
        private ICoreKernelExecution CoreKernelExecution { get; set; } = default!;
       
        private ChatView? _chatView;
        private bool _isBusy;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _isBusy = true;
                StateHasChanged();
                await Task.Delay(1);
               
                var history = new ChatHistory();
                history.AddUserMessage("Introduce yourself and describe your purpose and the tools you have available to you.");
                
                var runPromptEngineerAgent = CoreKernelService.RunPromptEngineerAgent(history);
                await ExecuteChatSequence(runPromptEngineerAgent);
                _isBusy = false;
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async void Submit(string input)
        {
            CoreKernelService.AdditionalAgentText += HandleAdditionalText;
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            _chatView.ChatState.AddUserMessage(input);

            
            var runPromptEngineerAgent = CoreKernelService.RunPromptEngineerAgent(_chatView.ChatState.ChatHistory);
            await ExecuteChatSequence(runPromptEngineerAgent);
            //var hasStarted = false;
            //await foreach (var response in runPromptEngineerAgent)
            //{
            //    if (!hasStarted)
            //    {
            //        hasStarted = true;
            //        _chatView.ChatState.AddAssistantMessage(response);
            //        _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
            //        continue;
            //    }

            //    _chatView.ChatState.UpdateAssistantMessage(response);
            //    StateHasChanged();
            //}
            //var lastAsstMessage = _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            //if (lastAsstMessage is not null)
            //    lastAsstMessage.IsActiveStreaming = false;
            _isBusy = false;
            StateHasChanged();
            CoreKernelService.AdditionalAgentText -= HandleAdditionalText;
        }
        private void HandleAdditionalText(string text)
        {
            if (text.StartsWith("Executing func")) return;
            if (_chatView!.ChatState.ChatMessages.LastOrDefault().Role == Role.Assistant)
            {
                _chatView!.ChatState.UpdateAssistantMessage(text);
            }
            else
                _chatView!.ChatState.AddAssistantMessage(text);
        }
        private async Task ExecuteChatSequence(IAsyncEnumerable<string> tokenUpdates)
        {
            var hasStarted = false;
            await foreach (var response in tokenUpdates)
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView!.ChatState.AddAssistantMessage(response,
                        _chatView!.ChatState.ChatMessages.Count + 1);
                    _chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!
                        .IsActiveStreaming = true;
                    continue;
                }

                _chatView!.ChatState.UpdateAssistantMessage(response);
            }

            var lastAsstMessage =
                _chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
        }
    }
}
