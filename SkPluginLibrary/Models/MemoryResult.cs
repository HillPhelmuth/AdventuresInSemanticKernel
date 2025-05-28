namespace SkPluginLibrary.Models;

public record MemoryResult(string Title, string Text, int Cluster)
{
    public string? ClusterTitle { get; set; }
    public List<string>? Tags { get; set; }
    public string? ClusterSummary { get; set; }
    public string? MetadataId { get; set; }
    public Dictionary<string, double> Relations { get; set; } = new();
    public double[] Embedding { get; set; } = [];
}