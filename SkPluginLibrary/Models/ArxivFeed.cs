using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Models;

public class ArxivFeed
{
    public string? Link { get; set; }
    public string? Title { get; set; }
    public string? Id { get; set; }
    public DateTime Updated { get; set; }
    public int TotalResults { get; set; }
    public int StartIndex { get; set; }
    public int ItemsPerPage { get; set; }
    public List<ArxivEntry> Entries { get; set; } = [];
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
    }

    public static ArxivFeed Parse(string xml)
    {
        XNamespace ns = "http://www.w3.org/2005/Atom";
        XNamespace opensearch = "http://a9.com/-/spec/opensearch/1.1/";
        XNamespace arxiv = "http://arxiv.org/schemas/atom";

        var doc = XDocument.Parse(xml);
        var feedElement = doc.Root;

        var feed = new ArxivFeed
        {
            Link = feedElement.Element(ns + "link")?.Attribute("href")?.Value,
            Title = feedElement.Element(ns + "title")?.Value,
            Id = feedElement.Element(ns + "id")?.Value,
            Updated = DateTime.Parse(feedElement.Element(ns + "updated")?.Value ?? DateTime.Now.ToString(CultureInfo.InvariantCulture), null, DateTimeStyles.AdjustToUniversal),
            TotalResults = int.Parse(feedElement.Element(opensearch + "totalResults")?.Value ?? "0"),
            StartIndex = int.Parse(feedElement.Element(opensearch + "startIndex")?.Value ?? "0"),
            ItemsPerPage = int.Parse(feedElement.Element(opensearch + "itemsPerPage")?.Value ?? "0")
        };

        var entries = feedElement.Elements(ns + "entry").Select(entryElement => new ArxivEntry
        {
            Id = entryElement.Element(ns + "id")?.Value,
            Updated = DateTime.Parse(entryElement.Element(ns + "updated")?.Value ?? DateTime.Now.ToString(CultureInfo.InvariantCulture), null, DateTimeStyles.AdjustToUniversal),
            Published = DateTime.Parse(entryElement.Element(ns + "published")?.Value ?? DateTime.Now.ToString(CultureInfo.InvariantCulture), null, DateTimeStyles.AdjustToUniversal),
            Title = entryElement.Element(ns + "title")?.Value,
            Summary = entryElement.Element(ns + "summary")?.Value,
            Authors = entryElement.Elements(ns + "author").Select(a => a.Element(ns + "name")?.Value).ToList(),
            Comment = entryElement.Element(arxiv + "comment")?.Value,
            PdfLink = entryElement.Elements(ns + "link")
                .FirstOrDefault(l => l.Attribute("title")?.Value == "pdf")?.Attribute("href")?.Value,
            PrimaryCategory = entryElement.Element(arxiv + "primary_category")?.Attribute("term")?.Value,
            Categories = entryElement.Elements(ns + "category").Select(c => c.Attribute("term")?.Value).ToList()
        });

        feed.Entries.AddRange(entries);
        return feed;
    }
}
[TypeConverter(typeof(GenericTypeConverter<ArxivEntry>))]
public class ArxivEntry
{
    public string? Id { get; set; }
    public DateTime Updated { get; set; }
    public DateTime Published { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public List<string?> Authors { get; set; } = [];
    public string? Comment { get; set; }
    public string? PdfLink { get; set; }
    public string? PrimaryCategory { get; set; }
    public List<string?> Categories { get; set; } = [];

    public string? GetHtmlLink()
    {
        return PdfLink?.Replace("/pdf/", "/html/");
    }
}