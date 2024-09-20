using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Radzen.Blazor;
using SemanticKernelAgentOrchestration.Extensions;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Agents;
using SkPluginLibrary.Agents.Models;
using static SkPluginLibrary.CoreKernelService;

namespace BlazorWithSematicKernel.Components.AgentComponents;

public partial class AgentBuilder
{
    [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    [Inject]
    private NotificationService NotificationService { get; set; } = default!;
    [Inject]
    private TooltipService TooltipService { get; set; } = default!;
    private Dictionary<PluginType, List<KernelPlugin>> _allPluginTypes = [];
    private List<PluginData> _allPlugins = [];
    private IEnumerable<PluginData> _selectedPlugins = [];
    private RadzenDropDownDataGrid<IEnumerable<PluginData>> _pluginGrid;
    private RadzenDataGrid<AgentProxy>? _agentGrid;
    [Parameter]
    public string StopText { get; set; } = "[STOP]";
    //private List<AgentProxy> _generatedAgents = [];
    [Parameter]
    public EventCallback<List<AgentProxy>> AgentsGeneratedChanged { get; set; }
    [Parameter]
    public EventCallback<AgentGroupCompletedArgs> AgentsCompleted { get; set; }
    [Parameter]
    public List<AgentProxy> AgentsGenerated { get; set; } = [];
    [Parameter]
    public List<AgentProxy> AgentsAsPlugins { get; set; } = [];
    //[Parameter]
    //public EventCallback<AgentGroupCompletedArgs> AgentGroupCompleted { get; set; }
    protected override Task OnParametersSetAsync()
    {
        _agentGroupForm.StopStatement = StopText;
        return base.OnParametersSetAsync();
    }

    private class PluginData(PluginType pluginType, KernelPlugin kernelPlugin)
    {
        public PluginType PluginType { get; set; } = pluginType;
        public KernelPlugin KernelPlugin { get; set; } = kernelPlugin;
        public bool IsSelected { get; set; }
        public string Name => KernelPlugin.Name;
    }
    private async Task AllPlugins()
    {
        _allPluginTypes = await CoreKernelService.GetAllPlugins();
        if (AgentsAsPlugins.Count > 0)
        {
            var kernel = CreateKernel();
            var plugins = new List<KernelPlugin>();
            foreach (var agent in AgentsAsPlugins)
            {
                var interactive = new InteractiveAgent(agent, kernel.Clone());
                var plugin = interactive.AsPlugin();
                plugins.Add(plugin);
            }
            _allPluginTypes[PluginType.Agent] = plugins;
        }
        _allPlugins = _allPluginTypes.SelectMany(x => x.Value.Select(y => new PluginData(x.Key, y))).ToList();

    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AllPlugins();
            StateHasChanged();
        }
        await base.OnAfterRenderAsync(firstRender);
    }
    private class AgentForm
    {
        public string? Name { get; set; }
        public string Description { get; set; } = "";
        public string Instructions { get; set; } = "";
        public IEnumerable<PluginData> Plugins { get; set; } = [];
        public bool IsPrimary { get; set; }
        public string? Model { get; set; } = "Gpt4";
        public bool IsUserProxy { get; set; }

    }
    private class AgentGroupForm
    {
        public GroupTransitionType GroupTransitionType { get; set; }
        public string StopStatement { get; set; } = "[STOP]";
        public int Rounds { get; set; } = 10;
    }
    private async void ConfirmUserProxy(bool isChecked)
    {
        if (isChecked)
        {
            var confirmed = await DialogService.Confirm("Setting this agent as a User Proxy will cause all interaction to this agent to pass through to you and the group will wait for your reply.", "Are you sure you want make this a User Proxy agent?");
            if (confirmed == true)
            {
                _agentForm.IsUserProxy = true;
            }
        }
        else
        {
            _agentForm.IsUserProxy = false;
        }
        StateHasChanged();
    }
    private List<GroupTransitionType> _transitionTypes = Enum.GetValues<GroupTransitionType>().ToList();
    private AgentGroupForm _agentGroupForm = new();
    private List<string> _models = ["Gpt35", "Gpt4"];
    private AgentForm _agentForm = new();

    private async void GenerateAgent(AgentForm agentForm)
    {
        Console.WriteLine($"Generating Agent with {agentForm.Plugins.Count()} plugins");
        var proxy = new AgentProxy
        {
            Description = agentForm.Description,
            Instructions = agentForm.Instructions,
            Name = agentForm.Name,
            Plugins = agentForm.Plugins.Select(x => x.KernelPlugin).ToList(),
            IsPrimary = agentForm.IsPrimary,
            IsUserProxy = agentForm.IsUserProxy,
            GptModel = agentForm.Model
        };
        //Console.WriteLine($"Agent Generated:\n {proxy.AsJson()}");
        AgentsGenerated.Add(proxy);
        await AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        if (_agentGrid is not null)
            await _agentGrid.Reload();
        _agentForm = new AgentForm();
        StateHasChanged();
    }
    private async Task UpdateAgent(AgentProxy agentProxy)
    {
        _agentForm = new AgentForm { Name = agentProxy.Name, Description = agentProxy.Description, Instructions = agentProxy.Instructions, Plugins = agentProxy.Plugins.Select(x => new PluginData(PluginType.Prompt, x)), IsUserProxy = agentProxy.IsUserProxy, Model = agentProxy.GptModel };
        AgentsGenerated.Remove(agentProxy);
        await AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        StateHasChanged();
        if (_agentGrid is not null)
            await _agentGrid.Reload();
    }
    private void DeleteAgent(AgentProxy agent)
    {
        AgentsGenerated.Remove(agent);
        AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        StateHasChanged();
    }
    private void MakePrimary(AgentProxy agent)
    {
        foreach (var agentProxy in AgentsGenerated)
        {
            if (agentProxy.Name == agent.Name)
            {
                agentProxy.IsPrimary = true;
                continue;
            }
            agentProxy.IsPrimary = false;
        }

        AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        StateHasChanged();
    }
   
    private void ShowFunctions(KernelPlugin kernelFunctions)
    {
        var paramters = new Dictionary<string, object> { { "Plugin", kernelFunctions } };
        var dialogOptions = new DialogOptions { Draggable = true, ShowClose = true, Style = "width:40vw; height:max-content" };
        DialogService.Open<ViewFunctions>($"Plugin {kernelFunctions.Name} - Functions", paramters, dialogOptions);
    }
    private void Finish(AgentGroupForm agentGroupForm)
    {
        if (!AgentsGenerated.Any(x => x.IsPrimary))
        {
            NotificationService.Notify(NotificationSeverity.Error, "Select Primary Agent", "Agent Interaction requires a primary agent. Select one!");
            return;
        }
        AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        AgentsCompleted.InvokeAsync(new AgentGroupCompletedArgs { TransitionType = agentGroupForm.GroupTransitionType, Agents = AgentsGenerated, StopStatement = agentGroupForm.StopStatement, Rounds = agentGroupForm.Rounds});
    }
    private void AddSubAgent(AgentProxy agentForm)
    {

        StateHasChanged();
    }
    
}

public class AgentGroupCompletedArgs
{
    public List<AgentProxy> Agents { get; set; } = [];
    public GroupTransitionType TransitionType { get; set; }
    public string StopStatement { get; set; } = "approve";
    public int Rounds { get; set; } = 10;
}