using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Abot2.Crawler;
using AbotX2.Crawler;
using AbotX2.Poco;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;

namespace SkPluginLibrary.Services
{
    public class CrawlService : IDisposable
    {
        private readonly CrawlerX _crawlerX;
        private readonly TaskCompletionSource<string> _taskCompletionSource = new();
        public event PageCrawlEventHandler? OnPageCrawlCompleted;
        public event Action<string>? OnPageCrawlCompletedEventCall;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<CrawlService> _logger;
        public CrawlService(bool useEventResult = false, ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory ?? ConsoleLogger.LogBuilder();
            _logger = _loggerFactory.CreateLogger<CrawlService>();
            var crawlConfigurationX = new CrawlConfigurationX
            {
                MaxPagesToCrawl = 1,
                MaxCrawlDepth = 0,
                //AutoTuning = new AutoTuningConfig
                //{
                //    IsEnabled = true
                //}
            };

            _crawlerX = new CrawlerX(crawlConfigurationX);

            _crawlerX.PageCrawlStarting += ProcessPageCrawlStarting;
            _crawlerX.PageCrawlCompleted += ProcessPageCrawlCompletedEventCall;
            _crawlerX.PageCrawlDisallowed += PageCrawlDisallowed;


        }

        public CrawlService(int maxPages, int maxDepth)
        {
            var crawlConfigurationX = new CrawlConfigurationX
            {
                MaxPagesToCrawl = maxPages,
                MaxCrawlDepth = maxDepth,
                //AutoTuning = new AutoTuningConfig
                //{
                //    IsEnabled = true
                //}
            };

            _crawlerX = new CrawlerX(crawlConfigurationX);

            _crawlerX.PageCrawlStarting += ProcessPageCrawlStarting;
            _crawlerX.PageCrawlCompleted += ProcessPageCrawlCompleted;
            _crawlerX.PageCrawlDisallowed += PageCrawlDisallowed;
        }
        private bool _convertToMarkdown;

        public async Task<string> CrawlAsync(string url, bool convertToMarkdown = true)
        {
            _convertToMarkdown = convertToMarkdown;
            Console.WriteLine($"Crawling url {url}");
            var response = await _crawlerX.CrawlAsync(new Uri(url));
            if (response.ErrorOccurred)
            {
                Console.WriteLine($"Crawl of {url} completed with error: {response.ErrorException.Message}");
                throw new Exception($"Crawl of {url} completed with error: {response.ErrorException.Message}");
            }

            Console.WriteLine($"Crawl of {url} completed without error.");
            return await _taskCompletionSource.Task;

        }
        private void ProcessPageCrawlStarting(object? sender, PageCrawlStartingArgs e)
        {

            var pageToCrawl = e.PageToCrawl;
            var text = $"About to crawl link {pageToCrawl.Uri.AbsoluteUri} which was found on page {pageToCrawl.ParentUri.AbsoluteUri}";
            Console.WriteLine(text);
        }

        private void ProcessPageCrawlCompleted(object? sender, PageCrawlCompletedArgs e)
        {

            var crawledPage = e.CrawledPage;

            if (crawledPage.HttpRequestException != null || crawledPage.HttpResponseMessage.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Crawl of page failed {crawledPage.Uri.AbsoluteUri}");
            }
            else
                Console.WriteLine($"Crawl of page succeeded {crawledPage.Uri.AbsoluteUri}");

            var html = crawledPage.Content.Text;
            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine($"Page had no content {crawledPage.Uri.AbsoluteUri}");
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            string text = "";
            if (_convertToMarkdown)
            {
                var config = new Config
                {
                    // Include the unknown tag completely in the result (default as well)
                    UnknownTags = Config.UnknownTagsOption.Drop,
                    // generate GitHub flavoured markdown, supported for BR, PRE and table tags
                    GithubFlavored = true,
                    // will ignore all comments
                    RemoveComments = true,
                    // remove markdown output for links where appropriate
                    SmartHrefHandling = true
                };

                var converter = new Converter(config);
                var htmlBuilder = new StringBuilder();
                foreach (var child in doc.DocumentNode?.DescendantsAndSelf().Where(x => x.Name?.ToLower() == "p" || IsValidHeader(x.Name?.ToLower())) ?? new List<HtmlNode>())
                {
                    htmlBuilder.Append(child.OuterHtml);
                }
                var tidyHtml = Cleaner.PreTidy(htmlBuilder.ToString(), true);
                var mkdwnText = converter.Convert(tidyHtml);
                var cleanUpContent = CleanUpContent(mkdwnText);
                Console.WriteLine($"{crawledPage.Uri}\n----------------\n Crawled for {StringHelpers.GetTokens(cleanUpContent)} Tokens");
                _taskCompletionSource.SetResult(cleanUpContent);
                return;
            }
            if (crawledPage.Uri.AbsoluteUri.EndsWith("/html"))
                text = html;
            else if (crawledPage.Uri.AbsoluteUri.Contains("wikipedia.org"))
                text = ParseWikiHtmlToText(doc);
            else if (crawledPage.Uri.AbsoluteUri.Contains("open5e.com"))
                text = ParseMainOrSection(doc);
            else
                text = ParseGenericHtmlToText(doc);
            _taskCompletionSource.SetResult(CleanUpContent(text));
        }

