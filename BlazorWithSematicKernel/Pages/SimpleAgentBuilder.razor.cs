﻿using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using SemanticKernelAgentOrchestration.Models;

namespace BlazorWithSematicKernel.Pages
{
    public partial class SimpleAgentBuilder : ComponentBase
    {
        [Inject]
        private AssistantAgentService AgentRunnerService { get; set; } = default!;
        [Inject]
        private NotificationService NotificationService { get; set; } = default!;
        private int _step = 0;
        private void GetStarted()
        {
            Console.WriteLine("Get Started");
            _step = 1;
            StateHasChanged();

        }
        private AgentProxy? _agentProxy;
        private bool _isBusy;
        private ChatView? _chatView;
        private void HandleAgentProxy(AgentProxy agentProxy)
        {
            _agentProxy = agentProxy;
            _step = 2;
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
            _isBusy = true;
            StateHasChanged();
            var input = userInputRequest.ChatInput!;
            _chatView!.ChatState.AddUserMessage(input);
            try
            {
                await ExecuteChatStream(input);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error Generating Response", ex.Message, 30000);
            }
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
