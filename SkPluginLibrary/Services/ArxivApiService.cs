using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace SkPluginLibrary.Services;
public enum SortByOption
{
    Relevance,
    LastUpdatedDate,
    SubmittedDate
}

public enum SortOrderOption
{
    Ascending,
    Descending
}
[TypeConverter(typeof(GenericTypeConverter<ArxivQueryParameters>))]
public class ArxivQueryParameters
{
    public string? SearchQuery { get; set; }
    public string? IdList { get; set; }
    public int? Start { get; set; }
    public int? MaxResults { get; set; }
    public SortByOption? SortBy { get; set; }
    public SortOrderOption? SortOrder { get; set; }

    // Field-specific queries
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Abstract { get; set; }
    public string? Comment { get; set; }
    public string? JournalReference { get; set; }
    public string? SubjectCategory { get; set; }
    public string? ReportNumber { get; set; }

    public override string ToString()
    {
        var query = HttpUtility.ParseQueryString(string.Empty);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query["search_query"] = SearchQuery;
        if (!string.IsNullOrWhiteSpace(IdList))
            query["id_list"] = IdList;
        if (Start.HasValue)
            query["start"] = Start.Value.ToString();
        if (MaxResults.HasValue)
            query["max_results"] = MaxResults.Value.ToString();
        if (SortBy.HasValue)
            query["sortBy"] = SortBy.Value.ToString().ToLower();
        if (SortOrder.HasValue)
            query["sortOrder"] = SortOrder.Value.ToString().ToLower();

        // Add field-specific queries
        if (!string.IsNullOrWhiteSpace(Title))
            query["search_query"] = AddToQuery(query["search_query"], $"ti:{Title}");
        if (!string.IsNullOrWhiteSpace(Author))
            query["search_query"] = AddToQuery(query["search_query"], $"au:{Author}");
        if (!string.IsNullOrWhiteSpace(Abstract))
            query["search_query"] = AddToQuery(query["search_query"], $"abs:{Abstract}");
        if (!string.IsNullOrWhiteSpace(Comment))
            query["search_query"] = AddToQuery(query["search_query"], $"co:{Comment}");
        if (!string.IsNullOrWhiteSpace(JournalReference))
            query["search_query"] = AddToQuery(query["search_query"], $"jr:{JournalReference}");
        if (!string.IsNullOrWhiteSpace(SubjectCategory))
            query["search_query"] = AddToQuery(query["search_query"], $"cat:{SubjectCategory}");
        if (!string.IsNullOrWhiteSpace(ReportNumber))
            query["search_query"] = AddToQuery(query["search_query"], $"rn:{ReportNumber}");

        return query.ToString();
    }

    private string AddToQuery(string existingQuery, string newQueryPart)
    {
        if (string.IsNullOrWhiteSpace(existingQuery))
            return newQueryPart;

        return $"{existingQuery} AND {newQueryPart}";
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SearchQuery) || !string.IsNullOrWhiteSpace(IdList);
    }
}

public class ArxivApiService(ILoggerFactory loggerFactory)
{
    private const string BaseUrl = "http://export.arxiv.org/api/query";
    private static readonly HttpClient HttpClient = new HttpClient();

    public async Task<ArxivFeed> QueryAsync(ArxivQueryParameters parameters)
    {
        if (parameters?.IsValid() != true)
            throw new ArgumentException("Invalid query parameters: Either 'SearchQuery' or 'IdList' must be provided.");

        var requestUrl = $"{BaseUrl}?{parameters}";

        var response = await HttpClient.GetStringAsync(requestUrl);
        try
        {
            var queryAsync = ArxivFeed.Parse(response);
            return queryAsync;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse the API response.", ex);
        }
    }

    public async Task<string> GetContentAsync(ArxivEntry entry)
    {
       
        var getResult = await HttpClient.GetAsync(entry.GetHtmlLink()!);
        if (getResult.IsSuccessStatusCode)
            return await GetHtmlMarkdown(entry) ?? string.Empty;
        
        var docBuilder = new StringBuilder();
        var pdfStream = await HttpClient.GetStreamAsync(entry.PdfLink);
        MemoryStream ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms);
        ms.Position = 0;
        using var document = PdfDocument.Open(ms);
        foreach (var page in document.GetPages())
        {
            var pageBuilder = new StringBuilder();
            foreach (var word in page.GetWords())
            {
                pageBuilder.Append(word.Text);
                pageBuilder.Append(' ');
            }
            var pageText = pageBuilder.ToString();
            
            docBuilder.AppendLine(pageText);
        }
        return docBuilder.ToString();
    }

    private async Task<string?> GetHtmlMarkdown(ArxivEntry entry)
    {
        var crawlService = new CrawlService(loggerFactory);
        var markdown = await crawlService.CrawlAsync(entry.GetHtmlLink()!);
        return markdown;
    }
}