using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using SkPluginLibrary.Models.Helpers;
using ConfigurationSection = SkPluginLibrary.Models.Helpers.ConfigurationSection;

namespace BlazorWithSematicKernel.Components
{
    public partial class AddConfiguration
    {
        [Inject]
        private ProtectedLocalStorage ProtectedLocalStorage { get; set; } = default!;
        [Inject]
        private NotificationService NotificationService { get; set; } = default!;
        [Inject]
        private IConfiguration Configuration { get; set; } = default!;
        private string? _originalKey;

        protected override Task OnInitializedAsync()
        {
            _originalKey ??= Configuration["OpenAI:ApiKey"];
            return base.OnInitializedAsync();
        }
        private readonly List<ConfigurationSection> _configurationSections = ConfigurationHelper.GetConfigurationSections();
        private List<ConfigurationDisplay> ConfigurationDisplays => _configurationSections.Select(x => new ConfigurationDisplay(x)).ToList();
        private bool ValidateApiKeyChange(ConfigurationSection section)
        {
            if (section.Name == "OpenAIConfig")
            {
                return section.ConfigurationProperties?.Find(x => x.Name == "ApiKey")?.Value != _originalKey;
            }
            return true;
        }
        private async void SetConfigSection(ConfigurationSection section)
        {
           
            ConfigurationHelper.SetConfigurationSection(section);
            await ProtectedLocalStorage.SetAsync(section.Name, section.ConfigurationProperties);
            NotificationService.Notify(NotificationSeverity.Info, "Configuration Saved", $"Configuration for {section.Name} has been saved to browser local storage");
        }
        private async void LoadConfigFromLocalStorage(ConfigurationSection section)
        {
            var config = await ProtectedLocalStorage.GetAsync<IEnumerable<ConfigurationProperty>>(section.Name);
            if (config is {Success: true, Value: not null})
            {
                section.ConfigurationProperties = config.Value.ToList();
                ConfigurationHelper.SetConfigurationSection(section);
                NotificationService.Notify(NotificationSeverity.Info, "Configuration Loaded", $"Configuration for {section.Name} has been loaded from browser local storage");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Configuration Load Failed", $"Configuration for {section.Name} could not be loaded from browser local storage");
            }
            StateHasChanged();
        }


        private record ConfigurationDisplay(ConfigurationSection ConfigurationSection)
        {
            private bool HasNoValues => ConfigurationSection.ConfigurationProperties!.All(x => string.IsNullOrEmpty(x.Value));
            public string CssClass => HasNoValues ? "config-novalues" : "";
        }
    }

}
