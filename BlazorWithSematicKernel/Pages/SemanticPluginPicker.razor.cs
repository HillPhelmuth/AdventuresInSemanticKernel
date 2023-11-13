using BlazorWithSematicKernel.Components;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models;

namespace BlazorWithSematicKernel.Pages
{
    public partial class SemanticPluginPicker : ComponentBase
    {
        public List<string?> Plugins => Directory.GetDirectories(RepoFiles.PluginDirectoryPath).Select(Path.GetFileName).ToList();
        private List<ChatGptPluginManifest> ChatGptPluginsManifests = new();
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;
        [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        private List<PluginFunctions> _allPlugins = new();
        private int _stepIndex;
        protected override async Task OnInitializedAsync()
        {
            _allPlugins = await CoreKernelService.GetAllPlugins();
            ChatGptPluginsManifests = ChatGptPluginManifest.GetAllPluginManifests().Where(x => x.Auth.TypeEnum == TypeEnum.None).ToList();
            await ChatGptPluginManifest.GetAndSaveAllNonAuthMantifestFiles();
            await base.OnInitializedAsync();
        }

        private Dictionary<string, ISKFunction> _selectedFunctions = new();
        private bool _isBusy;

        private void SelectPlugin(PluginFunctions pluginFunctions)
        {
            _selectedFunctions = pluginFunctions.SkFunctions;
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
        private void HandleSelectFunction(KeyValuePair<string, ISKFunction> function)
        {
            _selectedFunction = new Function(function.Key, function.Value);
            _stepIndex = 3;
            StateHasChanged();
        }

    }
}