        private void ProcessPageCrawlCompletedEventCall(object? sender, PageCrawlCompletedArgs e)
        {
            var crawledPage = e.CrawledPage;

            var crawledAbsUri = crawledPage.Uri.AbsoluteUri;
            if (crawledPage.HttpRequestException != null || crawledPage.HttpResponseMessage.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Crawl of page failed {crawledAbsUri}");
                _logger.LogError("Crawl of page failed {crawledAbsUri}", crawledAbsUri);
            }
            else
            {
                Console.WriteLine($"Crawl of page succeeded {crawledAbsUri}");
                _logger.LogInformation("Crawl of page succeeded {crawledAbsUri}",
                    crawledAbsUri);
            }

            var html = crawledPage.Content.Text;
            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine($"Page had no content {crawledAbsUri}");
                _logger.LogInformation("Page had no content {crawledAbsUri}",
                                       crawledAbsUri);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            string text = "";
            if (_convertToMarkdown)
            {
                var config = new Config
                {
                    // Include the unknown tag completely in the result (default as well)
                    UnknownTags = Config.UnknownTagsOption.Drop,
                    // generate GitHub flavoured markdown, supported for BR, PRE and table tags
                    GithubFlavored = true,
                    // will ignore all comments
                    RemoveComments = true,
                    // remove markdown output for links where appropriate
                    SmartHrefHandling = true
                };

                var converter = new Converter(config);
                var htmlBuilder = new StringBuilder();
                foreach (var child in doc.DocumentNode?.DescendantsAndSelf().Where(x => x.Name?.ToLower() == "p" || IsValidHeader(x.Name?.ToLower())) ?? new List<HtmlNode>())
                {
                    htmlBuilder.Append(child.OuterHtml);
                }
                var tidyHtml = Cleaner.PreTidy(htmlBuilder.ToString(), true);
                var mkdwnText = converter.Convert(tidyHtml);
                var cleanUpContent = CleanUpContent(mkdwnText);
                Console.WriteLine($"{crawledPage.Uri}\n----------------\n Crawled for {StringHelpers.GetTokens(cleanUpContent)} Tokens");
                _taskCompletionSource.SetResult(cleanUpContent);
                return;
            }
            if (crawledAbsUri.EndsWith("/html"))
                text = html;
            else if (crawledAbsUri.Contains("wikipedia.org"))
                text = ParseWikiHtmlToText(doc);
            else
                text = ParseBodyAsHtml(doc);
            if (OnPageCrawlCompleted == null && OnPageCrawlCompletedEventCall == null)
            {
                _taskCompletionSource.SetResult(CleanUpContent(text));
                return;
            }

            OnPageCrawlCompletedEventCall?.Invoke(text);
            OnPageCrawlCompleted?.Invoke(this, new PageCrawlEventArgs(text));

            _taskCompletionSource.SetResult(CleanUpContent(text));
        }
        private static List<string> ParseWikiApiToText(HtmlDocument doc)
        {
            var sw = new Stopwatch();
            sw.Start();
            var result = new List<string>();
            var currentHeader = "";
            foreach (var child in doc.DocumentNode?.DescendantsAndSelf().Where(x => x.Name?.ToLower() == "p" || IsValidHeader(x.Name?.ToLower())) ?? new List<HtmlNode>())
            {
                if (IsValidHeader(child.Name.ToLower()))
                {
                    currentHeader = child.InnerText;
                    continue;
                }

                var item = !string.IsNullOrEmpty(currentHeader) ? $"{currentHeader} --- {child.InnerText}" : child.InnerText;
                result.Add(item);
                Console.WriteLine($"{item} - Elapsed:{sw.ElapsedMilliseconds}ms");
                //sb.Append(child.InnerText);
            }

            return result;
        }

