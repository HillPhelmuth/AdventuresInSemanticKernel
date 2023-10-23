using System.Text.Json.Serialization;

namespace BlazorAceEditor.Models
{
    public class AceEditorOptions : AceRenderOptions
    {
        [JsonPropertyName("selectionStyle")]
        public string? SelectionStyleString => Enum.GetName(SelectionStyle)?.ToLower();

        [JsonIgnore]
        public SelectionStyle SelectionStyle { get; set; }
        [JsonPropertyName("highlightActiveLine")]
        public bool? HighlightActiveLine { get; set; }

        [JsonPropertyName("highlightSelectedWord")]
        public bool? HighlightSelectedWord { get; set; }

        [JsonPropertyName("readOnly")]
        public bool? ReadOnly { get; set; }

        [JsonPropertyName("cursorStyle")]
        public string? Cursor => Enum.GetName(CursorStyle)?.ToLower();
        [JsonIgnore]
        public CursorStyle CursorStyle { get; set; }

        [JsonPropertyName("mergeUndoDeltas")]
        public string? MergeUndoDeltas { get; set; }

        [JsonPropertyName("behavioursEnabled")]
        public bool? BehavioursEnabled { get; set; }

        [JsonPropertyName("wrapBehavioursEnabled")]
        public bool? WrapBehavioursEnabled { get; set; }

        [JsonPropertyName("autoScrollEditorIntoView")]
        public bool? AutoScrollEditorIntoView { get; set; }

        [JsonPropertyName("copyWithEmptySelection")]
        public bool? CopyWithEmptySelection { get; set; }

        [JsonPropertyName("useSoftTabs")]
        public bool? UseSoftTabs { get; set; }

        [JsonPropertyName("navigateWithinSoftTabs")]
        public bool? NavigateWithinSoftTabs { get; set; }

        [JsonPropertyName("enableMultiselect")]
        public bool? EnableMultiselect { get; set; }
    }

    public enum SelectionStyle
    {
        Line, Text
    }

    public enum CursorStyle
    {
        Ace, Slim, Smooth, Wide
    }
}
