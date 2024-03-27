using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel.Experimental.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using AngleSharp.Browser.Dom;
using SkPluginLibrary.Agents.Examples;
using SkPluginLibrary.Agents.Models;

namespace BlazorWithSematicKernel.Pages;

public partial class SciFiAgentRunner
{
    [Inject]
    private AdventureStoryAgents CommanderAstraAgent { get; set; } = default!;
    private ExternalServiceAgent _externalServiceAgent = new();
    private List<AgentChatMessage> _agentMessages = [];
    private bool _isBusy;
    private bool _isRunning;
    private CancellationTokenSource _cancellationTokenSource = new();
    private ChatHistory _chatHistory = [];
    private ChatView _chatView = default!;
    protected override Task OnInitializedAsync()
    {
        
        CommanderAstraAgent.ChatMessage += ChatMessage;
        return base.OnInitializedAsync();
    }
    private void ChatMessage(AgentChatMessage message)
    {
        Console.WriteLine($"{message.Name} says {message.Content}");
        _agentMessages.Add(message);
        StateHasChanged();
    }
    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
        _isBusy = false;
        StateHasChanged();
    }
    private Task StartChat()
    {
        _isRunning = true;
        return ExecuteChatStream("Set the stage for the adventure and begin the interactive adventure");
        //return ExecuteChatStream("Help me!");
    }
    private async Task Reset()
    {
        _chatView.ChatState.Reset();
        await CommanderAstraAgent.RestartAsync();
        _isRunning = false;
        StateHasChanged();
    }
    private async void HandleUserInput(UserInputRequest requestInput)
    {
        var input = requestInput.ChatInput;
        _isBusy = true;
        _chatView.ChatState.AddUserMessage(input);
        await ExecuteChatStream(input);
        var messages = _chatView.GetMessageHistory().Select(x => $"{x.role}:\n{x.message}");
    }

    private async Task ExecuteChatStream(string input)
    {
        var hasStarted = false;
        var astraChat = CommanderAstraAgent.ExecuteChatSequence(input, _cancellationTokenSource.Token);
        var taxChat = _externalServiceAgent.ChatStream(input, _cancellationTokenSource.Token);
        await foreach (var response in astraChat)
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
    }

    private async Task RunAgent()
    {
        _agentMessages.Clear();
        _isBusy = true;
        StateHasChanged();
        await CommanderAstraAgent.ExecuteRun(cancellationToken:_cancellationTokenSource.Token);
        _isBusy = false;
        StateHasChanged();
    }
}