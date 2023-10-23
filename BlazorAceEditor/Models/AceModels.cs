using System.Text.Json.Serialization;

namespace BlazorAceEditor.Models
{
    internal class AceModels
    {
    }
    public record ThemeModel
    {
        [JsonPropertyName("Caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("Theme")]
        public string? Theme { get; set; }

        [JsonPropertyName("IsDark")]
        public bool IsDark { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    public record ModeModel
    {
        [JsonPropertyName("DisplayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("SupportedFileTypes")]
        public string? SupportedFileTypes { get; set; }
        [JsonPropertyName("Mode")]
        public string? Mode { get; set; }
    }
}
