using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Audio;
using OpenAI.Images;
using SkPluginLibrary.Services;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Plugins.NativePlugins;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
	#region Custom Plugins - crawl wiki,c# repl, web search chat, novel writer, prompt engineer

	public async Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "",
		ReplType replType = ReplType.ReplConsole)
	{
		var repl = new ReplCsharpPlugin(CreateChatClient(), _scriptService, _compilerService);
		var codeResult = replType == ReplType.ReplScript
			? await repl.ReplScriptAsync(input, code)
			: await repl.ReplConsoleAsync(input, code);
		var outCode = $"{code}\n{codeResult?.Code}";
		return new CodeOutputModel { Code = outCode.TrimStart('\n'), Output = codeResult?.Output };
	}

	/// <summary>An agent that must search the web and answer with structured output (<see cref="QnAOutput"/>).</summary>
	public async IAsyncEnumerable<PfEvalInput> GenerateEvalInputsFromWeb(List<QnAInput> qnaInputs)
	{
		var webPlugin = ToolPlugin.FromObject(new WebCrawlPlugin(_bingSearchService), "WebSearchPlugin");
		const string systemPrompt =
			"""
			### Instructions
			Answer the user questions by searching the web.
			Always search the web before responding.
			Answer the user's question in 100 words or less and give a complete summary of the web content used as context to generate the answer. The content summary must be 2-3 paragraphs that are 200-300 words long.
			""";
		var agent = CreateAgent(CreateChatClient(AIModel.Gpt4Turbo), systemPrompt, webPlugin.Tools, "EvalWebAgent", new ChatOptions { Temperature = 0.3f });
		foreach (var input in qnaInputs)
		{
			var response = await agent.RunAsync<QnAOutput>(input.Question);
			var output = response.Result;
			yield return new PfEvalInput { Question = input.Question, Answer = input.Answer, Context = output.Context, GroundTruth = output.Answer };
		}
	}

	private AgentFunctionMiddleware CreateProgressMiddleware(Func<string, bool>? skip = null)
	{
		var middleware = new AgentFunctionMiddleware();
		middleware.FunctionInvoking += (_, e) =>
		{
			if (skip?.Invoke(e.Function.Name) == true) return;
			AdditionalAgentText?.Invoke($"\n<h4> Executing {e.Function.Name}</h4>\n\n");
		};
		middleware.FunctionInvoked += HandleCustomFunctionInvoked;
		return middleware;
	}

	public async IAsyncEnumerable<string> RunWebSearchChat(string query)
	{
		var webPlugin = ToolPlugin.FromObject(new WebCrawlPlugin(_bingSearchService), "WebSearchPlugin");
		const string sysPrompt =
			"""
			Answer the user's query using the web search results below. 
			Always search the web before responding. 
			Always include CITATIONS in your response.
			Include a numbered annotation in square brackets after each factual statement (e.g., [1](wwww.example.com)).
			""";
		query = $"""
		         Find the answer to the user question on the web

		         [User Question]

		         {query}

		         Include web page links
		         """;
		var agent = CreateAgent(CreateChatClient(AIModel.Gpt4Turbo), sysPrompt, webPlugin.Tools, "WebSearchAgent",
			new ChatOptions { MaxOutputTokens = 1500, Temperature = 1.0f }, [CreateProgressMiddleware()]);
		await foreach (var content in StreamText(agent, [new ChatMessage(ChatRole.User, query)]))
			yield return content;
	}
	public event Action<string>? AdditionalAgentText;
	public event EventHandler<string>? TextToImageUrl;

	public async IAsyncEnumerable<string> RunWikiSearchChat(string query)
	{
		var wikiPlugin = ToolPlugin.FromObject(new WikiChatPlugin(), "WikiChatPlugin");
		const string systemPrompt =
			"""
			Answer the user's query using Wikipedia search results. 
			Always search the Wikipedia before responding. 
			Always end your response with a list of ## Citations that include links to relevant Wikipedia pages.
			""";
		query = $"""
		         Find the answer to the user question on Wikipedia

		         [User Question]

		         {query}

		         Include page links
		         """;
		var agent = CreateAgent(CreateChatClient(AIModel.Gpt4Turbo), systemPrompt, wikiPlugin.Tools, "WikiSearchAgent",
			new ChatOptions { MaxOutputTokens = 1500, Temperature = 1.0f }, [CreateProgressMiddleware()]);
		await foreach (var content in StreamText(agent, [new ChatMessage(ChatRole.User, query)]))
			yield return content;
	}
	public async Task<NovelOutline> GenerateNovelIdea(NovelGenre genre)
	{
		const string Prompt = """
		                      You are a novel idea generator. Provided a Genre, generate a novel idea.
		                      The idea should contain a Title, a Theme, a few Character Details, and a few key Plot Events.

		                      ## Genre
		                      {{ $genre }}
		                      """;
		// Structured output: the model's reply is deserialized straight into NovelOutline.
		var outline = await PromptRunner.RunAsync<NovelOutline>(CreateChatClient(), Prompt,
			new Dictionary<string, object?> { ["genre"] = genre.ToString() },
			new ChatOptions { MaxOutputTokens = 1024, Temperature = 0.7f });
		return outline!;
	}
	public async Task<string> CreateNovelOutline(string theme, string characterDetails = "", string plotEvents = "",
		string novelTitle = "", int chapters = 15, AIModel aIModel = AIModel.Gpt4OCurrent)
	{
		var novelWriter = new NovelWriterPlugin(aIModel);
		_title = novelTitle;
		var outline = await novelWriter.CreateNovelOutline(theme, characterDetails, plotEvents, novelTitle, chapters);
		_description =
			$"""
			Theme or topic:
			{theme}

			Character Details:
			{characterDetails}

			Plot Events:
			{plotEvents}
			""";
		return outline;
	}
	private string _description = "";
	private string _title = "";
	public async IAsyncEnumerable<string> WriteNovel(string outline, AIModel aiModel = AIModel.Gpt4OCurrent,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var chapters = SplitMarkdownByHeaders(outline);
		var novelPlugin = new NovelWriterPlugin(aiModel);
		var summaryBuilder = new StringBuilder();
		var previousChapter = "none, 1st chapter";
		AdditionalAgentText?.Invoke(JsonSerializer.Serialize(chapters));
		var imageOutlines = "";
		foreach (var chapter in chapters)
		{
			if (cancellationToken.IsCancellationRequested) break;
			var chapterCopy = chapter + "\n\n**Word Count:** 3000 words";
			var chapterText = new StringBuilder();
			imageOutlines += $"\n{chapter}";

			yield return $"[CHAPTER] {chapter.Split('\n')[0]}";
			await foreach (var token in novelPlugin.WriteChapterStreaming(chapterCopy, _description, previousChapter, summaryBuilder.ToString()).WithCancellation(cancellationToken))
			{
				yield return token;
				chapterText.Append(token);
			}
			if (chapters.IndexOf(chapter) == 0 || chapters.IndexOf(chapter) % 3 == 0)
			{
				var imageUrl = await TextToImage(imageOutlines.TrimStart('\n'));
				TextToImageUrl?.Invoke(this, $"<img src='{imageUrl}' width='512' height='512'>");
				imageOutlines = "";
			}
			StringWritten?.Invoke(this, chapterText.ToString());
			yield return "\n\n  ";
			previousChapter = chapterText.ToString();
			var summary = await novelPlugin.SummarizeChapter(previousChapter);
			summaryBuilder.AppendLine(summary);
		}
	}
	private static async Task<string> TextToImage(string prompt)
	{
		// Image generation is not part of the agent abstractions; use the OpenAI SDK client behind the factory directly.
		var imageClient = ChatClientFactory.CreateOpenAIClient(ChatClientFactory.OpenAIProvider).GetImageClient("dall-e-3");
		var result = await imageClient.GenerateImageAsync($"Generate a photo-realistic image for this part of a novel. \n\n## Chapter Outline\n\n {prompt}",
			new OpenAI.Images.ImageGenerationOptions { Size = GeneratedImageSize.W1024xH1024 });
		return result.Value.ImageUri.ToString();
	}
	public async IAsyncEnumerable<ReadOnlyMemory<byte>?> TextToAudioAsync(string text, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var audioClient = ChatClientFactory.CreateOpenAIClient(ChatClientFactory.OpenAIProvider).GetAudioClient("tts-1");
		foreach (var segment in text.SplitText(4096))
		{
			var audio = await audioClient.GenerateSpeechAsync(segment, GeneratedSpeechVoice.Onyx,
				new SpeechGenerationOptions { ResponseFormat = GeneratedSpeechFormat.Mp3, SpeedRatio = 1.0f }, cancellationToken);
			yield return audio.Value.ToMemory();
		}
	}
	public static List<string> SplitMarkdownByHeaders(string markdownText)
	{
		var result = new List<string>();
		var headerPattern = new Regex(@"^(## .+)$", RegexOptions.Multiline);

		var matches = headerPattern.Matches(markdownText);

		var lastIndex = 0;
		foreach (Match match in matches)
		{
			if (lastIndex != match.Index)
			{
				if (lastIndex != 0)
				{
					var segment = markdownText.Substring(lastIndex, match.Index - lastIndex).Trim();
					result.Add(segment);
				}
				else
				{
					// Capture the first header and content if the first header is at the start
					var firstSegment = markdownText[..match.Index].Trim();
					if (!string.IsNullOrEmpty(firstSegment))
					{
						result.Add(firstSegment);
					}
				}
			}
			lastIndex = match.Index;
		}

		if (lastIndex == 0) return result;
		var lastSegment = markdownText[lastIndex..].Trim();
		result.Add(lastSegment);

		return result;
	}
	private void HandleCustomFunctionInvoked(object? sender, AgentFunctionCall call)
	{
		var function = call.Function;
		if (function.Name.StartsWith("func")) return;
		AdditionalAgentText?.Invoke($"\n<h4> {function.Name} Completed</h4>\n\n");
		var result = $"<p>{call.Result}</p>";
		var resultsExpando = $"""

		                      <details>
		                        <summary>See Results</summary>
		                        
		                        <h5>Results</h5>
		                        <p>
		                        {result}
		                        </p>

		                        <br/>
		                      </details>
		                      """;
		AdditionalAgentText?.Invoke(resultsExpando);
	}
	#endregion
}
[TypeConverter(typeof(PfEvalInputConverter))]
public class PfEvalInput
{
	[JsonPropertyName("question")]
	public string? Question { get; set; }

	[JsonPropertyName("ground_truth")]
	public string? GroundTruth { get; set; }

	[JsonPropertyName("answer")]
	public string? Answer { get; set; }

	[JsonPropertyName("context")]
	public string? Context { get; set; }
}
public class PfEvalInputConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

	/// <summary>
	/// This method is used to convert object from string to actual type. This will allow to pass object to
	/// method function which requires it.
	/// </summary>
	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		return JsonSerializer.Deserialize<PfEvalInput>((string)value);
	}

	/// <summary>
	/// This method is used to convert actual type to string representation, so it can be passed to AI
	/// for further processing.
	/// </summary>
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return JsonSerializer.Serialize(value);
	}
}
public record QnAInput([property: JsonPropertyName("question")] string Question, [property: JsonPropertyName("answer")] string Answer);

public record QnAOutput(string Answer, string Context);