        private static string ParseBodyAsHtml(HtmlDocument htmlDocument)
        {
            var body = htmlDocument.DocumentNode.DescendantsAndSelf().FirstOrDefault(x => x.Name?.ToLower() == "body");
            return body?.InnerHtml ?? "";
        }
        private static string ParseMainOrSection(HtmlDocument doc)
        {
            var main = doc.DocumentNode.DescendantsAndSelf().FirstOrDefault(x => x.Name?.ToLower() == "main");
            if (main is null)
            {
                return doc.DocumentNode.DescendantsAndSelf().FirstOrDefault(x => x.Name?.ToLower() == "section")?.InnerHtml ?? "";
            }
            return main?.InnerHtml ?? "";
        }
        private static bool IsValidHeader(string? tagName)
        {
            if (tagName == null) return false;
            var pattern = new Regex("^h[1-6]$");
            return pattern.IsMatch(tagName);
        }
        private string ParseWikiHtmlToText(HtmlDocument doc)
        {
            HtmlNode? first = null;
            var result = new List<string>();
            foreach (var x in doc.DocumentNode.DescendantsAndSelf())
            {
                if (x.Attributes["id"]?.Value?.Contains("bodyContent", StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    first = x;
                    _logger.LogInformation("Node Added: {node}", x.Name);
                    break;
                }

                //Console.WriteLine($"Node {x.Name} bypassed");
            }
            var sb = new StringBuilder();
            foreach (var child in first?.DescendantsAndSelf().Where(x => x.Name?.ToLower() == "p") ?? new List<HtmlNode>())
            {
                result.Add(child.OuterHtml);
                sb.Append((string?)child.OuterHtml);
            }
            _logger.LogInformation("Wiki Html Parsed. {result} elements added", result.Count);
            return string.Join("\n", result);
        }

        private static string ParseGenericHtmlToText(HtmlDocument doc)
        {
            var pTags = doc?.DocumentNode?.DescendantsAndSelf()?.Where(x => x.Name?.ToLower() == "p" || x.Attributes?["class"]?.Value?.Contains("content") == true)?.Select(x => x.InnerText);
            var sb = new StringBuilder();
            foreach (var tag in pTags)
            {
                sb.Append(tag);
            }

            return sb.ToString();
        }
        private async void PageLinksCrawlDisallowed(object? sender, PageLinksCrawlDisallowedArgs e)
        {
            var crawledPage = e.CrawledPage;
            var text = $"Did not crawl the links on page {crawledPage.Uri.AbsoluteUri} due to {e.DisallowedReason}";
            Console.WriteLine(text);
        }

        private void PageCrawlDisallowed(object? sender, PageCrawlDisallowedArgs e)
        {
            var pageToCrawl = e.PageToCrawl;
            var text = $"Did not crawl page {pageToCrawl.Uri.AbsoluteUri} due to {e.DisallowedReason}";
            throw new Exception(text);

        }

        private string CleanUpContent(string content)
        {
            //var lines = messages.Select(x => x.Replace("\t"," ")).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            //message = string.Join(Environment.NewLine, lines);

            return content.Replace("\t", " ");
        }


        public void Dispose()
        {
            _crawlerX.Dispose();
        }
    }
    public class PageCrawlEventArgs : EventArgs
    {
        public string PageContent { get; set; }

        public PageCrawlEventArgs(string pageContent)
        {
            PageContent = pageContent;
        }
    }

    // Declare a delegate.
    public delegate void PageCrawlEventHandler(object? sender, PageCrawlEventArgs args);
}
