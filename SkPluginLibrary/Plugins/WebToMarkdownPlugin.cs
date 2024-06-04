using SkPluginLibrary.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using ReverseMarkdown;

namespace SkPluginLibrary.Plugins;

public class WebToMarkdownPlugin
{
	private readonly CrawlService _crawlService;
	public WebToMarkdownPlugin()
	{
		_crawlService = new CrawlService(ConsoleLogger.LoggerFactory);

	}

	[KernelFunction, Description("Retrive the html from a url and convert it to markdown")]
	public async Task<string> ConvertWebToMarkdown([Description("The url to convert to markdown")] string url)
	{
		var markdown = await _crawlService.CrawlAsync(url);
		return markdown;
	}
	[KernelFunction, Description("Convert raw html into markdown format")]
	public async Task<string> ConvertHtmlToMarkdown([Description("The raw html to convert")] string html)
	{
		var config = new Config
		{
			// Include the unknown tag completely in the result (default as well)
			UnknownTags = Config.UnknownTagsOption.Bypass,
			// generate GitHub flavoured markdown, supported for BR, PRE and table tags
			GithubFlavored = true,
			// will ignore all comments
			RemoveComments = true,
			// remove markdown output for links where appropriate
			SmartHrefHandling = true
		};

		var converter = new Converter(config);
		var markdown = converter.Convert(html);
		return markdown;
	}
}