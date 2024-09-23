using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SkPluginLibrary.Agents.Models.Events;
using SkPluginLibrary.Agents.SkAgents;
using SemanticKernelAgentOrchestration.Models;
using BlazorWithSematicKernel.Components.AgentComponents;
using SkPluginLibrary.Models.Helpers;
using Microsoft.JSInterop;
using SkPluginLibrary.Models.JsonConverters;

namespace BlazorWithSematicKernel.Pages;

public partial class AgentGroupChatPage : ComponentBase
{
	[Inject]
	private ICustomNativePlugins CoreKernelService { get; set; } = default!;
    [Inject] 
    private ICoreKernelExecution CorePluginService { get; set; } = default!;
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    [Inject]
    private NotificationService NotificationService { get; set; } = default!;
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    private ChatCompletionGroup? _chatCompletionGroup;

    private bool _isBusy;
	private string _output = "";
    private CancellationTokenSource _cancellationTokenSource = new();
	private UserProxySkAgent? _requestingAgent;
    private bool _isUserInputRequested;
    private ChatView? _chatView;
    private string Css => _isUserInputRequested ? "blinking-input" : "";
    private string? _summary;
    private List<AgentProxy> _agentProxies = [];
    private List<KernelPlugin> _allKernelPlugins = [];
   
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _allKernelPlugins = (await CorePluginService.GetAllPlugins()).SelectMany(x => x.Value).ToList();

        }
        await base.OnAfterRenderAsync(firstRender);
    }
    private int _step = 0;
    private void GetStarted()
    {
        Console.WriteLine("Get Started");
        _step = 1;
        StateHasChanged();

    }
    private async void OnAgentInputRequest(object? sender, SkAgentInputRequestArgs args)
    {
        Console.WriteLine("User input requested");
        _requestingAgent = args.Agent;
        _isUserInputRequested = true;
        _isBusy = false;
        StateHasChanged();
    }
    private void Reset()
    {
        _chatView.ChatState.Reset();
        StateHasChanged();
    }
    private async void HandleMessage(string message)
    {
        _chatView?.ChatState?.UpsertAssistantMessage(message);
    }

    private void HandleAgentProxies(AgentGroupCompletedArgs agentGroupCompleted)
    {
        _chatCompletionGroup ??= new ChatCompletionGroup(agentGroupCompleted.Agents, agentGroupCompleted.TransitionType, agentGroupCompleted.Rounds, agentGroupCompleted.StopStatement);
        _chatCompletionGroup.AgentInputRequest += OnAgentInputRequest;
        _chatCompletionGroup.MessageSent += HandleMessage;
        _step = 2;
        StateHasChanged();
    }
    private void UseExample(string agentsexampleSkJson = "AgentsExample-sk.json")
    {
        _agentProxies = FileHelper.ExtractFromAssembly<List<AgentProxy>>(agentsexampleSkJson);
        foreach (var agent in _agentProxies.Where(agent => agent.PluginNames.Count > 0))
        {
            Console.WriteLine($"Agent {agent.Name} has {agent.PluginNames.Count} Plugins");
            var pluginNames = agent.PluginNames;
            agent.Plugins.AddRange(_allKernelPlugins.Where(x => pluginNames.Contains(x.Name)));
        }
        StateHasChanged();

    }
    private class FileUploadData
    {
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? FileBase64 { get; set; }
        public const int MaxFileSize = int.MaxValue;

    }

    private FileUploadData _fileUploadData = new();


    private Task HandleFile(FileUploadData fileUploadData)
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
        return Task.CompletedTask;
    }
    private void HandleError(UploadErrorEventArgs args)
    {
        NotificationService.Notify(NotificationSeverity.Error, "Upload Error", args.Message);
        Console.WriteLine($"Error: {args.Message}\n");
    }
    private async Task DownloadToFile()
    {
        var agentsJson = JsonSerializer.Serialize(_agentProxies, JsonSerializerOptions);
        if (string.IsNullOrEmpty(agentsJson)) return;
        var fileContent = FileHelper.GenerateTextFile(agentsJson);
        var groupFileName = _agentProxies.Find(a => a.IsPrimary)?.Name ?? "UnknownGroup";
        await JsRuntime.InvokeVoidAsync("downloadFile", $"{groupFileName}.json", fileContent);
    }
    private async void HandleUserInput(UserInputRequest userInputRequest)
    {
        var input = userInputRequest.ChatInput;
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        _chatView!.ChatState.AddUserMessage(input!);
        if (_requestingAgent is not null)
        {
            _requestingAgent.ProvideInput(input!);
            _isUserInputRequested = false;
            StateHasChanged();
            return;
        }

        await _chatCompletionGroup.ExecuteCustomAgentGroup(input);
    }
    private void ShowSummary()
    {
        var options = new DialogOptions { Width = "90vw", Height = "90vh", CloseDialogOnOverlayClick = true, ShowTitle = true, Draggable = true, Resizable = true };
        DialogService.Open<ConvoSummary>("Conversation Summary", new Dictionary<string, object> { { "ConversationSummary", _summary } }, options);
    }
    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
        _isBusy = false;
        StateHasChanged();
    }
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
    private async Task GenerateWebContext()
	{
		_isBusy = true;
		StateHasChanged();
		await Task.Delay(1);
		var inputs = JsonSerializer.Deserialize<List<QnAInput>>(await File.ReadAllTextAsync("qnaInputs.json"));
		var evalInputs = CoreKernelService.GenerateEvalInputsFromWeb(inputs);
		await foreach (var eval in evalInputs)
		{
			_output += $"{JsonSerializer.Serialize(eval)}\n";
			StateHasChanged();
			await Task.Delay(1);
		}
		_isBusy = false;
		StateHasChanged();
		await File.WriteAllTextAsync("evalInputs.json", _output);
	}
}