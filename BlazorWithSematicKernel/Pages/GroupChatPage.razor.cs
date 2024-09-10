using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BlazorWithSematicKernel.Components.AgentComponents;
using Microsoft.JSInterop;
using SkPluginLibrary.Models.Helpers;
using SkPluginLibrary.Models.JsonConverters;
using System.Text;
using SemanticKernelAgentOrchestration.Extensions;
using SemanticKernelAgentOrchestration.Group;
using SemanticKernelAgentOrchestration.Models;
using SemanticKernelAgentOrchestration.Models.Events;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Agents.Models;


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
    private List<AgentProxy> _agentsAsPlugins = [];
    private bool _isBusy;
    private bool _isUserInputRequested;
    private ChatView? _chatView;
    private Kernel? _kernel;
    private GroupChat? _groupChat;
    private CreateAgentForm? _create;
    private ChatAgent? _requestingAgent;
    private List<KernelPlugin> _allKernelPlugins = [];
    private string Css => _isUserInputRequested ? "blinking-input" : "";
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject]
    private NotificationService NotificationService { get; set; } = default!;
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    [Inject] private ICoreKernelExecution CorePluginService { get; set; } = default!;
    private class SelectAgentForm
    {
        public AgentProxy? AdminAgent { get; set; }
        public List<AgentProxy> Agents { get; set; } = [];
        public GroupTransitionType GroupTransitionType { get; set; }
        public string StopStatement { get; set; } = "[STOP]";
        public int Rounds { get; set; }
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
        var agents = InteractiveAgents(adminProxy, _agentProxies);
        InteractiveStreamingAgent adminAgent;
        if (adminProxy.GptModel.StartsWith("gemini"))
            adminAgent = new InteractiveStreamingAgent(adminProxy, CoreKernelService.CreateKernelGoogle(adminProxy.GptModel));
        else
            adminAgent = new InteractiveStreamingAgent(adminProxy, _kernel!);
        adminAgent.AgentResponse += HandleInteractiveStreamingResponse;
        var transitions = new List<Transition>();
        foreach (var agent in agents)
        {
            agent.AgentResponse += HandleInteractiveStreamingResponse;
            var trans = Transition.Create(adminAgent, agent);
            transitions.Add(trans);
            var transBack = Transition.Create(agent, adminAgent);
            transitions.Add(transBack);
        }
        var graph = new TransitionGraph(transitions);
        _groupChat = selectAgentForm.GroupTransitionType switch
        {
            GroupTransitionType.HubAndSpoke => new GroupChat(adminAgent, agents, transitionGraph: graph,
                endStatement: selectAgentForm.StopStatement),
            GroupTransitionType.Sequential => new GroupChat(adminAgent, agents,
                endStatement: selectAgentForm.StopStatement, isRoundRobin: true),
            _ => new GroupChat(adminAgent, agents)
        };
        _step = 1;
        StateHasChanged();
        _step = 2;
        StateHasChanged();
    }

    private List<ChatAgent> InteractiveAgents(AgentProxy adminProxy, List<AgentProxy> agentProxies)
    {
	    var interactiveAgents = new List<ChatAgent>();
	    foreach (var agent in agentProxies)
	    {
		    if (agent.Name == adminProxy.Name) continue;
		    var model = agent.GptModel switch
		    {
			    "Gpt4" => AIModel.Gpt4O,
			    "Gpt35" => AIModel.Gpt4OMini,
			    "gemini-1.0-pro" => AIModel.Gemini10,
			    "gemini-1.5-pro-latest" => AIModel.Gemini15,
			    _ => AIModel.Gpt4Turbo
		    };
		    Kernel kernel;
            if (model is not (AIModel.Gpt4OMini or AIModel.Gpt4Turbo or AIModel.Gpt4O))
            {
                kernel = CoreKernelService.CreateKernelGoogle();
            }
            else
                kernel = CoreKernelService.CreateKernel(model);

            if (agent.IsUserProxy)
		    {
			    var user = new UserProxyAgent(agent, kernel);
			    user.AgentInputRequest += HandleInteractiveAgentInputRequest;
			    interactiveAgents.Add(user);
		    }
		    else
		    {
			    var interactiveAgent = new InteractiveStreamingAgent(agent, kernel);
			    interactiveAgents.Add(interactiveAgent);
		    }
	    }
	    return interactiveAgents;
	    
    }

    protected override Task OnInitializedAsync()
    {
        var aiModel = AIModel.Gpt4O;
        _kernel = CoreKernelService.CreateKernel(aiModel);
        _agentsAsPlugins = FileHelper.ExtractFromAssembly<List<AgentProxy>>("agentsExample.json");
        return base.OnInitializedAsync();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _allKernelPlugins = (await CorePluginService.GetAllPlugins()).SelectMany(x => x.Value).ToList();

        }
        await base.OnAfterRenderAsync(firstRender);
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
        _selectAgentForm.Rounds = agentGroupCompleted.Rounds;
        _selectAgentForm.StopStatement = agentGroupCompleted.StopStatement;
        SelectAgent(_selectAgentForm);
        StateHasChanged();
    }
    private void Reset()
    {
        _chatView.ChatState.Reset();
        StateHasChanged();
    }
    private string? _summary;
    private async void HandleUserInput(UserInputRequest userInputRequest)
    {

        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var input = userInputRequest.ChatInput!;
        _chatView!.ChatState.AddUserMessage(input);

        var history = _chatView!.ChatState.ChatHistory;
        if (_isUserInputRequested)
        {
            _requestingAgent!.ProvideInput(input);
            _isUserInputRequested = false;
            _isBusy = true;
            StateHasChanged();
            return;
        }
        _cancellationTokenSource = new();
        _summary = null;
        var token = _cancellationTokenSource.Token;
        var result = await _groupChat.CallAsync(input, _selectAgentForm.Rounds, ct: token);
        var sb = new StringBuilder();
        foreach (var message in result)
        {
            sb.AppendLine(message.FormatMessage());
        }
        _summary = sb.ToString();

    }
    //private void HandleInteractiveAgentResponse(object? sender, AgentResponseArgs args)
    //{
    //    _chatView.ChatState.AddAssistantMessage($"{args.AgentChatMessage.AuthorName}:<br/> {args.AgentChatMessage.Content}");
    //}
    private async void HandleInteractiveAgentInputRequest(object? sender, AgentInputRequestEventArgs args)
    {
        _isBusy = false;
        _isUserInputRequested = true;
        _requestingAgent = args.Agent;
        StateHasChanged();
        await Task.Delay(1);
    }
    private void HandleInteractiveStreamingResponse(object? sender, AgentResponseArgs args)
    {
        if (args.IsStartToken)
        {
            _chatView.ChatState.AddAssistantMessage($"{args.AgentChatMessageUpdate.AuthorName}:<br/> {args.AgentChatMessageUpdate.Content}");
        }
        else
        {
            _chatView.ChatState.UpdateAssistantMessage(args.AgentChatMessageUpdate.Content ?? "");
        }
    }
    private class FileUploadData
    {
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? FileBase64 { get; set; }
        public const int MaxFileSize = int.MaxValue;

    }

    private FileUploadData _fileUploadData = new();


    private async Task HandleFile(FileUploadData fileUploadData)
    {
        try
        {

            var file = Convert.FromBase64String(fileUploadData.FileBase64!.ExtractBase64FromDataUrl());
            _agentProxies = FileHelper.DeserializeFromBytes<List<AgentProxy>>(file);
            foreach (var agent in _agentProxies.Where(agent => agent.PluginNames.Count > 0))
            {
                Console.WriteLine($"Agent {agent.Name} has {agent.PluginNames.Count} Plugins");
                var pluginNames = agent.PluginNames;
                agent.Plugins.AddRange(_allKernelPlugins.Where(x => pluginNames.Contains(x.Name)));
            }
            _fileUploadData = new FileUploadData();
            _isBusy = false;
        }
        catch (Exception e)
        {
            NotificationService.Notify(NotificationSeverity.Error, "File Format Error", e.Message);
            Console.WriteLine($"Error: {e.Message}\n");
        }
        StateHasChanged();
    }
    private void HandleError(UploadErrorEventArgs args)
    {
        NotificationService.Notify(NotificationSeverity.Error, "Upload Error", args.Message);
        Console.WriteLine($"Error: {args.Message}\n");
    }
    private void ShowSummary()
    {
        var options = new DialogOptions { Width = "90vw", Height = "90vh", CloseDialogOnOverlayClick = true, ShowTitle = true, Draggable = true, Resizable = true };
        DialogService.Open<ConvoSummary>("Conversation Summary", new Dictionary<string, object> { { "ConversationSummary", _summary } }, options);
    }
    private CancellationTokenSource _cancellationTokenSource = new();
    private JsonSerializerOptions? _jsonSerializerOptions;
    private JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            var jsonSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()) { WriteIndented = true, };
            jsonSerializerOptions.Converters.Add(new TypeJsonConverter());
            _jsonSerializerOptions ??= jsonSerializerOptions;

            return _jsonSerializerOptions;
        }
    }

    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
        _isBusy = false;
        StateHasChanged();
    }
}