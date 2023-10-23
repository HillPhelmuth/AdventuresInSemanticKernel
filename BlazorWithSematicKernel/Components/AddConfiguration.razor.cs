using SkPluginLibrary.Models.Helpers;
using ConfigurationSection = SkPluginLibrary.Models.Helpers.ConfigurationSection;

namespace BlazorWithSematicKernel.Components
{
    public partial class AddConfiguration
    {
        private readonly List<ConfigurationSection> _configurationSections = ConfigurationHelper.GetConfigurationSections();
        private List<ConfigurationDisplay> ConfigurationDisplays => _configurationSections.Select(x => new ConfigurationDisplay(x)).ToList();
        public void SetConfigSection(ConfigurationSection section)
        {
            ConfigurationHelper.SetConfigurationSection(section);
        }


        private record ConfigurationDisplay(ConfigurationSection ConfigurationSection)
        {
            //public required ConfigurationSection ConfigurationSection { get; set; }

            public bool HasNoValues => ConfigurationSection.ConfigurationProperties!.All(x => string.IsNullOrEmpty(x.Value));
            public string CssClass => HasNoValues ? "config-novalues" : "";
        }
    }

}
