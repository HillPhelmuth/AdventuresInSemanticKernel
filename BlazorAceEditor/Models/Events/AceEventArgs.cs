using System.Text.Json.Serialization;

namespace BlazorAceEditor.Models.Events
{
    public class AceEventArgs
    {
    }
    public delegate void AceChangeEventHandler(object? sender, AceChangeEventArgs e);
    public class AceChangeEventArgs
    {
        [JsonPropertyName("start")]
        public EditorPosition? Start { get; set; }

        [JsonPropertyName("end")]
        public EditorPosition? End { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("lines")]
        public List<string>? Lines { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class EditorPosition
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("column")]
        public int Column { get; set; }
    }
}
