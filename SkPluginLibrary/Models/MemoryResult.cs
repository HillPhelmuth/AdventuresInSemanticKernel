namespace SkPluginLibrary.Models;

public record MemoryResult(string Title, string Text, int Cluster)
{
    public string? ClusterTitle { get; set; }
    public List<string>? Tags { get; set; }
    public string? ClusterSummary { get; set; }
}