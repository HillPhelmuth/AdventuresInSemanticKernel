using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Agents.Models;
using SkPluginLibrary.Agents.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SkPluginLibrary.Agents.Group;
using BlazorWithSematicKernel.Components.AgentComponents;
using Microsoft.JSInterop;
using SkPluginLibrary.Models.Helpers;
using SkPluginLibrary.Models.JsonConverters;


namespace BlazorWithSematicKernel.Pages;

public partial class GroupChatPage : ComponentBase
{
    private int _step = 0;
    private void GetStarted()
    {
        Console.WriteLine("Get Started");
        _step = 1;
        StateHasChanged();

    }
    private AgentProxy? _agentProxy;
    private List<AgentProxy> _agentProxies = [];
    private bool _isBusy;
    private ChatView? _chatView;
    private Kernel? _kernel;
    private GroupChat? _groupChat;
    private CreateAgentForm? _create;
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;
    private class SelectAgentForm
    {
        public AgentProxy? AdminAgent { get; set; }
        public List<AgentProxy> Agents { get; set; } = new();
        public GroupTransitionType GroupTransitionType { get; set; }
    }
    private SelectAgentForm _selectAgentForm = new();
    private void UseLast()
    {
        _agentProxies = FileHelper.ExtractFromAssembly<List<AgentProxy>>("agentsExample.json") /*JsonSerializer.Deserialize<List<AgentProxy>>(File.ReadAllText("agentsExample.json"))*/;
        StateHasChanged();
        
    }
    private void SelectAgent(SelectAgentForm selectAgentForm)
    {
#if DEBUG
        File.WriteAllText("agents.json", JsonSerializer.Serialize(_agentProxies, JsonSerializerOptions));
#endif 
        var adminProxy = selectAgentForm.AdminAgent!;
        var interactiveAgents = new List<InteractiveAgentBase>();
        foreach (var agent in _agentProxies)
        {
            if (agent.Name == adminProxy.Name) continue;
            AIModel model = agent.GptModel switch
            {
                "Gpt4" => AIModel.Gpt4,
                "Gpt35" => AIModel.Gpt35,
                _ => AIModel.Gpt4
            };
            var kernel = CoreKernelService.CreateKernel(model);
            var interactiveAgent = new InteractiveStreamingAgent(agent, kernel);
            interactiveAgents.Add(interactiveAgent);
        }
        var agents = interactiveAgents;
        var adminAgent = new InteractiveStreamingAgent(adminProxy, _kernel!);
        adminAgent.AgentStreamingResponse += HandleInteractiveStreamingResponse;
        var transitions = new List<Transition>();
        foreach (var agent in agents)
        {
            agent.AgentStreamingResponse += HandleInteractiveStreamingResponse;
            var trans = Transition.Create(adminAgent, agent);
            transitions.Add(trans);
            var transBack = Transition.Create(agent, adminAgent);
            transitions.Add(transBack);
        }
        var graph = new TransitionGraph(transitions);
        _groupChat = selectAgentForm.GroupTransitionType == GroupTransitionType.HubAndSpoke ? new GroupChat(adminAgent, agents, transitionGraph: graph) : new GroupChat(adminAgent, agents);
        _step = 1;
        StateHasChanged();
        _step = 2;
        StateHasChanged();
    }
    protected override Task OnInitializedAsync()
    {
        var aiModel = AIModel.Gpt4;
        _kernel = CoreKernelService.CreateKernel(aiModel);
        return base.OnInitializedAsync();
    }
    private async Task DownloadToFile()
    {
        var agentsJson = JsonSerializer.Serialize(_agentProxies, JsonSerializerOptions);
        if (string.IsNullOrEmpty(agentsJson)) return;
        var fileContent = FileHelper.GenerateTextFile(agentsJson);
        var groupFileName = _agentProxies.Find(a => a.IsPrimary)?.Name ?? "UnknownGroup";
        await JsRuntime.InvokeVoidAsync("downloadFile", $"{groupFileName}.json", fileContent);
    }
   
    private void HandleAgentProxies(AgentGroupCompletedArgs agentGroupCompleted)
    {
        _agentProxies = agentGroupCompleted.Agents;
        var admin = _agentProxies.Find(a => a.IsPrimary);
        _selectAgentForm.AdminAgent = admin;
        _selectAgentForm.Agents = agentGroupCompleted.Agents;
        _selectAgentForm.GroupTransitionType = agentGroupCompleted.TransitionType;
        SelectAgent(_selectAgentForm);
        StateHasChanged();
    }
    private void Reset()
    {
        _chatView.ChatState.Reset();
        StateHasChanged();
    }
    private async void HandleUserInput(UserInputRequest userInputRequest)
    {
        _cancellationTokenSource = new();
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var input = userInputRequest.ChatInput!;
        _chatView!.ChatState.AddUserMessage(input);
        var token = _cancellationTokenSource.Token;
        var history = _chatView!.ChatState.ChatHistory;
        var result = await _groupChat.CallAsync(input,30, ct: token);

        
    }
    private void HandleInteractiveAgentResponse(object? sender, AgentResponseArgs args)
    {
        _chatView.ChatState.AddAssistantMessage($"{args.AgentChatMessage.AgentName}:<br/> { args.AgentChatMessage.Content}");
    }
    private void HandleInteractiveAgentInputRequest(object? sender, AgentInputRequestEventArgs args)
    {

    }
    private void HandleInteractiveStreamingResponse(object? sender, AgentStreamingResponseArgs args)
    {
        if (args.IsStartToken)
        {
            _chatView.ChatState.AddAssistantMessage($"{args.AgentChatMessageUpdate.AgentName}:<br/> {args.AgentChatMessageUpdate.Content}");
        }
        else
        {
            _chatView.ChatState.UpdateAssistantMessage(args.AgentChatMessageUpdate.Content ?? "");
        }
    }
    private CancellationTokenSource _cancellationTokenSource = new();
    private JsonSerializerOptions? _jsonSerializerOptions;
    private JsonSerializerOptions JsonSerializerOptions
    {
        get 
        {
            var jsonSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()) { WriteIndented = true,};
            jsonSerializerOptions.Converters.Add(new TypeJsonConverter());
            _jsonSerializerOptions ??= jsonSerializerOptions;
            
            return _jsonSerializerOptions; 
        }
    }

    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }
}