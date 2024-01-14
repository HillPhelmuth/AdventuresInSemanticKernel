using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Agents;
using SkPluginLibrary.Agents.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWithSematicKernel.Pages
{
    public partial class SimpleAgentBuilder : ComponentBase
    {
        [Inject]
        private AssistantAgentService AgentRunnerService { get; set; } = default!;
       
        private enum Section { Intro, Build, Execute }
        private class SectionSelector
        {
            public Section ActiveSection { get; set; }
            public bool SectionSelected(Section section)
            {
                return ActiveSection == section;
            }
        }
        private SectionSelector _sectionSelector = new();
        private void GetStarted()
        {
            _sectionSelector.ActiveSection = Section.Build;
            StateHasChanged();
        }
        private AgentProxy? _agentProxy;
        private bool _isBusy;
        private ChatView? _chatView;
        private void HandleAgentProxy(AgentProxy agentProxy)
        {
            _agentProxy = agentProxy;
            _sectionSelector.ActiveSection = Section.Execute;
            StateHasChanged();
        }
        private CancellationTokenSource _cancellationTokenSource = new();
        private void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
        private void Reset()
        {
            _chatView.ChatState.Reset();
            StateHasChanged();
        }
        private async void HandleUserInput(UserInputRequest userInputRequest)
        {
            var input = userInputRequest.ChatInput!;
            _chatView!.ChatState.AddUserMessage(input);
            await ExecuteChatStream(input);
        }
        private async Task ExecuteChatStream(string input)
        {
            var hasStarted = false;
            await foreach (var response in AgentRunnerService.ExecuteSimpleAgentChatStream(input, _agentProxy!, _cancellationTokenSource.Token))
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView!.ChatState!.AddAssistantMessage(response);
                    _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
                    continue;
                }

                _chatView!.ChatState!.UpdateAssistantMessage(response);
            }

            var lastAsstMessage = _chatView!.ChatState!.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
            _isBusy = false;
            StateHasChanged();
        }
    }
}
