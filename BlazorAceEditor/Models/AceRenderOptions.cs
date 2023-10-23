using System.Text.Json.Serialization;

namespace BlazorAceEditor.Models
{
    public class AceRenderOptions : AceSessionOptions
    {
        private string? _theme;

        [JsonPropertyName("hScrollBarAlwaysVisible")]
        public bool? HScrollBarAlwaysVisible { get; set; }

        [JsonPropertyName("vScrollBarAlwaysVisible")]
        public bool? VScrollBarAlwaysVisible { get; set; }

        [JsonPropertyName("highlightGutterLine")]
        public bool? HighlightGutterLine { get; set; }

        [JsonPropertyName("animatedScroll")]
        public bool? AnimatedScroll { get; set; }

        [JsonPropertyName("showInvisibles")]
        public bool? ShowInvisibles { get; set; }

        [JsonPropertyName("showPrintMargin")]
        public bool? ShowPrintMargin { get; set; }

        [JsonPropertyName("printMarginColumn")]
        public int? PrintMarginColumn { get; set; }

        [JsonPropertyName("print?Margin")]
        public int? PrintMargin { get; set; }

        [JsonPropertyName("fadeFoldWidgets")]
        public bool? FadeFoldWidgets { get; set; }

        [JsonPropertyName("showFoldWidgets")]
        public bool? ShowFoldWidgets { get; set; }

        [JsonPropertyName("showLineNumbers")]
        public bool? ShowLineNumbers { get; set; }

        [JsonPropertyName("showGutter")]
        public bool? ShowGutter { get; set; }

        [JsonPropertyName("displayIndentGuides")]
        public bool? DisplayIndentGuides { get; set; }

        [JsonPropertyName("fontSize")]
        public string? FontSize { get; set; }

        [JsonPropertyName("fontFamily")]
        public string? FontFamily { get; set; }

        [JsonPropertyName("maxLines")]
        public int? MaxLines { get; set; }

        [JsonPropertyName("minLines")]
        public int? MinLines { get; set; }

        [JsonPropertyName("scrollPastEnd")]
        public double ScrollPastEnd { get; set; }

        [JsonPropertyName("fixedWidthGutter")]
        public bool? FixedWidthGutter { get; set; }

        [JsonPropertyName("theme")]
        public string? Theme
        {
            get => _theme;
            set => _theme = value?.StartsWith("ace/theme/") == true ? value : $"ace/theme/{value}";
        }
    }
}
