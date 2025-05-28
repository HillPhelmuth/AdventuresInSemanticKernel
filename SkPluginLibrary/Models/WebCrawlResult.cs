using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Models;

public class WebCrawlResult
{
    public string? MarkdownContent { get; set; }
    public List<string> LinkUrls { get; set; } = [];
    public string? ContentSummary { get; set; }
}