using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Agents;
using SkPluginLibrary.Agents.Models;

namespace BlazorWithSematicKernel.Components.AgentComponents;

public partial class AgentBuilder
{
    [Inject]
    private AssistantAgentService AgentBuilderService { get; set; } = default!;
    [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    [Inject]
    private NotificationService NotificationService { get; set; } = default!;
    private Dictionary<PluginType, List<KernelPlugin>> _allPluginTypes = [];
    private List<PluginData> _allPlugins = [];
    private IEnumerable<PluginData> _selectedPlugins = [];
    private RadzenDropDownDataGrid<IEnumerable<PluginData>> _pluginGrid;
    private RadzenDataGrid<AgentProxy>? _agentGrid;

    //private List<AgentProxy> _generatedAgents = [];
    [Parameter]
    public EventCallback<List<AgentProxy>> AgentsGeneratedChanged { get; set; }
    [Parameter]
    public EventCallback<List<AgentProxy>> AgentsCompleted { get; set; }
    [Parameter]
    public List<AgentProxy> AgentsGenerated { get; set; } = [];
    
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
        _allPlugins = _allPluginTypes.Where(x => x.Key != PluginType.Api).SelectMany(x => x.Value.Select(y => new PluginData(x.Key, y))).ToList();

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

    }
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
            IsPrimary = agentForm.IsPrimary
        };
        //Console.WriteLine($"Agent Generated:\n {proxy.AsJson()}");
        AgentsGenerated.Add(proxy);
        await AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        if (_agentGrid is not null)
            await _agentGrid.Reload();
        _agentForm = new AgentForm();
        StateHasChanged();
    }
    private void UpdateAgent(AgentProxy agentProxy)
    {
        _agentForm = new AgentForm { Name = agentProxy.Name, Description = agentProxy.Description, Instructions = agentProxy.Instructions, Plugins = agentProxy.Plugins.Select(x => new PluginData(PluginType.Prompt, x)) };
        AgentsGenerated.Remove(agentProxy);
        AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        StateHasChanged();
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
    private bool ValidatePrimary(bool isSecondary)
    {
        if (!isSecondary)
        {
            return !AgentsGenerated.Any(x => x.IsPrimary);
        }
        return true;
    }
    private void Finish()
    {
        if (!AgentsGenerated.Any(x => x.IsPrimary))
        {
            NotificationService.Notify(NotificationSeverity.Error, "Select Primary Agent", "Agent Interaction requires a primary agent. Select one!");
            return;
        }
        AgentsGeneratedChanged.InvokeAsync(AgentsGenerated);
        AgentsCompleted.InvokeAsync(AgentsGenerated);
    }
    private void AddSubAgent(AgentProxy agentForm)
    {

        StateHasChanged();
    }
}