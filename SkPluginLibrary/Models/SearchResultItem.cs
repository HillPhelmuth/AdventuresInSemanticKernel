using System.Text.Json.Serialization;

namespace SkPluginLibrary.Models;

public record SearchResultItem(string Url) 
{
    [JsonPropertyName("content"), JsonPropertyOrder(3)]
    public string? Content { get; set; }
    [JsonPropertyName("title"), JsonPropertyOrder(1)]
    public string? Title { get; set; }
    [JsonPropertyName("url"), JsonPropertyOrder(2)]
    public string Url { get; set; } = Url;
    public string? Snippet { get; set; }

    [JsonPropertyName("additionalLinks"), JsonPropertyOrder(4), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AdditionalLinks { get; set; }
}