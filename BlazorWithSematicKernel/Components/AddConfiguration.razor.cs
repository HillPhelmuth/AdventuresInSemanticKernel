using SkPluginLibrary.Models.Helpers;
using ConfigurationSection = SkPluginLibrary.Models.Helpers.ConfigurationSection;

namespace BlazorWithSematicKernel.Components
{
    public partial class AddConfiguration
    {
        private string? _originalKey;
        private Dictionary<string, string> _modelValues = new()
        { 
            { nameof(TestConfiguration.OpenAI.Gpt4ModelId), TestConfiguration.OpenAI.Gpt4ModelId },
            { nameof(TestConfiguration.OpenAI.Gpt35ModelId), TestConfiguration.OpenAI.Gpt35ModelId },
            { nameof(TestConfiguration.OpenAI.PlannerModelId), TestConfiguration.OpenAI.PlannerModelId}
        };
        protected override Task OnInitializedAsync()
        {
            _originalKey ??= TestConfiguration.OpenAI!.ApiKey;
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
        public void SetConfigSection(ConfigurationSection section)
        {
           
            ConfigurationHelper.SetConfigurationSection(section);
        }


        private record ConfigurationDisplay(ConfigurationSection ConfigurationSection)
        {
            private bool HasNoValues => ConfigurationSection.ConfigurationProperties!.All(x => string.IsNullOrEmpty(x.Value));
            public string CssClass => HasNoValues ? "config-novalues" : "";
        }
    }

}
