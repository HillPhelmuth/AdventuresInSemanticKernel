using Microsoft.AspNetCore.Components;
using Microsoft.SemanticKernel.Agents;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components.AgentComponents
{
	public partial class CreateAgentForm : ComponentBase
	{
		private const string MadLibAgentInstructions = """
                                                       Use the tools available to generate a madlib story and then help the user complete the mad lib by asking for one word at a time.
                                                       DO NOT TRY TO COMPLETE THE MAD LIB YOURSELF.
                                                       use the responses from the user to complete the mad lib. When the mad lib is complete, provide the full story to the user.
                                                       Take a deep breath and think step by step.
                                                       """;

		[Parameter]
		public AgentProxy? Agent { get; set; }
		[Parameter]
		public EventCallback<AgentProxy> AgentChanged { get; set; }
		[Parameter]
		public List<AgentProxy> PreMadeAgents { get; set; } = [];
		[Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
		[Inject]
		private DialogService DialogService { get; set; } = default!;
		private List<PluginData> _allPlugins = [];
		protected override async Task OnParametersSetAsync()
		{
			if (Agent != null)
			{
				await AllPlugins();
				var pluginNames = Agent.Plugins.Select(x => x.Name);
				var pluginData = _allPlugins.Where(x => pluginNames.Contains(x.Name)).ToList();
				_agentForm.Name = Agent.Name;
				_agentForm.Description = Agent.Description;
				_agentForm.Instructions = Agent.Instructions;
				_agentForm.Plugins = pluginData;
			}
			await base.OnParametersSetAsync();
		}
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
			public bool Save { get; set; }

		}
		private AgentForm _agentForm = new();
		private void UseMediumExample()
		{
			_agentForm.Name = "Medium Article Helper";
			_agentForm.Description = "Help users find and learn about articles they're interested in on Medium.com";
			_agentForm.Instructions = """
                                      Search for articles on medium.com that best fit the user's request. Summarize, paraphrase or describe articles when it's requested.
                                      When you're not sure what to do, ask the user.
                                      If a task requires multiple steps, always stop between steps to describe your plan and confirm the user wishes to continue.
                                      Now, take a deep breath and use the tools available to complete each task.
                                      """;
			var mediumPlugin = _allPlugins.FirstOrDefault(x => x.Name.Equals("MediumApiPlugin", StringComparison.InvariantCultureIgnoreCase));
			var summarizePlugin = _allPlugins.FirstOrDefault(x => x.Name.Equals("SummarizePlugin", StringComparison.InvariantCultureIgnoreCase));
			_agentForm.Plugins = [mediumPlugin, summarizePlugin];
			StateHasChanged();
		}
		private void UseWebChatExample()
		{
			_agentForm.Name = "Web Search Agent";
			_agentForm.Description = "You are a web-search Q&A assistant. Find the answer to the user question on the web and then answer them. Provide links to the relevant web pages used in your answers.";
			_agentForm.Instructions = """
                                      Answer the user's query using the web search results below. 
                                      Always search the web before responding. 
                                      Always include CITATIONS in your response.
                                      Now, take a deep breath and use the tools available to complete each task.
                                      """;
			var webCrawlPlugin = _allPlugins.FirstOrDefault(x => x.Name.Equals("WebCrawlPlugin", StringComparison.InvariantCultureIgnoreCase));
			_agentForm.Plugins = [webCrawlPlugin];
			StateHasChanged();
		}
		private void UseMadLibExample(string madLibAgentInstructions = MadLibAgentInstructions, string description = "You are a Mad Libs assistant. You will help users fill in the blanks of a story.", string? agentName = "Mad Lib Agent")
		{
			_agentForm.Name = agentName;
			_agentForm.Description = description;
			_agentForm.Instructions = madLibAgentInstructions;
			var madLibPlugin = _allPlugins.Find(x => x.Name.Equals("MadLibPlugin", StringComparison.InvariantCultureIgnoreCase));
			_agentForm.Plugins = [madLibPlugin];
			StateHasChanged();
		}

		private class PluginData(PluginType pluginType, KernelPlugin kernelPlugin)
		{
			public PluginType PluginType { get; set; } = pluginType;
			public KernelPlugin KernelPlugin { get; set; } = kernelPlugin;
			public string Name => KernelPlugin.Name;

		}
		private async Task AllPlugins()
		{
			if (_allPlugins.Count > 0) return;
			var allPluginTypes = await CoreKernelService.GetAllPlugins();
			_allPlugins = allPluginTypes.SelectMany(x => x.Value.Select(y => new PluginData(x.Key, y))).ToList();
			StateHasChanged();

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
			var dialogOptions = new DialogOptions { Draggable = true, ShowClose = true, Style = "width:40vw; height:max-content" };
			DialogService.Open<ViewFunctions>($"Plugin {kernelFunctions.Name} - Functions", paramters, dialogOptions);
		}
		public void Clear()
		{
			_agentForm = new AgentForm();
			StateHasChanged();
		}
	}
}
