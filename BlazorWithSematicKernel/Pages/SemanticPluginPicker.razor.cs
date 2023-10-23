using BlazorWithSematicKernel.Components;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class SemanticPluginPicker : ComponentBase
    {
        public List<string?> Plugins => Directory.GetDirectories(RepoFiles.PluginDirectoryPath).Select(Path.GetFileName).ToList();
        private List<ChatGptPlugin> ChatGptPlugins = new();
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;
        [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        private List<PluginFunctions> _allPlugins = new();
        private int _stepIndex;
        protected override async Task OnInitializedAsync()
        {
            _allPlugins = await CoreKernelService.GetAllPlugins();
            ChatGptPlugins = ChatGptPlugin.AllChatGptPlugins;
            await base.OnInitializedAsync();
        }

        private Dictionary<string, ISKFunction> _selectedFunctions = new();
        private ChatGptPluginManifest _selectedManifest = new();
        private bool _isBusy;

        private void SelectPlugin(PluginFunctions pluginFunctions)
        {
            _selectedFunctions = pluginFunctions.SkFunctions;
            _stepIndex = 2;
            StateHasChanged();
        }
        private async Task ShowManifest(ChatGptPlugin chatGptPlugin)
        {
            try
            {
                _selectedManifest = await CoreKernelService.GetManifest(chatGptPlugin);
                DialogService.Open<ManifestDisplay>("Manifest",
                    new Dictionary<string, object> {{"ChatGptPluginManifest", _selectedManifest}},
                    new DialogOptions {Height = "max-content", Width = "60vw", Resizable = true, Draggable = true, CloseDialogOnEsc = true, CloseDialogOnOverlayClick = true});
                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error Retrieving Manifest", ex.Message);
            }
        }

        private async Task SelectChatGptPlugin(ChatGptPlugin chatGptPlugin)
        {
            try
            {
                var manifest = await CoreKernelService.GetManifest(chatGptPlugin);
                if (!string.IsNullOrEmpty(chatGptPlugin.OverrideUrl))
                    manifest.OverrideUrl = chatGptPlugin.OverrideUrl;
                _selectedFunctions = await CoreKernelService.GetExternalPluginFunctions(manifest);
                _stepIndex = 2;
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error Retrieving Functions", ex.Message);
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
