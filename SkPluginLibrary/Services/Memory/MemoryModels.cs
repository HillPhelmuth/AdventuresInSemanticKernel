using System.Text.Json.Serialization;

namespace SkPluginLibrary.Services;

/// <summary>Descriptive information stored alongside an embedding.</summary>
public class MemoryRecordMetadata
{
    [JsonPropertyName("is_reference")] public bool IsReference { get; set; }
    [JsonPropertyName("external_source_name")] public string ExternalSourceName { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("additional_metadata")] public string AdditionalMetadata { get; set; } = "";
}

public class MemoryRecord
{
    public MemoryRecordMetadata Metadata { get; set; } = new();
    public ReadOnlyMemory<float> Embedding { get; set; }
    public string Key { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
}

/// <summary>A memory match with its cosine-similarity relevance.</summary>
public class MemoryQueryResult(MemoryRecordMetadata metadata, double relevance, ReadOnlyMemory<float> embedding)
{
    public MemoryRecordMetadata Metadata { get; } = metadata;
    public double Relevance { get; } = relevance;
    public ReadOnlyMemory<float> Embedding { get; } = embedding;
}
