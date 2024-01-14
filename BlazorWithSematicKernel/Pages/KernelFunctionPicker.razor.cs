using BlazorWithSematicKernel.Components;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models;

namespace BlazorWithSematicKernel.Pages
{
    public partial class KernelFunctionPicker : ComponentBase
    {
        public List<string?> Plugins => Directory.GetDirectories(RepoFiles.PluginDirectoryPath).Select(Path.GetFileName).ToList();
        private List<ChatGptPluginManifest> ChatGptPluginsManifests = [];
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;
        [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        private List<PluginDisplay> _allPlugins = [];
        private int _stepIndex;
        private class PluginDisplay
        {
            public KernelPlugin Plugin { get; set; } = default!;
            public PluginType PluginType { get; set; }
        }
        private List<PluginDisplay> _pluginDisplays = [];
        protected override async Task OnInitializedAsync()
        {
            var allPlugins = await CoreKernelService.GetAllPlugins();
            foreach (var pluginItems in allPlugins.Select(plugin => plugin.Value.Select(x => new PluginDisplay { PluginType = plugin.Key, Plugin = x })))
            {
                _allPlugins.AddRange(pluginItems);
            }
            //_allPlugins = allPlugins.SelectMany(x => new PluginDisplay { PluginType = x.Key, Plugin = x.Value});
            ChatGptPluginsManifests = ChatGptPluginManifest.GetAllPluginManifests().Where(x => x.Auth.TypeEnum == TypeEnum.None).ToList();
            await ChatGptPluginManifest.GetAndSaveAllNonAuthMantifestFiles();
            await base.OnInitializedAsync();
        }

        private Dictionary<string, KernelFunction> _selectedFunctions = [];
        private bool _isBusy;

        private void SelectPlugin(PluginDisplay pluginFunctions)
        {
            _selectedFunctions = pluginFunctions.Plugin.ToDictionary(x => x.Name, x => x);
            _stepIndex = 2;
            StateHasChanged();
        }
       
        private async Task SelectChatGptPluginManifest(ChatGptPluginManifest chatGptPluginManifest, string overrideUrl = "")
        {
            try
            {
                if (!string.IsNullOrEmpty(overrideUrl))
                    chatGptPluginManifest.OverrideUrl = overrideUrl;
                _selectedFunctions = await CoreKernelService.GetExternalPluginFunctions(chatGptPluginManifest);
                _stepIndex = 2;
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error Retrieving Functions", $"{ex.Message}\n{ex.StackTrace}");
            }
            StateHasChanged();
        }
        private Function _selectedFunction;
        private void HandleSelectFunction(KeyValuePair<string, KernelFunction> function)
        {
            _selectedFunction = new Function(function.Key, function.Value);
            _stepIndex = 3;
            StateHasChanged();
        }

    }
}
