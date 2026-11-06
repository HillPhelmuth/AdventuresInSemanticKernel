using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;
namespace SkPluginLibrary.Models;

public class ContextItem(string id)
{
    public ContextItem() : this(Guid.NewGuid().ToString())
    {
    }
    [VectorStoreKey]
    public string Id { get; set; } = id;
    public string? MemoryId { get; set; }
    [VectorStoreData(IsIndexed = true)]
    public string? Content { get; set; }
       
}

public class VectorStoreContextItem
{
    [VectorStoreKey]
    public string id { get; set; } = Guid.NewGuid().ToString();
    public string? MemoryId { get; set; }
    [VectorStoreData(IsIndexed = true)]
    public string? Content { get; set; }
    [VectorStoreData(IsIndexed = true)]
    public string? Tag { get; set; }

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
public class ResearchVectorStoreContextItem : VectorStoreContextItem
{
    public ResearchVectorStoreContextItem(string link, string? title, string? content, string? source = null)
    {
        Link = link;
        Title = title;
        Content = content;
        Source = source;
    }
    [JsonConstructor]
    public ResearchVectorStoreContextItem()
    {
    }
    [VectorStoreData(IsFullTextIndexed = true)]
    public string? Title { get; set; }
    
    [VectorStoreData(IsIndexed = true)]
    public string? Source { get; set; }
    [VectorStoreData]
    public string? Link { get; set; }
    public ResearchMetadata Metadata
    {
        get => new(Link, Source, Tag);
        set
        {
            Link = value.Link;
            Source = value.Source;
            Tag = value.Tag;
        }
    }

    public string AsContextString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Title: {Title}");
        sb.AppendLine($"Source: {Source}");
        sb.AppendLine($"Reference Link: {Link}");
        sb.AppendLine($"Content:\n{Content}");
        return sb.ToString();
    }
}

public record ResearchMetadata(string? Link, string? Source, string? Tag);
