using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Agents.Models;

namespace BlazorWithSematicKernel.Components.AgentComponents
{
    public partial class CreateAgentForm : ComponentBase
    {
        [Parameter]
        public AgentProxy? Agent { get; set; }
        [Parameter]
        public EventCallback<AgentProxy> AgentChanged { get; set; }
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject]
        private DialogService DialogService { get; set; } = default!;
        private List<PluginData> _allPlugins = [];

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await AllPlugins();
            }
            Console.WriteLine($"Plugin Count: {_allPlugins.Count}");
            await base.OnAfterRenderAsync(firstRender);
        }
        private class AgentForm
        {
            public string? Name { get; set; }
            public string Description { get; set; } = "";
            public string Instructions { get; set; } = "";
            public IEnumerable<PluginData> Plugins { get; set; } = [];

        }
        private AgentForm _agentForm = new();
        private class PluginData(PluginType pluginType, KernelPlugin kernelPlugin)
        {
            public PluginType PluginType { get; set; } = pluginType;
            public KernelPlugin KernelPlugin { get; set; } = kernelPlugin;
            public string Name => KernelPlugin.Name;
           
        }
        private async Task AllPlugins()
        {
            var allPluginTypes = await CoreKernelService.GetAllPlugins();
            _allPlugins = allPluginTypes.SelectMany(x => x.Value.Select(y => new PluginData(x.Key, y))).ToList();

        }
        private async void GenerateAgent(AgentForm agentForm)
        {
            Console.WriteLine($"Generating Agent with {agentForm.Plugins.Count()} plugins");
            var proxy = new AgentProxy
            {
                Description = agentForm.Description,
                Instructions = agentForm.Instructions,
                Name = agentForm.Name,
                Plugins = agentForm.Plugins.Select(x => x.KernelPlugin).ToList()
            };
            Agent = proxy;
            await AgentChanged.InvokeAsync(Agent);
            //Console.WriteLine($"Agent Generated:\n {proxy.AsJson()}");
           
            StateHasChanged();
        }
        private void ShowFunctions(KernelPlugin kernelFunctions)
        {
            var paramters = new Dictionary<string, object> { { "Plugin", kernelFunctions } };
            var dialogOptions = new DialogOptions { Draggable = true, ShowClose = true, Style="width:40vw; height:max-content" };
            DialogService.Open<ViewFunctions>($"Plugin {kernelFunctions.Name} - Functions", paramters, dialogOptions);
        }
    }
}
