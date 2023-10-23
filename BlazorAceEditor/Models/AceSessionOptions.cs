using System.Text.Json.Serialization;

namespace BlazorAceEditor.Models
{
    public class AceSessionOptions
    {
        private string? _mode;

        [JsonPropertyName("firstLineNumber")]
        public int? FirstLineNumber { get; set; }

        [JsonPropertyName("overwrite")]
        public bool? Overwrite { get; set; }

        [JsonPropertyName("newLineMode")]
        public string? NewLineMode => Enum.GetName(NewLineModeOption)?.ToLower();
        [JsonIgnore]
        public NewLineModeOption NewLineModeOption { get; set; }

        [JsonPropertyName("useWorker")]
        public bool? UseWorker { get; set; }

        [JsonPropertyName("useSoftTabs")]
        public bool? UseSoftTabs { get; set; }

        [JsonPropertyName("tabSize")]
        public int? TabSize { get; set; }

        [JsonPropertyName("wrap")]
        public int? Wrap { get; set; }

        [JsonPropertyName("foldStyle")]
        public string? FoldStyle => Enum.GetName(FoldStyleOption)?.ToLower();
        [JsonIgnore]
        public FoldStyleOption FoldStyleOption { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode
        {
            get => _mode;
            set => _mode = value?.StartsWith("ace/mode/") == true ? value : $"ace/mode/{value}";
        }
    }

    public enum NewLineModeOption
    {
        Auto, Unix, Window
    }

    public enum FoldStyleOption
    {
        MarkBegin, MarkBeginEnd, Manual
    }
}
