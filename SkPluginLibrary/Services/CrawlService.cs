using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;

namespace SkPluginLibrary.Services
{
    public class CrawlService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<CrawlService> _logger;
        private HtmlWeb _htmlWeb = new();
        public CrawlService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<CrawlService>();
        }
        public async Task<List<string>> CrawlAndExtractUrls(string url)
        {
            _logger.LogInformation($"Crawling url {url}");
            var doc = await _htmlWeb.LoadFromWebAsync(url);
            var markdownUrls = doc.DocumentNode?.Descendants("a").Select(x => $"[{x.InnerText}]({x.GetAttributeValue("href", string.Empty)})").ToList() ?? [];
            _logger.LogInformation($"Found {markdownUrls.Count} urls");
            return markdownUrls;
        }
        public async Task<string> CrawlAsync(string url)
        {
            _logger.LogInformation($"Crawling url {url}");
            var doc = await _htmlWeb.LoadFromWebAsync(url);
            return ConvertHtmlToMarkdown(url, doc);
        }

        private string ConvertHtmlToMarkdown(string url, HtmlDocument doc)
        {
            var config = new Config
            {
                UnknownTags = Config.UnknownTagsOption.Bypass,
                GithubFlavored = true,
                RemoveComments = true,
                SmartHrefHandling = true
            };

            var converter = new Converter(config);
            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine(doc.DocumentNode.OuterHtml);
            
            var tidyHtml = Cleaner.PreTidy(htmlBuilder.ToString(), true);

            var mkdwnText = converter.Convert(tidyHtml);
           
            var cleanUpContent = CleanUpContent(mkdwnText);
            _logger.LogInformation($"{url}\n----------------\n Crawled for {StringHelpers.GetTokens(cleanUpContent)} Tokens");
            return cleanUpContent;
        }

        private static bool IsValidHeader(string? tagName)
        {
            if (tagName == null) return false;
            var pattern = new Regex("^h[1-6]$");
            return pattern.IsMatch(tagName);
        }

        private static string CleanUpContent(string content) => content.Replace("\t", " ");
    }
}
